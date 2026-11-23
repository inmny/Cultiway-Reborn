using System;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.Performance;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>在 Worldfall 的真实三维绘制阶段提交带完整表面资料的形体，不转换为面色槽。</summary>
internal static class FormWorldfallRenderer
{
    /// <summary>该次场景收集已确定的几何、姿态和材质。</summary>
    private static readonly List<DrawEntry> solids = new(), transparent = new();

    /// <summary>与原版共用实例及骨架上传，按真实网格、表面和通道合批。</summary>
    private static readonly FormDrawBatch batch = new();
    /// <summary>本次读取的材质及默认表面色。</summary>
    private static readonly FormFrameSurfaces surfaces = new(native: false);
    /// <summary>当前投影下实际透明提交及对应的合成资源。</summary>
    private static int visibleTransparent;
    private static FormResource transparentResource;

    /// <summary>外部场景的真实深度及前景颜色，透明合成仍使用相同实体遮挡。</summary>
    private static FieldInfo sceneColor, foreground;

    /// <summary>本帧由外部绘制器实际使用的投影及输出尺寸。</summary>
    private static Matrix4x4 viewProjection;

    /// <summary>本帧真实输出像素，独立于窗口缩放及内部超采样。</summary>
    private static Vector2Int outputPixels;

    /// <summary>外部模组实际建立的三维过渡画面。</summary>
    private static FieldInfo nativeScreen;

    /// <summary>实际外部实例和各种三维观察状态，退出后不保留上一帧的资源引用。</summary>
    private static PropertyInfo viewInstance, firstPerson, godShown, flying, diveDone;

    /// <summary>当前帧真正提交的部件数量。</summary>
    internal static int LastDrawCount { get; private set; }

    /// <summary>实体进入场景颜色、资料及深度；透明体进入共用场景深度的前景层。</summary>
    /// <param name="assembly">实际加载的 Worldfall 程序集。</param>
    internal static void Install(Assembly assembly)
    {
        Type renderer = assembly.GetType("FirstPerson.Gpu.GpuRenderer", true);
        sceneColor = renderer.GetField("_sceneCol", BindingFlags.Instance | BindingFlags.NonPublic);
        foreground = renderer.GetField("_front", BindingFlags.Instance | BindingFlags.NonPublic);
        var harmony = new Harmony("cultiway.forms.worldfall");
        harmony.Patch(assembly.GetType("FirstPerson.Gpu.GpuSprites", true).GetMethod("DrawSolid"),
            prefix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(DrawSolid)));
        harmony.Patch(renderer.GetMethod("FlareOpen", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(DrawTransparent)));
        harmony.Patch(renderer.GetMethod("SetGlobals", BindingFlags.Instance | BindingFlags.NonPublic),
            postfix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(SetProjection)));
        Type mod = assembly.GetType("FirstPerson.WorldBoxMod", true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic;

        /// <summary>在安装边界核对实际外部观察属性。</summary>
        /// <param name="name">需要读取的属性名称。</param>
        PropertyInfo ViewProperty(string name) =>
            mod.GetProperty(name, flags) ?? throw new MissingMemberException(mod.FullName, name);

        viewInstance = ViewProperty("Instance");
        firstPerson = ViewProperty("IsFirstPerson");
        godShown = ViewProperty("GodShown");
        flying = ViewProperty("Flying");
        diveDone = ViewProperty("DiveDone");
        nativeScreen = mod.GetField("_godScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        harmony.Patch(mod.GetMethod("BuildGodScreen", BindingFlags.Instance | BindingFlags.NonPublic),
            postfix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(RegisterNativeScreen)));
        Application.quitting += Clear;
    }

    /// <summary>记录当前真实投影，不读取上一帧的着色器全局值。</summary>
    /// <param name="__3">当前观察投影。</param><param name="__8">实际输出宽度。</param><param name="__9">实际输出高度。</param>
    private static void SetProjection(Matrix4x4 __3, int __8, int __9)
    {
        viewProjection = __3;
        outputPixels = new Vector2Int(__8, __9);
    }

    /// <summary>只有真正回到原版并结束过渡后才释放旧三维帧，飞行和附身观察仍保留当前显示。</summary>
    internal static void ClearIfNative()
    {
        object view = viewInstance.GetValue(null);
        if (view == null || (!(bool)firstPerson.GetValue(view) && !(bool)godShown.GetValue(view) &&
                             !(bool)flying.GetValue(view) && (bool)diveDone.GetValue(view))) Clear();
    }

    /// <summary>保留真实三维过渡层的原材质和透明度，让原版模型随完整原版场景一起淡出。</summary>
    /// <param name="__instance">刚建立过渡画面的外部模组。</param>
    private static void RegisterNativeScreen(object __instance)
    {
        GameObject screen = nativeScreen.GetValue(__instance) as GameObject;
        if (screen != null) FormNativeScenePass.Register(screen.GetComponent<Renderer>());
    }

    /// <summary>从同一发布状态生成本视角的绘制资料，只在这里增加地面承托高度。</summary>
    /// <param name="ground">当前场景使用的地面采样。</param>
    /// <param name="bodyShift">实际身体在桥面、游泳及跳跃时的共同偏移。</param>
    internal static void Prepare(Func<float, float, float> ground, Func<long, Vector3> bodyShift)
    {
        using var observation = FrameWorkTrace.Measure("draw", "worldfall.prepare");
        solids.Clear();
        transparent.Clear();
        surfaces.Begin((float)World.world.getCurWorldTime());
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            float height = instance.Terrain == null && instance.Geometry == null
                ? ground(instance.GroundAnchor.x, instance.GroundAnchor.y) : 0f;
            Vector3 shift = instance.GroundActorId == 0 ? Vector3.zero : bodyShift(instance.GroundActorId);
            for (int part = 0; part < instance.PartCount; part++)
            {
                Matrix4x4 transform = instance.PartTransform(part);
                transform.m03 += shift.x;
                transform.m13 += shift.y;
                transform.m23 += height + shift.z;
                AddDraw(in instance, part, transform, new Vector3(shift.x, shift.y, height + shift.z));
            }
        }

        OrganWorldfallRenderer.Append(ground);
    }

    /// <summary>追加已经按真实身体完成转换的器官，不重复加入地表或身体偏移。</summary>
    /// <param name="resource">当前实际资源。</param>
    /// <param name="transform">完整三维世界姿态。</param>
    internal static void AppendWorld(FormResource resource, Matrix4x4 transform)
    {
        var instance = new FormVisualInstance(resource, transform);
        for (int part = 0; part < instance.PartCount; part++)
            AddDraw(in instance, part, instance.PartTransform(part));
    }

    /// <summary>共用形体和器官使用同一材质、深度与提交路径。</summary>
    /// <param name="instance">实际显示实例。</param>
    /// <param name="part">实际资源部件。</param>
    /// <param name="transform">已完成空间转换的姿态。</param>
    /// <param name="shift">本视图实际施加的身体及地面偏移。</param>
    private static void AddDraw(in FormVisualInstance instance, int part, Matrix4x4 transform, Vector3 shift = default)
    {
        var surface = surfaces.Get(instance.Resource, instance.SurfaceFor(part), instance.IsTranslucent);
        Mesh mesh = instance.Geometry ?? instance.Resource.Meshes[part];
        bool opaque = !surface.Transparent && !instance.IsTranslucent;
        var draw = new DrawEntry
        {
            Resource = instance.Resource,
            Build = new Vector4(instance.BuildProgress, FormFormation.TipSpan,
                !instance.DynamicGeometry && instance.Resource.Description.Parts[part].HasBuildData ? 1f : 0f, instance.Feedback.Strength),
            Contact = instance.Feedback.Region(shift),
            Mesh = instance.Layer == null ? mesh : null,
            Transform = transform,
            Material = surface.Material,
            Tint = BodyWorldfallRenderer.ResolveTint(in instance) ?? surface.Color,
            Dye = instance.Dye,
            Skin = !instance.DynamicGeometry && instance.Resource.Description.Parts[part].Skin ? instance.Skin : null,
            ClosedVolume = !instance.DynamicGeometry && instance.Resource.Description.Parts[part].HasSurfaceData,
            Part = part,
            Dynamic = instance.DynamicGeometry,
            Layer = instance.Layer,
            Bounds = instance.Layer != null ? instance.Layer.Bounds :
                instance.Skin != null && instance.Resource.Description.Parts[part].Skin
                ? instance.PoseBounds ?? mesh.bounds
                : mesh.bounds
        };
        (opaque ? solids : transparent).Add(draw);
    }

    /// <summary>场景双颜色目标仍在绑定时写入实体，后方轮廓和雾效读取实体自身资料。</summary>
    /// <param name="__0">当前帧命令缓冲。</param>
    private static void DrawSolid(CommandBuffer __0)
    {
        using var observation = FrameWorkTrace.Measure("draw", "worldfall.solid");
        batch.Begin();
        transparentResource = null;
        LastDrawCount = PrepareVisible(solids, 2);
        visibleTransparent = PrepareVisible(transparent, 4);
        batch.Upload();
        batch.Draw(__0, 2);
        FrameWorkTrace.RecordSubmission("worldfall", LastDrawCount, batch.DrawCount, batch.UploadBytes);
    }

    /// <summary>相机投影已经确定后选择共享网格，主体位置及实际作用保持原资料。</summary>
    /// <param name="draws">当前实体或透明部件。</param>
    /// <param name="pass">共用实体或透明通道。</param>
    private static int PrepareVisible(List<DrawEntry> draws, int pass)
    {
        int count = 0;
        for (int i = 0; i < draws.Count; i++)
        {
            DrawEntry draw = draws[i];
            if (!FormDetail.TryProjected(draw.Bounds, draw.Transform, viewProjection, outputPixels, out draw.Detail)) continue;
            if (draw.Layer != null) draw.Mesh = FormLayerMeshes.Get(draw.Layer);
            else if (!draw.Dynamic) draw.Mesh = draw.Resource.DetailMesh(draw.Part, draw.Detail);
            draws[i] = draw;
            AddBatch(draw, pass);
            if (pass == 4) transparentResource ??= draw.Resource;
            count++;
        }
        return count;
    }

    /// <summary>按片元累加交叉曲面，保留外部前景层和场景深度，不依赖部件中心排序。</summary>
    /// <param name="__instance">当前外部绘制器。</param>
    /// <param name="__0">当前帧命令缓冲。</param>
    private static void DrawTransparent(object __instance, CommandBuffer __0)
    {
        if (visibleTransparent == 0) return;
        using var observation = FrameWorkTrace.Measure("draw", "worldfall.transparent");
        int previousDraws = batch.DrawCount;
        var scene = (RenderTexture)sceneColor.GetValue(__instance);
        var front = (RenderTexture)foreground.GetValue(__instance);
        FormTransparency.Begin(__0, front.width, front.height, scene.depthBuffer);
        batch.Draw(__0, 4);
        FormTransparency.Resolve(__0, transparentResource.Generation.Transparency(), front, scene.depthBuffer,
            new Rect(0, 0, front.width, front.height));
        LastDrawCount += visibleTransparent;
        FrameWorkTrace.RecordSubmission("worldfall", visibleTransparent, batch.DrawCount - previousDraws, 0);
    }

    /// <summary>同一实际部件加入共用批量提交，不改变姿态、表面或透明层。</summary>
    /// <param name="draw">已经完成姿态和分层的部件。</param>
    /// <param name="pass">实体或透明绘制通道。</param>
    private static void AddBatch(DrawEntry draw, int pass)
    {
        batch.Add(new FormDrawItem
        {
            Resource = draw.Resource,
            Mesh = draw.Mesh,
            Material = draw.Material,
            Transform = draw.Transform,
            Normal = draw.Transform.inverse.transpose,
            Tint = draw.Tint,
            Dye = draw.Dye ?? Color.white,
            Skin = draw.Skin,
            Build = draw.Build,
            Contact = draw.Contact,
            ClosedVolume = draw.ClosedVolume
        }, pass);
    }

    /// <summary>一项只读发布部件及本视眼计算的透明排序资料。</summary>
    private struct DrawEntry
    {
        /// <summary>保持所属资源版本存活，命令提交结束前不回收网格和材质。</summary>
        internal FormResource Resource;

        /// <summary>共用网格。</summary>
        internal Mesh Mesh;

        /// <summary>已加地面及身体偏移的姿态。</summary>
        internal Matrix4x4 Transform;

        /// <summary>共用表面。</summary>
        internal Material Material;

        /// <summary>独立实例色及仅显示染色。</summary>
        internal Color Tint;
        internal Color? Dye;

        /// <summary>发布的骨架姿态。</summary>
        internal Matrix4x4[] Skin;

        /// <summary>与当前控制姿态一同发布的成形进度及截面资料标记。</summary>
        internal Vector4 Build;
        /// <summary>与本视角几何一起平移的局部表面区域。</summary>
        internal Vector4 Contact;

        /// <summary>封闭水冰的前后表面共同承担一份体积光程。</summary>
        internal bool ClosedVolume;

        /// <summary>所属真实部件。</summary>
        internal int Part;

        /// <summary>当前实际姿态的保守局部边界。</summary>
        internal Bounds Bounds;

        /// <summary>动态格网仍保留真实水面。</summary>
        internal bool Dynamic;
        /// <summary>水层或传播面的实际动力网格，在确认可见后准备。</summary>
        internal FormLayerFrame Layer;

        /// <summary>本视点当前使用的显示等级。</summary>
        internal int Detail;
    }

    /// <summary>清理旧世界的暂存资料。</summary>
    internal static void Clear()
    {
        solids.Clear();
        transparent.Clear();
        batch.Clear();
        surfaces.Begin(0f);
        transparentResource = null;
        visibleTransparent = 0;
        LastDrawCount = 0;
        OrganWorldfallRenderer.ClearFrame();
    }
}
