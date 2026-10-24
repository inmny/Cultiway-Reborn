using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>连接真实动物表面与共同施法出口；只提交三维外观，不推进身体或法术。</summary>
internal static class OrganWorldfallRenderer
{
    /// <summary>此接合方案仍由专用验证脚本启用，不自动接管正常游戏的身体。</summary>
    internal static bool IsInstalled { get; private set; }
    /// <summary>本版外部动物显示及实际模型资料。</summary>
    private static FieldInfo states, stateMesh, stateFamily, model, parts, partName, positions, triangles, hidden;
    /// <summary>外部身体高度用于附身和游泳，比例调整后同步真实高度。</summary>
    private static FieldInfo tall, stateScale;
    /// <summary>每个外部模型只读取一次基础骨架高度。</summary>
    private static MethodInfo rigFor;
    /// <summary>实际骨架的基础高度。</summary>
    private static PropertyInfo rigHeight;
    /// <summary>当前一次身体收集的单位与活动身体；不跨场景保留。</summary>
    private static Actor currentActor;
    /// <summary>本次身体的共同资料。</summary>
    private static CompiledCreaturePhenotype currentBody;
    /// <summary>本次身体是否在游泳。</summary>
    private static bool swimming;
    /// <summary>完成比例绑定后的真实高度。</summary>
    private static float drawnHeight;
    /// <summary>本次接入采用的器官及身体表面资料。</summary>
    private static Catalog catalog;
    /// <summary>按物种查找制作时声明的安装区域。</summary>
    private static readonly Dictionary<string, Profile> profiles = new(StringComparer.Ordinal);
    /// <summary>同一外部模型只定位一次表面；空值明确表示本版模型尚未接入。</summary>
    private static readonly Dictionary<object, Mount?> mounts = new();
    /// <summary>当前场景真实收集到的身体，每个单位只提交一次。</summary>
    private static readonly Dictionary<long, Capture> captures = new();
    /// <summary>本次真正交给三维绘制的器官姿态，供实机核对。</summary>
    internal static readonly List<Attachment> Prepared = new();
    /// <summary>只在实际场景收集期间接收身体，不接管人物卡片或独立预览。</summary>
    private static bool collecting;

    /// <summary>读取本模组制作资料，并在外部真实身体完成姿态后取得表面位置。</summary>
    /// <param name="assembly">已加载并由外层核对版本的三维模组。</param>
    internal static void Install(Assembly assembly)
    {
        if (IsInstalled) return;
        catalog = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(Path.Combine(
            ModClass.I.GetDeclaration().FolderPath, "Content/CreatureVisuals/worldfall_mounts.json")));
        if (catalog == null || string.IsNullOrEmpty(catalog.LayerId) || string.IsNullOrEmpty(catalog.FormId) || catalog.Bodies == null)
            throw new InvalidDataException("三维器官制作资料缺少外观、形体或身体列表");
        foreach (Profile profile in catalog.Bodies)
        {
            if (string.IsNullOrEmpty(profile.Family) || string.IsNullOrEmpty(profile.Part) ||
                !(profile.Across >= 0f && profile.Across <= 1f && profile.Along >= 0f && profile.Along <= 1f))
                throw new InvalidDataException("三维器官安装区域无效：" + profile.Family);
            profiles.Add(profile.Family, profile);
        }
        Type creatures = assembly.GetType("FirstPerson.Creatures", true);
        Type state = creatures.GetNestedType("State", BindingFlags.NonPublic);
        states = AccessTools.Field(creatures, "States"); stateMesh = AccessTools.Field(state, "Mesh");
        stateFamily = AccessTools.Field(state, "Family");
        tall = AccessTools.Field(state, "Tall");
        stateScale = AccessTools.Field(state, "Scale");
        rigFor = AccessTools.Method(assembly.GetType("FirstPerson.Models", true), "CreatureRigFor");
        rigHeight = assembly.GetType("FirstPerson.Core.CreatureRig", true).GetProperty("Height");
        Type mesh = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        model = AccessTools.Field(mesh, "Model"); hidden = AccessTools.Field(mesh, "Hidden");
        parts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        Type part = assembly.GetType("FirstPerson.Core.MeshPart", true);
        partName = AccessTools.Field(part, "Name"); positions = AccessTools.Field(part, "Pos");
        triangles = AccessTools.Field(part, "Tri");
        var harmony = new Harmony("cultiway.organs.worldfall");
        WorldfallBodyPosture.Install(assembly);
        harmony.Patch(AccessTools.Method(assembly.GetType("FirstPerson.Models", true), "DetailedCreature"),
            postfix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(AfterModel)));
        harmony.Patch(AccessTools.Method(assembly.GetType("FirstPerson.SceneCollector", true), "Collect"),
            prefix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(Begin)),
            postfix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(End)));
        harmony.Patch(AccessTools.Method(creatures, "Add"),
            prefix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(BeforeCreature)),
            postfix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(AfterCreature)));
        harmony.Patch(AccessTools.Method(mesh, "Place"),
            prefix: new HarmonyMethod(typeof(OrganWorldfallRenderer), nameof(PlaceBody)));
        IsInstalled = true;
    }

    /// <summary>每次场景单独收集，多个视眼不沿用上一场景的身体引用。</summary>
    private static void Begin() { captures.Clear(); Prepared.Clear(); currentActor = null; currentBody = null; collecting = true; }
    /// <summary>场景之外的独立绘制不进入世界器官列表。</summary>
    private static void End() => collecting = false;

    /// <summary>在实际身体调用范围内记录共同资料，不让器官有无决定整个身体的比例。</summary>
    /// <param name="__1">当前单位。</param>
    /// <param name="__11">本次显示步长。</param>
    /// <param name="__15">外部实际游泳状态。</param>
    /// <param name="__16">独立步态步长。</param>
    private static void BeforeCreature(Actor __1, ref float __11, bool __15, ref float __16)
    {
        currentActor = null; currentBody = null; drawnHeight = 0f;
        if (!collecting || !__1.GetExtend().TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid ||
            !CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out var compiled)) return;
        currentActor = __1; currentBody = compiled; swimming = __15;
        var bodyStates = (IDictionary)states.GetValue(null);
        if (!bodyStates.Contains(__1)) return;
        object state = bodyStates[__1], mesh = stateMesh.GetValue(state), body = model.GetValue(mesh);
        if (body == null || !mounts.TryGetValue(body, out Mount? mount) || !mount.HasValue) return;
        float scale = ScaleFor(mount.Value);
        if (!(scale > 0f)) return;
        stateScale.SetValue(state, scale);
        // 原动物分支会向固定尺寸缓动；共同身体直接给出当前尺寸，步态仍使用原来的时间。
        if (__16 < 0f) __16 = __11;
        __11 = 0f;
    }

    /// <summary>身体、阴影、箭支和命中盒建立前，按共同静止身体资料绑定实际比例。</summary>
    /// <param name="__instance">即将放置的实际身体。</param>
    /// <param name="__2">原来的脚下高度。</param>
    /// <param name="__4">身体横向尺寸。</param>
    /// <param name="__5">身体纵深尺寸。</param>
    /// <param name="__6">身体竖向尺寸。</param>
    private static void PlaceBody(object __instance, ref float __2, ref float __4, ref float __5, ref float __6)
    {
        if (!collecting || currentActor == null) return;
        object state = ((IDictionary)states.GetValue(null))[currentActor];
        if (!ReferenceEquals(__instance, stateMesh.GetValue(state)) ||
            !profiles.TryGetValue((string)stateFamily.GetValue(state), out Profile profile)) return;
        object body = model.GetValue(__instance);
        Mount? located = FindMount(body, profile);
        if (!located.HasValue) return;
        Mount mount = located.Value;
        float scale = ScaleFor(mount);
        if (!(scale > 0f)) return;
        drawnHeight = mount.Height * scale;
        if (swimming) __2 += (mount.Height * __6 - drawnHeight) * .5f;
        // 按静止颅顶高度等比例对齐，保留原三维身体的体态与部件比例。
        __4 = scale; __5 = scale; __6 = scale;
    }

    /// <summary>静止帧只决定共同身体大小，走路和攻击的帧变化不拉伸身体。</summary>
    /// <param name="mount">实际模型的静止表面。</param>
    private static float ScaleFor(Mount mount)
    {
        if (!ReferenceFrame(out Sprite standing, out CreatureFramePoint point)) return 0f;
        float height = (point.Y - standing.pivot.y) / standing.pixelsPerUnit * Mathf.Abs(currentActor.current_scale.y);
        return height / mount.Point.z;
    }

    /// <summary>读出同一身体静止帧的颅顶资料，不受本帧走路或攻击影响。</summary>
    /// <param name="standing">原版静止参考。</param>
    /// <param name="point">制作时声明的安装位置。</param>
    private static bool ReferenceFrame(out Sprite standing, out CreatureFramePoint point)
    {
        standing = null; point = null;
        AnimationContainerUnit animation = currentActor.animation_container;
        if (animation == null || !animation.has_walking || animation.walking.frames.Length == 0) return false;
        standing = animation.walking.frames[0];
        var layer = Content.Libraries.Manager.CreatureVisualLayerLibrary.get(catalog.LayerId);
        if (!layer.FramesByRig.TryGetValue(currentBody.Morph.VisualRigId, out var frames) ||
            !frames.TryGetValue(standing.name, out var frame) || frame.BodyWidth != standing.rect.width ||
            frame.BodyHeight != standing.rect.height || !frame.Points.TryGetValue("mount", out point)) return false;
        return true;
    }

    /// <summary>贴地长身先调整静止姿态，避免用全身放大去追赶原版头部高度。</summary>
    /// <param name="__0">外部实际身体族。</param>
    /// <param name="__result">本次选出的实际细节模型。</param>
    private static void AfterModel(string __0, ref object __result)
    {
        if (currentActor == null || __result == null || !profiles.TryGetValue(__0, out Profile profile) ||
            !profile.RaisedBody || !ReferenceFrame(out Sprite standing, out CreatureFramePoint point)) return;
        object original = WorldfallBodyPosture.Original(__result);
        Mount? mount = FindMount(original, profile);
        if (!mount.HasValue) return;
        float crownHeight = WorldfallBodyPosture.Length(original) * (point.Y - standing.pivot.y) / standing.rect.width;
        __result = WorldfallBodyPosture.Fit(original, mount.Value.Point, crownHeight);
    }

    /// <summary>外部完成姿态后读取真实表面；没有采用该器官图片时也不提交三维副本。</summary>
    /// <param name="__1">本次真实动物。</param>
    /// <param name="__result">外部是否实际提供了身体模型。</param>
    private static void AfterCreature(Actor __1, bool __result)
    {
        bool controlled = currentActor == __1;
        CompiledCreaturePhenotype compiled = currentBody;
        currentActor = null; currentBody = null;
        if (!collecting || !__result || !controlled) return;
        object state = ((IDictionary)states.GetValue(null))[__1];
        if (drawnHeight > 0f) tall.SetValue(state, drawnHeight);
        if (!CreatureAppearanceService.TryGetAnchor(compiled, __1, catalog.LayerId, "cast", out Vector3 outlet)) return;
        string family = (string)stateFamily.GetValue(state);
        if (!profiles.TryGetValue(family, out Profile profile)) return;
        object mesh = stateMesh.GetValue(state), body = model.GetValue(mesh);
        Mount? found = FindMount(body, profile);
        if (!found.HasValue) return;
        Mount mount = found.Value;
        if (mount.Part < 64 && ((ulong)hidden.GetValue(mesh) & (1UL << mount.Part)) != 0) return;
        Vector3 root = BodyWorldfallRenderer.TransformPoint(mesh, mount.Part, mount.Point);
        Vector3 right = BodyWorldfallRenderer.TransformPoint(mesh, mount.Part,
            mount.Point + Vector3.right * mount.Width) - root;
        captures[__1.id] = new Capture
        {
            Actor = __1.id,
            Root = root,
            Right = right,
            Outlet = outlet,
            Mount = mount,
            Resource = FormResources.Load(catalog.FormId)
        };
    }

    /// <summary>同一模型复用已定位的表面，缺失只报告一次。</summary>
    /// <param name="body">当前外部模型。</param>
    /// <param name="profile">声明的身体安装区域。</param>
    private static Mount? FindMount(object body, Profile profile)
    {
        if (mounts.TryGetValue(body, out Mount? found)) return found;
        found = Locate(body, profile); mounts.Add(body, found);
        if (!found.HasValue) ModClass.LogWarning("Worldfall器官表面未接入：" + profile.Family + "/" + profile.Part);
        return found;
    }

    /// <summary>在声明区域向下与真实三角形相交，安装点落在实际表面而非包围盒顶面。</summary>
    /// <param name="body">本版实际身体模型。</param>
    /// <param name="profile">制作时选择的部件和区域。</param>
    private static Mount? Locate(object body, Profile profile)
    {
        var bodyParts = (Array)parts.GetValue(body);
        for (int part = 0; part < bodyParts.Length; part++)
        {
            object item = bodyParts.GetValue(part);
            if ((string)partName.GetValue(item) != profile.Part) continue;
            var vertices = (float[])positions.GetValue(item); var indices = (int[])triangles.GetValue(item);
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < vertices.Length; i += 3)
            { Vector3 p = new(vertices[i], vertices[i + 1], vertices[i + 2]); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float x = Mathf.Lerp(min.x, max.x, profile.Across), y = Mathf.Lerp(min.y, max.y, profile.Along);
            float top = float.NegativeInfinity;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = Vertex(indices[i]), b = Vertex(indices[i + 1]), c = Vertex(indices[i + 2]);
                float determinant = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(determinant) < 1e-9f) continue;
                float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / determinant;
                float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / determinant;
                if (u < -1e-5f || v < -1e-5f || u + v > 1.00001f) continue;
                top = Mathf.Max(top, u * a.z + v * b.z + (1f - u - v) * c.z);
            }
            if (float.IsNegativeInfinity(top)) return null;
            return new Mount
            {
                Part = part,
                Point = new Vector3(x, y, top),
                Width = max.x - min.x,
                Height = (float)rigHeight.GetValue(rigFor.Invoke(null, new[] { body }))
            };
            Vector3 Vertex(int i) => new(vertices[i * 3], vertices[i * 3 + 1], vertices[i * 3 + 2]);
        }
        return null;
    }

    /// <summary>从真实表面连接到共同出口，出口按同场景旧法术使用的地表规则转换。</summary>
    /// <param name="ground">本次实际场景的地表。</param>
    internal static void Append(Func<float, float, float> ground)
    {
        foreach (Capture capture in captures.Values)
        {
            Vector3 outlet = capture.Outlet; outlet.z += ground(outlet.x, outlet.y);
            Vector3 delta = outlet - capture.Root, axis = delta.normalized;
            Vector3 across = capture.Right - axis * Vector3.Dot(capture.Right, axis);
            if (delta.sqrMagnitude < 1e-8f || across.sqrMagnitude < 1e-10f) continue;
            // 根部直径约为实际头宽的六成；纵向单独连接共同出口，不放大横截面。
            float width = capture.Right.magnitude * .6f / .32f;
            across = across.normalized * width;
            Vector3 depth = Vector3.Cross(axis, across);
            var transform = Matrix4x4.identity;
            transform.SetColumn(0, new Vector4(across.x, across.y, across.z, 0));
            transform.SetColumn(1, new Vector4(depth.x, depth.y, depth.z, 0));
            transform.SetColumn(2, new Vector4(delta.x, delta.y, delta.z, 0));
            transform.SetColumn(3, new Vector4(capture.Root.x, capture.Root.y, capture.Root.z, 1));
            Prepared.Add(new Attachment
            {
                Actor = capture.Actor,
                Root = capture.Root,
                Outlet = outlet,
                Transform = transform,
                Resource = capture.Resource,
                Part = capture.Mount.Part,
                LocalPoint = capture.Mount.Point
            });
            FormWorldfallRenderer.AppendWorld(capture.Resource, transform);
        }
    }

    /// <summary>退出视角后不保留旧帧器官与资源。</summary>
    internal static void ClearFrame() { captures.Clear(); Prepared.Clear(); currentActor = null; currentBody = null; collecting = false; }
    /// <summary>切换世界时同时移除外部模型的定位缓存。</summary>
    internal static void Clear() { ClearFrame(); mounts.Clear(); }

    /// <summary>本模组制作的器官与身体表面说明。</summary>
    private sealed class Catalog
    {
        /// <summary>原版实际外观层。</summary>
        public string LayerId { get; set; }
        /// <summary>共用三维资源。</summary>
        public string FormId { get; set; }
        /// <summary>各身体的安装区域。</summary>
        public Profile[] Bodies { get; set; }
    }
    /// <summary>在指定部件的横向和纵向范围中声明安装区域。</summary>
    private sealed class Profile
    {
        /// <summary>外部实际身体族。</summary>
        public string Family { get; set; }
        /// <summary>随动的实际部件。</summary>
        public string Part { get; set; }
        /// <summary>从左到右的位置比例。</summary>
        public float Across { get; set; }
        /// <summary>从前到后的位置比例。</summary>
        public float Along { get; set; }
        /// <summary>贴地长身按原版长高比例抬起前部，后部仍留在地面。</summary>
        public bool RaisedBody { get; set; }
    }
    /// <summary>一次定位的真实部件与表面坐标。</summary>
    private struct Mount
    {
        /// <summary>实际部件索引。</summary>
        public int Part;
        /// <summary>模型中的真实表面位置。</summary>
        public Vector3 Point;
        /// <summary>同部件横向尺寸。</summary>
        public float Width;
        /// <summary>该身体模型的基础高度。</summary>
        public float Height;
    }
    /// <summary>本场景已完成的真实身体取样。</summary>
    private struct Capture
    {
        /// <summary>真实单位。</summary>
        public long Actor;
        /// <summary>实际表面、横向宽度向量与共同逻辑出口。</summary>
        public Vector3 Root, Right, Outlet;
        /// <summary>实际部件定位。</summary>
        public Mount Mount;
        /// <summary>本次引用的资源版本。</summary>
        public FormResource Resource;
    }
    /// <summary>真正提交的器官姿态，只用于显示与实测。</summary>
    internal struct Attachment
    {
        /// <summary>身体编号。</summary>
        public long Actor;
        /// <summary>三维表面与共同出口。</summary>
        public Vector3 Root, Outlet;
        /// <summary>完整绘制变换。</summary>
        public Matrix4x4 Transform;
        /// <summary>实际资源版本。</summary>
        public FormResource Resource;
        /// <summary>实际身体部件。</summary>
        public int Part;
        /// <summary>部件中的表面位置。</summary>
        public Vector3 LocalPoint;
    }
}
