using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>从实际动物表面借用三维器官，不改变外部身体的正常大小。</summary>
internal static class BodyOrganWorldfall
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
    private static readonly Dictionary<string, Profile> profiles = new(StringComparer.Ordinal);

    /// <summary>同一实际模型只求一次表面和固定站立姿态。</summary>
    private static readonly Dictionary<object, Template> templates = new();

    /// <summary>核对当前外部版本并注册真实模型提供者。</summary>
    /// <param name="assembly">已核对的Worldfall程序集。</param>
    internal static void Install(Assembly assembly)
    {
        Type models = assembly.GetType("FirstPerson.Models", true),
            creatures = assembly.GetType("FirstPerson.Creatures", true);
        meshType = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        vectorType = assembly.GetType("FirstPerson.Core.Vec3", true);
        matrixType = assembly.GetType("FirstPerson.Core.Mat3", true);
        Type part = assembly.GetType("FirstPerson.Core.MeshPart", true);
        familyOf = AccessTools.Method(models, "CreatureFamily");
        detailed = AccessTools.Method(models, "DetailedCreature");
        rigOf = AccessTools.Method(models, "CreatureRigFor");
        stateOf = AccessTools.Method(creatures, "StateOf");
        place = AccessTools.Method(meshType, "Place");
        poseRig = AccessTools.Method(assembly.GetType("FirstPerson.Core.CreatureRig", true), "Pose");
        worldPoint = AccessTools.Method(assembly.GetType("FirstPerson.Core.PersonRig", true), "Transform");
        meshModel = AccessTools.Field(meshType, "Model");
        cacheModel = AccessTools.Field(meshType, "CacheModel");
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
        var catalog = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(Path.Combine(
            ModClass.I.GetDeclaration().FolderPath, "Content/CreatureVisuals/worldfall_mounts.json")));
        if (catalog?.Bodies == null) throw new InvalidDataException("三维器官缺少身体表面目录");
        foreach (Profile profile in catalog.Bodies)
        {
            if (string.IsNullOrWhiteSpace(profile.Family) || string.IsNullOrWhiteSpace(profile.Part) ||
                !(profile.Across >= 0f && profile.Across <= 1f && profile.Along >= 0f && profile.Along <= 1f))
                throw new InvalidDataException("三维器官表面区域无效：" + profile.Family);
            profiles.Add(profile.Family, profile);
        }

        new Harmony("cultiway.body.organ.worldfall").Patch(AccessTools.Method(creatures, "Add"),
            prefix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(BeforeCreature)),
            postfix: new HarmonyMethod(typeof(BodyOrganWorldfall), nameof(AfterCreature)));
        BodyOrganBinding.ModelProvider = Capture;
    }

    /// <summary>只接入已有真实三维资源的器官；游泳和其他模型资料尚未提供时明确拒绝。</summary>
    /// <param name="actor">真实身体。</param><param name="organ">实际器官编号。</param>
    private static BodyOrganModel Capture(Actor actor, string organ)
    {
        if (organ != "yao.horn.thunder" || actor.isAffectedByLiquid() || !actor.isAdult()) return null;
        string family = (string)familyOf.Invoke(null, new object[] { actor });
        if (family == null || !profiles.TryGetValue(family, out Profile profile)) return null;
        object model = detailed.Invoke(null, new object[] { family, 0f, null });
        if (model == null) return null;
        if (!templates.TryGetValue(model, out Template template))
        {
            template = Build(model, profile);
            templates.Add(model, template);
        }

        if (template == null) return null;
        object state = stateOf.Invoke(null, new object[] { actor, family });
        float bodyScale = (float)stateScale.GetValue(state);
        if (bodyScale <= 0f) bodyScale = .5714286f * (.94f + .12f * ((float)stateSeed.GetValue(state) % 1f));
        FormResource resource = FormResources.Load("horn_thunder");
        if (resource.Contacts.Length == 0) throw new InvalidDataException("雷角缺少实际三维作用形状");
        return new BodyOrganModel(resource, template.Mount, template.Rotation, bodyScale,
            template.Width * bodyScale * .8f, template.Poses);
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

        float x = Mathf.Lerp(min.x, max.x, profile.Across),
            y = Mathf.Lerp(min.y, max.y, profile.Along),
            top = float.NegativeInfinity;
        Vector3 normal = Vector3.forward;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = Vertex(triangles[i]), b = Vertex(triangles[i + 1]), c = Vertex(triangles[i + 2]);
            float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(d) < 1e-9f) continue;
            float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / d;
            float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / d;
            float height = u * a.z + v * b.z + (1f - u - v) * c.z;
            if (u < 0 || v < 0 || u + v > 1f || height <= top) continue;
            top = height;
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.z < 0) normal = -normal;
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
            Mount = parent.MultiplyPoint3x4(new Vector3(x, y, top)),
            Rotation = Quaternion.FromToRotation(Vector3.forward, parent.MultiplyVector(normal).normalized),
            Width = max.x - min.x, Poses = new ReadOnlyDictionary<string, Matrix4x4>(poses)
        };
        Vector3 Vertex(int i) => new(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
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
    /// <param name="__11">动画步长。</param><param name="__15">游泳。</param><param name="__16">步态步长。</param>
    private static void BeforeCreature(Actor __1, ref float __3, ref float __4, ref float __5, ref float __8,
        ref float __9, ref float __11, ref bool __15, ref float __16)
    {
        if (!ConstructedAbilityService.TryGetBodyPose(__1.id, out BodyPose pose) ||
            pose.Organ?.Model.HasValue != true ||
            !ConstructedAbilityService.TryGetBodyOrgan(__1.id, out var organ) || organ.Model == null) return;
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
        if (!__result || !ConstructedAbilityService.TryGetBodyOrgan(__1.id, out var organ) ||
            organ.Model == null) return;
        string family = (string)familyOf.Invoke(null, new object[] { __1 });
        object state = stateOf.Invoke(null, new object[] { __1, family });
        if (ReferenceEquals(state, figure.GetValue(null))) return;
        object mesh = stateMesh.GetValue(state);
        var parts = (Array)modelParts.GetValue(meshModel.GetValue(mesh));
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
    internal static void Clear() => templates.Clear();

    /// <summary>制作区域目录。</summary>
    private sealed class Catalog
    {
        /// <summary>实际身体区域。</summary>
        public Profile[] Bodies = Array.Empty<Profile>();
    }

    /// <summary>制作时明确选择的部件及表面区域。</summary>
    private sealed class Profile
    {
        /// <summary>外部物种族。</summary>
        public string Family = string.Empty;

        /// <summary>真实部件名。</summary>
        public string Part = string.Empty;

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