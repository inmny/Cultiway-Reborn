using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>从实际动物表面借用三维器官，不改变外部身体的正常大小。</summary>
internal static partial class BodyOrganWorldfall
{
    /// <summary>外部模型、骨架及个体状态入口。</summary>
    private static MethodInfo familyOf, detailed, rigOf, stateOf, poseRig, place, worldPoint;

    /// <summary>外部模型与姿态的已核对字段。</summary>
    private static FieldInfo meshModel,
        modelParts,
        partName,
        partParent,
        partPivot,
        partPositions,
        partTriangles,
        rotations,
        offsets,
        scales,
        stateMesh,
        stateScale,
        stateSeed,
        figure,
        cacheModel;

    /// <summary>外部数值类型与构造入口。</summary>
    private static Type meshType, vectorType, matrixType;

    /// <summary>外部向量分量。</summary>
    private static FieldInfo vx, vy, vz;

    /// <summary>实际制作安装区域，复用已有表面声明，不采用旧试稿的身体缩放。</summary>
    private static readonly Dictionary<(string family, string mount), Profile> profiles = new();

    /// <summary>按当前真实外观层选择模型，不在运行时猜测尾数或器官种类。</summary>
    private static readonly Dictionary<string, Model> models = new(StringComparer.Ordinal);

    /// <summary>同一实际模型只求一次表面和固定站立姿态。</summary>
    private static readonly Dictionary<(object model, string mount), Template> templates = new();

    /// <summary>本次实际场景的正常器官显示，不跨视角重复保留身体引用。</summary>
    internal static readonly List<(long Actor, string Layer, FormResource Resource, Matrix4x4 Transform)> Prepared =
        new();

    /// <summary>只在真正的世界场景中接收身体，人物卡片不进入世界显示。</summary>
    private static bool collecting;

    /// <summary>外部实例的实际隐藏部件位。</summary>
    private static FieldInfo hidden;

    /// <summary>核对当前外部版本并注册真实模型提供者。</summary>
    /// <param name="assembly">已核对的Worldfall程序集。</param>
    internal static void Install(Assembly assembly)
    {
        Type modelTypes = assembly.GetType("FirstPerson.Models", true),
            creatures = assembly.GetType("FirstPerson.Creatures", true);
        meshType = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        vectorType = assembly.GetType("FirstPerson.Core.Vec3", true);
        matrixType = assembly.GetType("FirstPerson.Core.Mat3", true);
        Type part = assembly.GetType("FirstPerson.Core.MeshPart", true);
        familyOf = AccessTools.Method(modelTypes, "CreatureFamily");
        detailed = AccessTools.Method(modelTypes, "DetailedCreature");
        rigOf = AccessTools.Method(modelTypes, "CreatureRigFor");
        stateOf = AccessTools.Method(creatures, "StateOf");
        place = AccessTools.Method(meshType, "Place");
        poseRig = AccessTools.Method(assembly.GetType("FirstPerson.Core.CreatureRig", true), "Pose");
        worldPoint = AccessTools.Method(assembly.GetType("FirstPerson.Core.PersonRig", true), "Transform");
        meshModel = AccessTools.Field(meshType, "Model");
        cacheModel = AccessTools.Field(meshType, "CacheModel");
        hidden = AccessTools.Field(meshType, "Hidden");
        rotations = AccessTools.Field(meshType, "PartRotation");
        offsets = AccessTools.Field(meshType, "PartOffset");
        scales = AccessTools.Field(meshType, "PartScale");
        modelParts = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshModel", true), "Parts");
        partName = AccessTools.Field(part, "Name");
        partParent = AccessTools.Field(part, "Parent");
        partPivot = AccessTools.Field(part, "Pivot");
        partPositions = AccessTools.Field(part, "Pos");
        partTriangles = AccessTools.Field(part, "Tri");
        Type state = creatures.GetNestedType("State", BindingFlags.NonPublic);
        stateMesh = AccessTools.Field(state, "Mesh");
        stateScale = AccessTools.Field(state, "Scale");
        stateSeed = AccessTools.Field(state, "Seed");
        figure = AccessTools.Field(creatures, "_figure");
        vx = AccessTools.Field(vectorType, "X");
        vy = AccessTools.Field(vectorType, "Y");
        vz = AccessTools.Field(vectorType, "Z");
        InstallSurfaces(assembly);
        InstallPatterns(assembly);
        var catalog = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(Path.Combine(
            ModClass.I.GetDeclaration().FolderPath, "Content/CreatureVisuals/organ_mounts_3d.json")));
        if (catalog?.Bodies == null || catalog.Models == null) throw new InvalidDataException("三维器官缺少身体表面目录");
        foreach (Profile profile in catalog.Bodies)
        {
            if (string.IsNullOrWhiteSpace(profile.Family) || string.IsNullOrWhiteSpace(profile.Part) ||
                string.IsNullOrWhiteSpace(profile.Mount) ||
                profile.Axis is not (1 or 2) ||
                !(profile.Across >= 0f && profile.Across <= 1f && profile.Along >= 0f && profile.Along <= 1f))
                throw new InvalidDataException("三维器官表面区域无效：" + profile.Family);
            profiles.Add((profile.Family, profile.Mount), profile);
        }

        foreach (Model model in catalog.Models)
        {
            if (string.IsNullOrWhiteSpace(model.Layer) || string.IsNullOrWhiteSpace(model.Form) ||
                string.IsNullOrWhiteSpace(model.Mount) || !(model.Width > 0f) ||
                float.IsInfinity(model.Width)) throw new InvalidDataException("三维器官模型登记无效：" + model.Layer);
            models.Add(model.Layer, model);
        }

        new Harmony("cultiway.body.organ.worldfall").Patch(AccessTools.Method(creatures, "Add"),
            prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforeCreature)),
            postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterCreature)));
        BodyOrganBinding.ModelProvider = Capture;
    }

    /// <summary>只接入已有真实三维资源的器官；游泳和其他模型资料尚未提供时明确拒绝。</summary>
    /// <param name="actor">真实身体。</param><param name="layer">当前实际采用的外观层。</param>
    private static BodyOrganModel Capture(Actor actor, CreatureVisualLayerAsset layer)
    {
        if (actor.isAffectedByLiquid() || !actor.isAdult() ||
            !models.TryGetValue(layer.id, out Model entry)) return null;
        string family = (string)familyOf.Invoke(null, new object[] { actor });
        if (family == null || !profiles.TryGetValue((family, entry.Mount), out Profile profile)) return null;
        object model = detailed.Invoke(null, new object[] { family, 0f, null });
        if (model == null) return null;
        if (!templates.TryGetValue((model, entry.Mount), out Template template))
        {
            template = Build(model, profile);
            templates.Add((model, entry.Mount), template);
        }

        if (template == null) return null;
        object state = stateOf.Invoke(null, new object[] { actor, family });
        float bodyScale = (float)stateScale.GetValue(state);
        if (bodyScale <= 0f) bodyScale = .5714286f * (.94f + .12f * ((float)stateSeed.GetValue(state) % 1f));
        FormResource resource = FormResources.Load(entry.Form);
        if (resource.Contacts.Length == 0) throw new InvalidDataException("器官缺少实际三维作用形状：" + entry.Form);
        return new BodyOrganModel(resource, template.Mount, template.Rotation, bodyScale,
            template.Width * bodyScale * entry.Width, template.Poses, profile.Part);
    }

    /// <summary>从最高精度实际网格取表面，再用外部正式骨架生成固定站立姿态。</summary>
    /// <param name="model">实际模型。</param><param name="profile">明确制作区域。</param>
    private static Template Build(object model, Profile profile)
    {
        var parts = (Array)modelParts.GetValue(model);
        int selected = -1;
        for (int i = 0; i < parts.Length; i++)
            if ((string)partName.GetValue(parts.GetValue(i)) == profile.Part)
                selected = i;
        if (selected < 0) return null;
        object part = parts.GetValue(selected);
        float[] positions = (float[])partPositions.GetValue(part);
        int[] triangles = (int[])partTriangles.GetValue(part);
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int i = 0; i < positions.Length; i += 3)
        {
            Vector3 point = new(positions[i], positions[i + 1], positions[i + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        bool rear = profile.Axis == 1;
        float x = Mathf.Lerp(min.x, max.x, profile.Across),
            y = Mathf.Lerp(rear ? min.z : min.y, rear ? max.z : max.y, profile.Along),
            top = float.NegativeInfinity;
        Vector3 normal = Vector3.forward;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = Project(Vertex(triangles[i])),
                b = Project(Vertex(triangles[i + 1])),
                c = Project(Vertex(triangles[i + 2]));
            float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(d) < 1e-9f) continue;
            float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / d;
            float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / d;
            float height = u * a.z + v * b.z + (1f - u - v) * c.z;
            if (u < 0 || v < 0 || u + v > 1f || height <= top) continue;
            top = height;
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.z < 0) normal = -normal;
            normal = Project(normal);
        }

        if (float.IsNegativeInfinity(top)) return null;
        object mesh = Activator.CreateInstance(meshType);
        meshModel.SetValue(mesh, model);
        object rig = rigOf.Invoke(null, new[] { model });
        poseRig.Invoke(rig, new[] { mesh, (object)0f, 0f, 0f, false, 0f, 0f });
        place.Invoke(mesh, new object[] { 0f, 0f, 0f, -Mathf.PI / 2f, 1f, 1f, 1f });
        var poses = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        for (int i = 0; i < parts.Length; i++) poses.Add((string)partName.GetValue(parts.GetValue(i)), Pose(mesh, i));
        Matrix4x4 parent = poses[profile.Part];
        return new Template
        {
            Mount = parent.MultiplyPoint3x4(Project(new Vector3(x, y, top))),
            Rotation = Quaternion.FromToRotation(Vector3.forward, parent.MultiplyVector(normal).normalized),
            Width = max.x - min.x, Poses = new ReadOnlyDictionary<string, Matrix4x4>(poses)
        };
        Vector3 Vertex(int i) => new(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
        Vector3 Project(Vector3 p) => rear ? new Vector3(p.x, p.z, p.y) : p;
    }

    /// <summary>用四个实际变换点读取完整部件矩阵，保留真实父部件和原枢轴。</summary>
    /// <param name="mesh">实际骨架实例。</param><param name="part">部件索引。</param>
    private static Matrix4x4 Pose(object mesh, int part)
    {
        Vector3 p = Point(mesh, part, Vector3.zero);
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, Point(mesh, part, Vector3.right) - p);
        matrix.SetColumn(1, Point(mesh, part, Vector3.up) - p);
        matrix.SetColumn(2, Point(mesh, part, Vector3.forward) - p);
        matrix.SetColumn(3, new Vector4(p.x, p.y, p.z, 1f));
        return matrix;
    }

    /// <summary>沿外部正式转换读取点，只在捕获身体资料时调用。</summary>
    /// <param name="mesh">实际实例。</param><param name="part">部件索引。</param><param name="point">模型点。</param>
    private static Vector3 Point(object mesh, int part, Vector3 point) => Read(worldPoint.Invoke(null,
        new[] { mesh, (object)part, Vector(point) }));

    /// <summary>接管期间使用同轮身体根部、朝向和实际大小，不按原版图片重定三维比例。</summary>
    /// <param name="__1">实际动物。</param><param name="__3">横向。</param><param name="__4">纵向。</param>
    /// <param name="__5">高度。</param><param name="__8">方向。</param><param name="__9">步速。</param>
    /// <param name="__10">目标的实际模型精度。</param>
    /// <param name="__11">动画步长。</param><param name="__15">游泳。</param><param name="__16">步态步长。</param>
    private static void BeforeCreature(Actor __1, ref float __3, ref float __4, ref float __5, ref float __8,
        ref float __9, ref float __10, ref float __11, ref bool __15, ref float __16)
    {
        patternActor = collecting ? __1 : null;
        if (PrepareSurfaceDisplay(__1, ref __3, ref __4, ref __5, ref __8, ref __9, ref __10, ref __11,
                ref __15, ref __16)) return;
        var organs = ConstructedAbilityService.GetBodyOrgans(__1.id);
        if (organs.Count == 0 || organs[0].Binding.Model == null) return;
        BodyPose pose = organs[0].Pose;
        BodyOrganBinding organ = organs[0].Binding;
        string family = (string)familyOf.Invoke(null, new object[] { __1 });
        object state = stateOf.Invoke(null, new object[] { __1, family });
        if (ReferenceEquals(state, figure.GetValue(null))) return;
        Vector3 foot = pose.Foot + WorldfallVisualBridge.BodyShift(__1);
        __3 = foot.x;
        __4 = foot.y;
        __5 = foot.z;
        __8 = Mathf.Atan2(pose.Forward.y, pose.Forward.x);
        __9 = 0f;
        __11 = 0f;
        __15 = false;
        __16 = 0f;
        stateScale.SetValue(state, organ.Model.BodyScale);
    }

    /// <summary>各显示精度采用相同的实际站立姿态；结束后外部骨架继续正常推进。</summary>
    /// <param name="__1">实际动物。</param><param name="__result">实际身体是否已提交。</param>
    private static void AfterCreature(Actor __1, bool __result)
    {
        if (__result && collecting) FinishPatterns(__1);
        else patternActor = null;
        if (!__result || !collecting) return;
        if (ConstructedAbilityService.TryGetBodySurface(__1.id, out BodySurfaceBinding binding,
                out BodySurfacePose surface) && binding is CreatureSurface heldBody)
            heldBody.Display(surface);
        var borrowed = ConstructedAbilityService.GetBodyOrgans(__1.id);
        bool controlled = borrowed.Count > 0 && borrowed[0].Binding.Model != null;
        if (!controlled && !BodyOrganBinding.VisibleLayers(__1).Any(layer => models.ContainsKey(layer.id))) return;
        string family = (string)familyOf.Invoke(null, new object[] { __1 });
        object state = stateOf.Invoke(null, new object[] { __1, family });
        if (ReferenceEquals(state, figure.GetValue(null))) return;
        object mesh = stateMesh.GetValue(state);
        var parts = (Array)modelParts.GetValue(meshModel.GetValue(mesh));
        if (!controlled)
        {
            Collect(__1, mesh, parts, borrowed);
            return;
        }

        BodyOrganBinding organ = borrowed[0].Binding;
        var turns = (Array)rotations.GetValue(mesh);
        var shifts = (Array)offsets.GetValue(mesh);
        var sizes = (float[])scales.GetValue(mesh);
        for (int i = 0; i < parts.Length; i++)
        {
            object part = parts.GetValue(i);
            if (!organ.Model.Parts.TryGetValue((string)partName.GetValue(part), out Matrix4x4 target)) continue;
            int parent = (int)partParent.GetValue(part);
            if (parent >= 0 && organ.Model.Parts.TryGetValue((string)partName.GetValue(parts.GetValue(parent)),
                    out Matrix4x4 parentPose))
                target = parentPose.inverse * target;
            Vector3 pivot = Read(partPivot.GetValue(part));
            turns.SetValue(Matrix(target), i);
            sizes[i] = 1f;
            shifts.SetValue(Vector((Vector3)target.GetColumn(3) - pivot + target.MultiplyVector(pivot)), i);
        }

        cacheModel.SetValue(mesh, null);
        foreach (var item in borrowed) HideParts(mesh, parts, models[item.Binding.Layer.id].Replaces);
        Collect(__1, mesh, parts, borrowed);
    }

    /// <summary>沿实际外部骨架显示未被动作借走的器官；出招结束不使器官突然消失。</summary>
    /// <param name="actor">实际身体。</param><param name="mesh">本次已完成姿态的模型。</param>
    /// <param name="parts">当前显示精度的部件。</param><param name="borrowed">动作已经绘制的全部器官及其姿态。</param>
    private static void Collect(Actor actor, object mesh, Array parts, IReadOnlyList<ConstructedAbilityService.PresentedOrgan> borrowed)
    {
        foreach (CreatureVisualLayerAsset layer in BodyOrganBinding.VisibleLayers(actor))
        {
            if (borrowed.Any(item => item.Binding.Layer == layer && item.Binding.PartId == null) || !models.ContainsKey(layer.id)) continue;
            BodyOrganModel organ = Capture(actor, layer);
            if (organ == null) continue;
            for (int i = 0; i < parts.Length; i++)
            {
                if ((string)partName.GetValue(parts.GetValue(i)) != organ.Parent) continue;
                Matrix4x4 matrix = Pose(mesh, i) * organ.Parts[organ.Parent].inverse *
                                   Matrix4x4.TRS(organ.Mount, organ.Rotation,
                                       Vector3.one * (organ.Scale / organ.BodyScale));
                if (!borrowed.Any(item => item.Binding.Layer == layer))
                    Prepared.Add((actor.id, layer.id, organ.Resource, matrix));
                else
                    for (int part = 0; part < organ.Resource.Description.Parts.Length; part++)
                    {
                        string control = organ.Resource.Description.Parts[part].Control;
                        if (!borrowed.Any(item => item.Binding.Layer == layer && item.Binding.PartId == control))
                            Prepared.Add((actor.id, layer.id, organ.Resource.Part(part), matrix));
                    }
                HideParts(mesh, parts, models[layer.id].Replaces);
                break;
            }
        }
    }

    /// <summary>升级器官替代明确登记的原部件，避免尾冠或鸟翼之外再叠一份默认器官。</summary>
    /// <param name="mesh">本次真实身体。</param><param name="parts">当前部件。</param>
    /// <param name="replaces">被实际器官替代的原部件。</param>
    private static void HideParts(object mesh, Array parts, string[] replaces)
    {
        if (replaces.Length == 0) return;
        for (int i = 0; i < parts.Length && i < 64; i++)
            if (replaces.Contains((string)partName.GetValue(parts.GetValue(i))))
            {
                hidden.SetValue(mesh, (ulong)hidden.GetValue(mesh) | (1UL << i));
                cacheModel.SetValue(mesh, null);
            }
    }

    /// <summary>每个场景从实际收集的身体重新建立，多个视角互不借用旧帧。</summary>
    internal static void BeginScene()
    {
        Prepared.Clear();
        collecting = true;
    }

    /// <summary>追加已经完成实际骨架转换的正常器官，不重复增加地面高度。</summary>
    internal static void AppendScene()
    {
        collecting = false;
        patternActor = null;
        foreach (var display in Prepared) FormWorldfallRenderer.AppendWorld(display.Resource, display.Transform);
    }

    /// <summary>把内部数值向量交给已核对的外部格式。</summary>
    /// <param name="point">三维点。</param>
    private static object Vector(Vector3 point) => Activator.CreateInstance(vectorType, point.x, point.y, point.z);

    /// <summary>读取外部向量。</summary>
    /// <param name="value">外部值。</param>
    private static Vector3 Read(object value) =>
        new((float)vx.GetValue(value), (float)vy.GetValue(value), (float)vz.GetValue(value));

    /// <summary>写入外部实际线性变换，平移仍由部件偏移承担。</summary>
    /// <param name="matrix">部件局部变换。</param>
    private static object Matrix(Matrix4x4 matrix) => Activator.CreateInstance(matrixType,
        Vector(matrix.GetColumn(0)), Vector(matrix.GetColumn(1)), Vector(matrix.GetColumn(2)));

    /// <summary>清理当前世界的模型资料。</summary>
    internal static void Clear()
    {
        templates.Clear();
        Prepared.Clear();
        collecting = false;
        ClearPatterns();
        patternActor = null;
        ClearDragons();
    }

    /// <summary>制作区域目录。</summary>
    private sealed class Catalog
    {
        /// <summary>实际身体区域。</summary>
        public Profile[] Bodies = Array.Empty<Profile>();

        /// <summary>已实现独立外观的真实三维资源。</summary>
        public Model[] Models = Array.Empty<Model>();
    }

    /// <summary>实际外观到模型及安装类别的明确对应。</summary>
    private sealed class Model
    {
        /// <summary>实际外观层、共用模型与安装类别。</summary>
        public string Layer = string.Empty, Form = string.Empty, Mount = string.Empty;

        /// <summary>升级器官接替的实际外部部件，未列出的身体保持正常显示。</summary>
        public string[] Replaces = Array.Empty<string>();

        /// <summary>器官模型相对实际安装部件宽度的比例。</summary>
        public float Width = 0f;
    }

    /// <summary>制作时明确选择的部件及表面区域。</summary>
    private sealed class Profile
    {
        /// <summary>外部物种族。</summary>
        public string Family = string.Empty;

        /// <summary>真实部件名。</summary>
        public string Part = string.Empty;

        /// <summary>头顶、背顶或尾根。</summary>
        public string Mount = string.Empty;

        /// <summary>沿模型高度或纵向寻找实际外表面，2为高度，1为纵向。</summary>
        public int Axis = 2;

        /// <summary>横向与纵向的制作比例。</summary>
        public float Across = .5f, Along = .65f;
    }

    /// <summary>每个模型共享的不可变安装和站立资料。</summary>
    private sealed class Template
    {
        /// <summary>模型安装点。</summary>
        internal Vector3 Mount;

        /// <summary>模型方向。</summary>
        internal Quaternion Rotation;

        /// <summary>安装部件的实际宽度。</summary>
        internal float Width;

        /// <summary>命名部件的实际整体姿态。</summary>
        internal IReadOnlyDictionary<string, Matrix4x4> Poses;
    }
}
