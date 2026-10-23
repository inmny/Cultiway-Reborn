using System;
using System.Collections.Generic;
using System.Reflection;
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

    /// <summary>命令提交时复制的实例颜色，避免不同人物共用材质时互相染色。</summary>
    private static readonly MaterialPropertyBlock properties = new();

    /// <summary>外部绘制器的实际透视相机，兼顾第一人称和独立视眼。</summary>
    private static FieldInfo hasEye, eye, eyePosition, eyeForward;

    /// <summary>外部模组实际建立的三维过渡画面。</summary>
    private static FieldInfo nativeScreen;

    /// <summary>实际外部实例和各种三维观察状态，退出后不保留上一帧的资源引用。</summary>
    private static PropertyInfo viewInstance, firstPerson, godShown, flying, diveDone;

    /// <summary>普通三维观察的视点与前方，透视绘制时由实际视眼覆盖。</summary>
    private static Vector3 viewPosition, viewForward;

    /// <summary>当前帧真正提交的部件数量。</summary>
    internal static int LastDrawCount { get; private set; }

    /// <summary>实体进入场景颜色、资料及深度；透明体进入共用场景深度的前景层。</summary>
    /// <param name="assembly">实际加载的 Worldfall 程序集。</param>
    internal static void Install(Assembly assembly)
    {
        Type renderer = assembly.GetType("FirstPerson.Gpu.GpuRenderer", true);
        hasEye = renderer.GetField("HasEye");
        eye = renderer.GetField("Eye");
        eyePosition = eye.FieldType.GetField("Position");
        eyeForward = eye.FieldType.GetField("Forward");
        var harmony = new Harmony("cultiway.forms.worldfall");
        harmony.Patch(assembly.GetType("FirstPerson.Gpu.GpuSprites", true).GetMethod("DrawSolid"),
            prefix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(DrawSolid)));
        harmony.Patch(renderer.GetMethod("FlareOpen", BindingFlags.Instance | BindingFlags.NonPublic),
            prefix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(DrawTransparent)));
        Type mod = assembly.GetType("FirstPerson.WorldBoxMod", true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        /// <summary>在安装边界核对实际外部观察属性。</summary>
        /// <param name="name">需要读取的属性名称。</param>
        PropertyInfo ViewProperty(string name) => mod.GetProperty(name, flags) ?? throw new MissingMemberException(mod.FullName, name);
        viewInstance = ViewProperty("Instance"); firstPerson = ViewProperty("IsFirstPerson");
        godShown = ViewProperty("GodShown"); flying = ViewProperty("Flying"); diveDone = ViewProperty("DiveDone");
        nativeScreen = mod.GetField("_godScreen", BindingFlags.Instance | BindingFlags.NonPublic);
        harmony.Patch(mod.GetMethod("BuildGodScreen", BindingFlags.Instance | BindingFlags.NonPublic),
            postfix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(RegisterNativeScreen)));
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
    /// <param name="position">普通三维观察的视点。</param>
    /// <param name="forward">普通三维观察的深度方向。</param>
    internal static void Prepare(Func<float, float, float> ground, Func<long, Vector3> bodyShift,
        Vector3 position, Vector3 forward)
    {
        solids.Clear();
        transparent.Clear();
        viewPosition = position;
        viewForward = forward;
        float time = (float)World.world.getCurWorldTime();
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            float height = instance.Terrain == null ? ground(instance.GroundAnchor.x, instance.GroundAnchor.y) : 0f;
            Vector3 shift = instance.GroundActorId == 0 ? Vector3.zero : bodyShift(instance.GroundActorId);
            for (int part = 0; part < instance.PartCount; part++)
            {
                Matrix4x4 transform = instance.PartTransform(part);
                transform.m03 += shift.x;
                transform.m13 += shift.y;
                transform.m23 += height + shift.z;
                Material material = instance.Resource.Surface(instance.SurfaceFor(part), false);
                material.SetFloat("_WorldTime", time);
                Mesh mesh = instance.MeshFor(part);
                bool opaque = material.GetFloat("_ZWrite") > .5f &&
                              (!instance.Tint.HasValue || instance.Tint.Value.a >= 1f);
                var draw = new DrawEntry
                {
                    Resource = instance.Resource, Mesh = mesh, Transform = transform, Material = material,
                    Tint = instance.Tint, Dye = instance.Dye,
                    Skin = instance.Layer == null && instance.Resource.Description.Parts[part].Skin
                        ? instance.Skin
                        : null,
                    Centre = transform.MultiplyPoint3x4(
                        instance.Skin != null && instance.Resource.Description.Parts[part].Skin
                            ? instance.PoseBounds?.center ?? mesh.bounds.center
                            : mesh.bounds.center),
                    Order = transparent.Count
                };
                (opaque ? solids : transparent).Add(draw);
            }
        }
    }

    /// <summary>场景双颜色目标仍在绑定时写入实体，后方轮廓和雾效读取实体自身资料。</summary>
    /// <param name="__0">当前帧命令缓冲。</param>
    private static void DrawSolid(CommandBuffer __0)
    {
        LastDrawCount = solids.Count;
        foreach (DrawEntry draw in solids) Submit(__0, draw, 2);
    }

    /// <summary>按当前实际视眼深度从远到近提交透明体，相同深度保留发布顺序。</summary>
    /// <param name="__instance">当前外部绘制器。</param>
    /// <param name="__0">当前帧命令缓冲。</param>
    private static void DrawTransparent(object __instance, CommandBuffer __0)
    {
        Vector3 position = viewPosition, forward = viewForward;
        if (transparent.Count > 1 && (bool)hasEye.GetValue(__instance))
        {
            object camera = eye.GetValue(__instance);
            position = (Vector3)eyePosition.GetValue(camera);
            forward = (Vector3)eyeForward.GetValue(camera);
        }

        for (int i = 0; i < transparent.Count; i++)
        {
            DrawEntry draw = transparent[i];
            draw.Depth = Vector3.Dot(draw.Centre - position, forward);
            transparent[i] = draw;
        }

        transparent.Sort((a, b) => a.Depth == b.Depth ? a.Order.CompareTo(b.Order) : b.Depth.CompareTo(a.Depth));
        foreach (DrawEntry draw in transparent) Submit(__0, draw, 1);
        LastDrawCount += transparent.Count;
    }

    /// <summary>复制本次部件属性到命令中，不改动共享材质。</summary>
    /// <param name="commands">当前绘制命令。</param>
    /// <param name="draw">已经完成姿态和分层的部件。</param>
    /// <param name="pass">实体或透明绘制通道。</param>
    private static void Submit(CommandBuffer commands, DrawEntry draw, int pass)
    {
        properties.Clear();
        if (draw.Tint.HasValue) properties.SetColor("_Color", draw.Tint.Value);
        properties.SetColor("_Dye", draw.Dye ?? Color.white);
        properties.SetFloat("_UseSkinning", draw.Skin == null ? 0f : 1f);
        if (draw.Skin != null) properties.SetMatrixArray("_FormBones", draw.Skin);
        commands.DrawMesh(draw.Mesh, draw.Transform, draw.Material, 0, pass, properties);
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
        internal Color? Tint, Dye;

        /// <summary>发布的骨架姿态。</summary>
        internal Matrix4x4[] Skin;

        /// <summary>实际姿态边界中心。</summary>
        internal Vector3 Centre;

        /// <summary>当前视眼的前方深度。</summary>
        internal float Depth;

        /// <summary>相同深度的稳定发布顺序。</summary>
        internal int Order;
    }

    /// <summary>清理旧世界的暂存资料。</summary>
    internal static void Clear()
    {
        solids.Clear();
        transparent.Clear();
        LastDrawCount = 0;
    }
}
