using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>已构建形体的部件、挂点及初始作用形状；不保存单次施放进度。</summary>
public sealed class FormDescription
{
    /// <summary>当前发布资料格式；更新制作字段时同步重建资源。</summary>
    public const int FormatVersion = 8;

    /// <summary>形体资料版本。</summary>
    public int Version;

    /// <summary>明确的语义部位、连接、制作来源与实际支持的变化。</summary>
    public FormStructureDescription Structure;

    /// <summary>稳定资源编号。</summary>
    public string Id;

    /// <summary>制作坐标中的前向；缺省沿用早期剑、掌和枝体的正纵轴。</summary>
    public float[] Forward = { 0f, 0f, 1f };

    /// <summary>制作坐标中的上向；与前向共同说明资源姿态，不自动旋转既有施放。</summary>
    public float[] Up = { 0f, 1f, 0f };

    /// <summary>可独立控制的部件。</summary>
    public FormPartDescription[] Parts;

    /// <summary>供附着和动作使用的命名挂点。</summary>
    public FormAnchorDescription[] Anchors;

    /// <summary>器物的完整握持坐标、柄粗与允许尺寸。</summary>
    public FormGripDescription[] Grips = Array.Empty<FormGripDescription>();

    /// <summary>可实际借用的装备类型及尺寸；无装备类型时仅作为生成形体或其他资源使用。</summary>
    public FormHeldDescription Held = new();

    /// <summary>基础动作的调整范围、实际支撑和双方接触阶段。</summary>
    public FormActionDescription[] Actions = Array.Empty<FormActionDescription>();

    /// <summary>随部位控制结构变化的初始作用形状。</summary>
    public FormContactDescription[] Contacts;

    /// <summary>有序控制骨架，刚性形体为空。</summary>
    public FormControlDescription[] Controls;

    /// <summary>完整保留的制作动作。</summary>
    public FormMotionDescription[] Motions;

    /// <summary>此柔性曲面可由共同地表挠曲，刚性资源不声明此项。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ConformToGround;

    /// <summary>原版投影隐藏实际地表以下的表面；三维继续由真实场景地形遮挡。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool HideBelowGround;
}

/// <summary>一个部件的资源和控制枢轴。</summary>
public sealed class FormPartDescription
{
    /// <summary>部件编号。</summary>
    public string Id;

    /// <summary>当前资源的默认显示表面，不承担实际材料规则。</summary>
    public string Surface;

    /// <summary>制作时声明的部位用途，可跨资源选择刃部、握持部等，不从编号猜测。</summary>
    public string[] Roles;

    /// <summary>局部控制枢轴。</summary>
    public float[] Pivot;

    /// <summary>资源包中的网格路径。</summary>
    public string MeshAsset;

    /// <summary>中景和远景共享网格，空值表示该部件保留完整几何。</summary>
    public string[] DetailMeshes = Array.Empty<string>();

    /// <summary>整个刚性部件随动的控制，空值留在模型空间。</summary>
    public string Control;

    /// <summary>顶点是否带四项控制权重。</summary>
    public bool Skin;

    /// <summary>网格是否带局部厚度及泡沫覆盖资料。</summary>
    public bool HasSurfaceData;

    /// <summary>顶点是否带截面中心与逐段成形顺序。</summary>
    public bool HasBuildData;

    /// <summary>是否由本次主体材料选择表面；装饰和握柄可保持自身表面。</summary>
    public bool UseMatterSurface;
}

/// <summary>与部件绑定的命名挂点。</summary>
public sealed class FormAnchorDescription
{
    /// <summary>挂点编号。</summary>
    public string Id;

    /// <summary>所属部件索引。</summary>
    public int Part;

    /// <summary>局部位置。</summary>
    public float[] Position;

    /// <summary>局部方向。</summary>
    public float[] Direction;

    /// <summary>挂点随动控制，空值留在绑定模型空间。</summary>
    public string Control;

    /// <summary>明确表面权重，优先于单控制及所属部件的默认控制。</summary>
    public FormInfluenceDescription[] Influences;

    /// <summary>该挂点实际部位成形后才可附着，零表示没有额外要求。</summary>
    public float BuildAt;
}

/// <summary>绑定部位的初始胶囊作用形状，是否生效由运行过程决定。</summary>
public sealed class FormContactDescription
{
    /// <summary>形状编号。</summary>
    public string Id;

    /// <summary>所属部件索引；负一表示根区域，不要求存在显示网格。</summary>
    public int Part;

    /// <summary>轴线起点。</summary>
    public float[] A;

    /// <summary>轴线终点。</summary>
    public float[] B;

    /// <summary>局部半径。</summary>
    public float Radius;

    /// <summary>以 A 为中心的局部箱体半宽；提供时替代轴线 B 和半径。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float[] HalfExtents;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public FormContactSector Sector;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public FormContactPath Path;

    internal FormContactDescription WithPart(int part)
    {
        var copy = (FormContactDescription)MemberwiseClone();
        copy.Part = part;
        return copy;
    }

    /// <summary>两个端点各自随动的控制，空值继承部件的刚性控制。</summary>
    public string AControl, BControl;

    /// <summary>两个表面端点各自的明确混合权重。</summary>
    public FormInfluenceDescription[] AInfluences, BInfluences;

    /// <summary>成形所需进度范围，空值不限制；区间首端开始出现。</summary>
    public float[] BuildRange;

    /// <summary>轴线沿成形方向时收住前端，横向曲面条带只调整厚度。</summary>
    public bool BuildAlong;
}

/// <summary>已解析端点、控制和半径的纯数值作用形状。</summary>
public readonly struct FormContactBinding
{
    /// <summary>绑定模型空间的端点。</summary>
    public readonly Vector3 A, B;

    /// <summary>两个端点各自已经解析的表面控制。</summary>
    public readonly FormControlBlend ABlend, BBlend;

    /// <summary>模型空间半径。</summary>
    public readonly float Radius;

    /// <summary>本地三个方向的箱体半宽；零值保留原轴与半径。</summary>
    private readonly Vector3 halfExtents;
    private readonly Vector2[] outline;
    private readonly float startsAt;

    internal readonly string Id;
    internal readonly int Part;
    internal readonly float Extent;

    internal bool IsPrism => outline != null;
    internal bool IsFormed(FormPose pose) => (pose?.BuildProgress ?? 1f) >= startsAt;

    /// <summary>已经检查的成形区间，负值表示不限制。</summary>
    public readonly Vector2 BuildRange;

    /// <summary>是否需要把胶囊前端收在当前可见端点之内。</summary>
    private readonly bool buildAlong;

    /// <summary>只在资源边界解析一次。</summary>
    /// <param name="source">已检查形状。</param>
    /// <param name="rig">已检查骨架。</param>
    /// <param name="control">所属刚性部件的控制。</param>
    internal FormContactBinding(FormContactDescription source, FormRig rig, string control,
        Vector2[] footprint = null, float thickness = 0f, float startsAt = 0f)
    {
        Id = source.Id;
        Part = source.Part;
        this.startsAt = startsAt;
        A = FormRig.Vector(source.A, 0, source.Id);
        outline = footprint ?? (source.HalfExtents == null ? null : AbilityPrism.Rectangle);
        if (outline == null)
        {
            B = FormRig.Vector(source.B, 0, source.Id);
            Radius = source.Radius;
            halfExtents = default;
            Extent = 0f;
        }
        else
        {
            B = A;
            Radius = 0f;
            halfExtents = footprint == null ? FormRig.Vector(source.HalfExtents, 0, source.Id) : new Vector3(1f, thickness, 1f);
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                throw new InvalidDataException(source.Id + "：箱体三个半宽都须大于零");
            Extent = footprint == null ? halfExtents.magnitude : Mathf.Sqrt(thickness * thickness + footprint.Max(p => p.sqrMagnitude));
        }
        BuildRange = FormFormation.Range(source.BuildRange, source.Id);
        buildAlong = source.BuildAlong;
        if (buildAlong && (BuildRange.x < 0f || BuildRange.y - BuildRange.x <= FormFormation.TipSpan))
            throw new InvalidDataException(source.Id + "：纵向成形区间必须包含轴线及前端收尖");
        ABlend = new FormControlBlend(source.AInfluences,
            string.IsNullOrEmpty(source.AControl) ? control : source.AControl, rig);
        BBlend = new FormControlBlend(source.BInfluences,
            string.IsNullOrEmpty(source.BControl) ? control : source.BControl, rig);
    }

    /// <summary>根据实际部位控制取得箱体中心与三个半轴；不先转成放大的世界包围盒。</summary>
    private Matrix4x4 PrismMatrix(FormPose pose, Matrix4x4 world, float build = 1f)
    {
        Vector3 origin = ABlend.Point(A, pose);
        var local = Matrix4x4.identity;
        local.SetColumn(0, ABlend.Point(A + Vector3.right * (halfExtents.x * build), pose) - origin);
        local.SetColumn(1, ABlend.Point(A + Vector3.up * (halfExtents.y * build), pose) - origin);
        local.SetColumn(2, ABlend.Point(A + Vector3.forward * (halfExtents.z * build), pose) - origin);
        local.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
        return world * local;
    }

    internal AbilityPrism.Frame PrismFrame(FormPose pose, Matrix4x4 world) => new(outline, PrismMatrix(pose, world));

    /// <summary>共用施放、借用形体及留置材料的作用形状采样入口。</summary>
    internal bool World(FormPose pose, Matrix4x4 world, int node, int shape, int revision, AbilityMatter material,
        float until, out AbilityCapsule result, string matter = null)
    {
        if (!IsFormed(pose)) { result = default; return false; }
        int control = ABlend.RigidControl;
        var sweep = new AbilitySweepPose(control >= 0 && (IsPrism || control == BBlend.RigidControl)
            ? world * pose.Skin[control] : world);
        if (IsPrism)
        {
            float build = BuildRange.x < 0f ? 1f : Mathf.Clamp01(((pose?.BuildProgress ?? 1f) - BuildRange.x) /
                FormFormation.TipSpan);
            if (build <= .000001f) { result = default; return false; }
            Matrix4x4 matrix = PrismMatrix(pose, world, build);
            Vector3 center = matrix.GetColumn(3);
            result = new AbilityCapsule(node, center, center, 0f, material?.Id ?? matter, shape, revision, material,
                until: until, prism: new AbilityPrism(outline, matrix), sweepPose: sweep);
            return true;
        }
        if (!Sample(pose, out var a, out var b, out float radius)) { result = default; return false; }
        Vector3 scale = world.lossyScale;
        result = new AbilityCapsule(node, world.MultiplyPoint3x4(a), world.MultiplyPoint3x4(b),
            radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), material?.Id ?? matter,
            shape, revision, material, until: until, sweepPose: sweep);
        return true;
    }

    /// <summary>显示、施放接触及世界材料共同采用的当前成形形状。</summary>
    /// <param name="pose">同一时刻的数值姿态。</param>
    /// <param name="a">实际轴线起点。</param>
    /// <param name="b">实际轴线终点。</param>
    /// <param name="radius">本时刻实际厚度。</param>
    public bool Sample(FormPose pose, out Vector3 a, out Vector3 b, out float radius)
    {
        a = ABlend.Point(A, pose);
        b = BBlend.Point(B, pose);
        radius = Radius;
        if (BuildRange.x < 0f) return true;
        float progress = pose?.BuildProgress ?? 1f;
        if (progress <= BuildRange.x) return false;
        radius *= Mathf.Clamp01((progress - BuildRange.x) / FormFormation.TipSpan);
        if (buildAlong)
            b = Vector3.Lerp(a, b,
                Mathf.Clamp01((progress - BuildRange.x) / (BuildRange.y - BuildRange.x - FormFormation.TipSpan)));
        if (buildAlong && (progress < BuildRange.y || BuildRange.y >= 1f))
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < .00001f) return false;
            radius = Mathf.Min(radius, length * .5f);
            b -= delta / length * radius;
        }

        return radius > .000001f;
    }
}

/// <summary>一次加载、多个构造共用的形体资源；Unity 对象只在主线程使用。</summary>
public sealed partial class FormResource
{
    /// <summary>本资源所属的完整资源版本。</summary>
    internal readonly FormResourceGeneration Generation;

    /// <summary>本次运行内的资源版本，不写入正式存档。</summary>
    public int Version => whole == null ? Generation.Version : whole.Version;

    /// <summary>保留本版本的范围反馈球，退休后仍能为已有施放建立显示。</summary>
    internal readonly FormResource ContactVolume;

    /// <summary>从同一版本取得显示材质，旧施放不会混用新材质。</summary>
    /// <param name="surface">实际表面。</param>
    /// <param name="native">是否原版投影。</param>
    /// <param name="transparent">实例半透明时不写原版深度，不改动共享实体表面。</param>
    public Material Surface(string surface, bool native, bool transparent = false) =>
        Generation.Surface(surface, native, transparent);

    /// <summary>固定的部位说明。</summary>
    public readonly FormDescription Description;

    /// <summary>加载时解析的共同结构；计算只读取索引和数值。</summary>
    public readonly FormStructure Structure;

    /// <summary>明确声明的纵向伸展能力；其他模型为空。</summary>
    public readonly FormSpan Span;

    /// <summary>资源包含明确的逐段成形顶点资料。</summary>
    public readonly bool Builds;

    /// <summary>实际具有可改变结构的控制，刚性器物和空支点不接受内部形变。</summary>
    public bool Deformable => Rig.Count > 0 && Meshes.Length > 0 && Structure.Kind is not ("rigid" or "anchor");

    /// <summary>完整保留表面坐标和法向的共享网格。</summary>
    public readonly Mesh[] Meshes;

    /// <summary>只用于显示的中远景几何，实际作用始终使用原完整资料。</summary>
    private readonly Mesh[][] detailMeshes;

    /// <summary>按已确定的显示等级取得共享几何，不读取磁盘或改变实际姿态。</summary>
    /// <param name="part">真实部件索引。</param><param name="level">近、中、远分别为0、1、2。</param>
    public Mesh DetailMesh(int part, int level) => level == 0 || detailMeshes[part].Length == 0
        ? Meshes[part]
        : detailMeshes[part][level - 1];

    /// <summary>工作计算使用的不可变骨架资料。</summary>
    public readonly FormRig Rig;

    /// <summary>每个刚性部件的控制，负一为固定。</summary>
    public readonly int[] PartControls;

    /// <summary>已解析的纯数值作用形状。</summary>
    public readonly FormContactBinding[] Contacts;

    /// <summary>已解析名称、方向及控制的挂点。</summary>
    public readonly FormAnchorBinding[] Anchors;

    /// <summary>资源边界已经解析的握持编号，不在取样时查询挂点名称。</summary>
    private readonly Dictionary<string, FormGrip> grips = new(StringComparer.Ordinal);

    /// <summary>取得明确的握持资料；普通挂点不能代替完整握持。</summary>
    /// <param name="id">稳定握持编号。</param>
    public FormGrip Grip(string id) => grips.TryGetValue(id ?? "", out FormGrip grip)
        ? grip
        : throw new InvalidDataException(
            Description.Id + "缺少握持资料：" + id + "；可用握持：" + string.Join("、", grips.Keys));

    /// <summary>在完整挂点建立后解析和复制握持资料，分件也复用同一加载边界。</summary>
    private void LoadGrips()
    {
        if (Description.Grips == null) throw new InvalidDataException(Description.Id + "：缺少握持资料集合");
        foreach (FormGripDescription source in Description.Grips)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Id) || grips.ContainsKey(source.Id))
                throw new InvalidDataException(Description.Id + "：握持编号为空或重复");
            int anchor = Array.FindIndex(Anchors, item => item.Id == source.Anchor);
            if (anchor < 0)
                throw new InvalidDataException(Description.Id + "/" + source.Id + "：握持挂点不存在：" + source.Anchor);
            grips.Add(source.Id, new FormGrip(source, Anchors[anchor], Description.Id + "/" + source.Id));
        }

        FormHeldDescription held = Description.Held;
        if (held == null || held.Groups == null || held.Groups.Any(string.IsNullOrWhiteSpace) ||
            held.Groups.Distinct().Count() != held.Groups.Length || !FormRig.Finite(held.Scale) ||
            (held.Groups.Length > 0 ? held.Scale <= 0f || grips.Count == 0 : held.Scale != 0f))
            throw new InvalidDataException(Description.Id + "：实际装备需要明确类型、正有限尺寸及可用握点");
    }

    /// <summary>把制作前上向转为共用出手前上向，只供需要主动组合的资料生成使用。</summary>
    public readonly Quaternion Facing;

    /// <summary>绑定姿态的包围盒对角长度，供按地图尺寸组合资源，不从文件名猜大小。</summary>
    public readonly float RestSize;

    /// <summary>加载时已经取得的完整绑定边界，刚性物件无需建立空骨架。</summary>
    public readonly Bounds RestBounds;

    /// <summary>资源和已保存的创作输入共用同一前上向检查及坐标换算。</summary>
    /// <param name="forwardValues">制作坐标中的前向。</param>
    /// <param name="upValues">制作坐标中的上向。</param>
    /// <param name="id">用于明确错误的资源编号。</param>
    public static Quaternion ReadFacing(float[] forwardValues, float[] upValues, string id)
    {
        if (forwardValues?.Length != 3 || upValues?.Length != 3 ||
            forwardValues.Concat(upValues).Any(value => !FormRig.Finite(value)))
            throw new InvalidDataException(id + "：制作前向和上向需要三个有限数值");
        var forward = new Vector3(forwardValues[0], forwardValues[1], forwardValues[2]);
        var up = new Vector3(upValues[0], upValues[1], upValues[2]);
        float cross = Vector3.Cross(forward, up).sqrMagnitude;
        if (!FormRig.Finite(forward.sqrMagnitude) || !FormRig.Finite(up.sqrMagnitude) ||
            !FormRig.Finite(cross) || cross < .000001f)
            throw new InvalidDataException(id + "：制作前向和上向不能为零或相互平行");
        return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
    }

    /// <summary>绑定已读取的描述和几何。</summary>
    /// <param name="description">已检查的形体说明。</param>
    /// <param name="meshes">与部件一一对应的网格。</param>
    /// <param name="generation">拥有本次几何和表面的完整资源组。</param>
    internal FormResource(FormDescription description, Mesh[] meshes, FormResourceGeneration generation)
    {
        Generation = generation;
        ContactVolume = generation.ContactVolume;
        Description = description;
        Builds = description.Parts.Any(part => part.HasBuildData);
        HasMatterParts = description.Parts.Any(part => part.UseMatterSurface);
        Facing = ReadFacing(description.Forward, description.Up, description.Id);
        Meshes = meshes;
        Bounds? fixedBounds = null;
        for (int i = 0; i < meshes.Length; i++)
            if (!description.Parts[i].Skin && string.IsNullOrEmpty(description.Parts[i].Control))
            {
                if (fixedBounds.HasValue)
                {
                    Bounds bounds = fixedBounds.Value;
                    bounds.Encapsulate(meshes[i].bounds);
                    fixedBounds = bounds;
                }
                else fixedBounds = meshes[i].bounds;
            }

        Rig = new FormRig(description, fixedBounds);
        PartControls = new int[meshes.Length];
        FormPose rest = Rig.CreatePose();
        if (rest == null) RestSize = fixedBounds?.size.magnitude ?? 0f;
        else
        {
            Rig.Sample(null, 0f, rest);
            RestSize = rest.Bounds.size.magnitude;
        }

        RestBounds = rest?.Bounds ?? fixedBounds.GetValueOrDefault();

        if (description.ConformToGround && (Rig.Count == 0 ||
                                            description.Parts.Any(part =>
                                                !part.Skin && string.IsNullOrEmpty(part.Control))))
            throw new InvalidDataException(description.Id + "：贴地资源需要为所有部件绑定骨架或控制点");
        detailMeshes = new Mesh[meshes.Length][];
        for (int i = 0; i < meshes.Length; i++)
        {
            FormPartDescription part = description.Parts[i];
            PartControls[i] = Rig.Control(part.Control);
            string[] paths = part.DetailMeshes ?? Array.Empty<string>();
            if (paths.Length != 0 && paths.Length != 2)
                throw new InvalidDataException(description.Id + "/" + part.Id + "：显示细节需要同时提供中景和远景网格");
            detailMeshes[i] = paths.Select(Generation.LoadOwned<Mesh>).ToArray();
            foreach (Mesh mesh in new[] { meshes[i] }.Concat(detailMeshes[i]))
            {
                if (mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                    throw new InvalidDataException(description.Id + "/" + part.Id + "：显示细节缺少表面坐标或法线");
                if (part.HasSurfaceData &&
                    mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord3) != 2)
                    throw new InvalidDataException(description.Id + "/" + part.Id + "：缺少局部厚度资料");
                if (part.HasBuildData && (!part.Skin ||
                                          mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute
                                              .TexCoord4) != 4))
                    throw new InvalidDataException(description.Id + "/" + part.Id + "：逐段成形需要骨架与截面中心资料");
                if (part.Skin && (Rig.Count == 0 || !string.IsNullOrEmpty(part.Control) ||
                                  mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord1) !=
                                  4 ||
                                  mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord2) !=
                                  4))
                    throw new InvalidDataException(description.Id + "/" + part.Id + "：蒙皮缺少骨架、权重或混用刚性控制");
            }
        }

        Anchors = new FormAnchorBinding[description.Anchors.Length];
        for (int i = 0; i < Anchors.Length; i++)
            Anchors[i] = new FormAnchorBinding(description.Anchors[i], Rig,
                description.Parts[description.Anchors[i].Part].Control);
        LoadGrips();
        var contacts = new List<FormContactBinding>(description.Contacts.Length);
        foreach (FormContactDescription contact in description.Contacts)
        {
            if (contact.Part < -1 || contact.Part >= meshes.Length || contact.A?.Length != 3 ||
                (contact.Sector == null && contact.Path == null && (contact.HalfExtents == null
                    ? contact.B?.Length != 3 || !FormRig.Finite(contact.Radius) || contact.Radius <= 0f
                    : contact.HalfExtents.Length != 3 || contact.B != null || contact.Radius != 0f ||
                      contact.BControl != null || contact.BInfluences != null || contact.BuildAlong)))
                throw new InvalidDataException("作用部位或形状无效：" + description.Id + "/" + contact.Id);
            FormContactRegions.Add(contacts, contact, Rig, contact.Part < 0 ? null : description.Parts[contact.Part].Control);
        }
        Contacts = contacts.ToArray();

        Structure = new FormStructure(description);
        if (Structure.Supports("span")) Span = new FormSpan(this);
        LoadActions();
    }
}

/// <summary>读取共享形体及表面资源，不在世界存档保存资源对象。</summary>
internal sealed partial class FormResourceGeneration
{
    /// <summary>只在本次运行内递增的版本。</summary>
    internal readonly int Version;

    /// <summary>一组资源只保存弱引用，退休后不阻止施放和预览正常回收。</summary>
    private readonly List<WeakReference<FormResource>> references = new();

    /// <summary>此组实际拥有的原生资源，按实例去重。</summary>
    private readonly Dictionary<int, UnityEngine.Object> owned = new();

    /// <summary>同组材质共享的着色器及纹理。</summary>
    private Shader shader;

    /// <summary>透明交叉曲面共用的合成表面。</summary>
    private Shader transparencyShader;

    /// <summary>本版本固定的透明合成材质。</summary>
    private Material transparency;

    /// <summary>原版精灵的共同深度着色器。</summary>
    private Shader nativeSpriteShader;

    /// <summary>原版专用着色器生成画面后的共同表面。</summary>
    private Material nativeStamp;

    /// <summary>从原材质派生的精灵表面，与本资源版本一起释放。</summary>
    private readonly Dictionary<Material, Material> nativeSprites = new();

    /// <summary>同组的表面纹理。</summary>
    private Texture2D texture;

    /// <summary>建立一组独立版本，首次读取时才加载资源包。</summary>
    /// <param name="version">运行内唯一版本。</param>
    internal FormResourceGeneration(int version)
    {
        Version = version;
    }

    /// <summary>是否仍有运行、预览或发布帧持有本组的实际形体。</summary>
    internal bool IsReferenced => references.Any(reference => reference.TryGetTarget(out _));

    /// <summary>提前保留旧组可能继续使用的表面资料，不在卸包后再读旧包。</summary>
    private void EnsureBaseAssets()
    {
        if (shader != null) return;
        shader = LoadOwned<Shader>("Assets/Cultiway/Shaders/FormSurface.shader");
        transparencyShader = LoadOwned<Shader>("Assets/Cultiway/Shaders/FormTransparency.shader");
        nativeSpriteShader = LoadOwned<Shader>("Assets/Cultiway/Shaders/NativeObject.shader");
        texture = LoadOwned<Texture2D>("Assets/Cultiway/Forms/surface_detail.asset");
    }

    /// <summary>在加载边界核对并登记实际原生对象。</summary>
    /// <param name="path">资源包内路径。</param>
    internal T LoadOwned<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("形体说明缺少资源包内路径");
        T value = Bundle.LoadAsset<T>(path);
        if (value == null) throw new InvalidDataException("形体资源包缺少对象：" + path);
        Own(value);
        return value;
    }

    /// <summary>同组去重后登记真实所有权。</summary>
    /// <param name="value">此组实际使用的原生对象。</param>
    private void Own(UnityEngine.Object value)
    {
        int id = value.GetInstanceID();
        if (owned.ContainsKey(id)) return;
        owned.Add(id, value);
        FormResources.Retain(value);
    }

    /// <summary>本版本已经读取并固定的形体编号。</summary>
    internal IReadOnlyList<string> Ids => forms.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    /// <summary>完整读取后固定资源集合，后续不再从磁盘补读这一版本。</summary>
    internal void Complete()
    {
        complete = true;
    }

    /// <summary>Unity不允许重复打开同名包；仅关闭包文件，已经加载的对象仍归本组拥有。</summary>
    internal void CloseBundle()
    {
        if (bundle != null)
        {
            bundle.Unload(false);
            bundle = null;
        }
    }

    /// <summary>停止接受新加载，保留已经加载且仍被施放使用的对象。</summary>
    internal void Retire()
    {
        forms.Clear();
        Steam = null;
        ContactVolume = null;
        CloseBundle();
    }

    /// <summary>失去全部使用者或退出时，主线程释放此组原生所有权。</summary>
    internal void Dispose()
    {
        Retire();
        foreach (UnityEngine.Object value in owned.Values) FormResources.Release(value);
        owned.Clear();
        materials.Clear();
        references.Clear();
        shader = null;
        transparencyShader = null;
        transparency = null;
        texture = null;
        FormResources.NotifyReleased(Version);
    }

    /// <summary>共用形体缓存。</summary>
    private readonly Dictionary<string, FormResource> forms = new(StringComparer.Ordinal);

    /// <summary>原版与 Worldfall 使用各自的材质实例。</summary>
    private readonly Dictionary<(string, bool, bool), Material> materials = new();

    /// <summary>同一资源版本持有透明合成入口，不创建逐实例材质。</summary>
    internal Material Transparency()
    {
        if (transparency != null) return transparency;
        EnsureBaseAssets();
        transparency = new Material(transparencyShader)
            { name = "Cultiway.TransparentResolve", hideFlags = HideFlags.DontSave };
        Own(transparency);
        return transparency;
    }

    /// <summary>按运行平台加载一次的形体资源包。</summary>
    private AssetBundle bundle;

    /// <summary>已经完整读取，此后不能混入磁盘上的其他版本。</summary>
    private bool complete;

    /// <summary>读取一个已构建形体，资源边界出错时给出具体路径。</summary>
    /// <param name="id">形体资源文件名，不带路径和扩展名。</param>
    internal FormResource Load(string id)
    {
        if (forms.TryGetValue(id, out FormResource found)) return found;
        if (complete) throw new InvalidDataException("当前资源版本没有形体：" + id + "；可用形体：" + string.Join("、", Ids));
        if (id != Path.GetFileName(id)) throw new ArgumentException("形体编号不能包含路径", nameof(id));
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Forms", id + ".json");
        FormDescription data = JsonConvert.DeserializeObject<FormDescription>(File.ReadAllText(path));
        if (data?.Version != FormDescription.FormatVersion || data.Id != id || data.Parts == null ||
            data.Anchors == null || data.Contacts == null)
            throw new InvalidDataException("不支持或不完整的形体说明：" + path);
        EnsureBaseAssets();
        var meshes = new Mesh[data.Parts.Length];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < meshes.Length; i++)
        {
            FormPartDescription part = data.Parts[i];
            if (!ids.Add(part.Id) || part.Pivot?.Length != 3)
                throw new InvalidDataException("重复部位或缺少三维枢轴：" + path + "/" + part.Id);
            meshes[i] = LoadOwned<Mesh>(part.MeshAsset);
            if (meshes[i] == null || meshes[i].uv.Length != meshes[i].vertexCount)
                throw new InvalidDataException("网格缺失或表面坐标不完整：" + part.MeshAsset);
        }

        foreach (FormAnchorDescription anchor in data.Anchors)
            if (anchor.Part < 0 || anchor.Part >= meshes.Length || anchor.Position?.Length != 3 ||
                anchor.Direction?.Length != 3)
                throw new InvalidDataException("挂点部位或位置无效：" + path + "/" + anchor.Id);
        found = new FormResource(data, meshes, this);
        forms.Add(id, found);
        references.Add(new WeakReference<FormResource>(found));
        return found;
    }

    /// <summary>读取指定表面在对应视角下的共享材质；颜色覆盖留给每次显示。</summary>
    /// <param name="surface">实际已经提供的显示表面。</param>
    /// <param name="native">是否用于原版投影。</param>
    /// <param name="transparent">实例半透明时关闭实体深度写入。</param>
    internal Material Surface(string surface, bool native, bool transparent = false)
    {
        if (materials.TryGetValue((surface, native, transparent), out Material found)) return found;
        (float kind, Color color, float roughness) = surface switch
        {
            "metal" => (0f, new Color(.53f, .64f, .70f), .23f),
            "restoration_jade" => (2f, new Color(.22f, .76f, .45f), .38f),
            "living_tissue" => (26f, new Color(.97f, .48f, .31f, .74f), .36f),
            "living_sap" => (26f, new Color(.91f, .93f, .47f, .93f), .26f),
            "living_bark" => (1f, new Color(.51f, .28f, .095f), .56f),
            "living_leaf" => (2f, new Color(.31f, .66f, .29f, .82f), .46f),
            "thunder_core" => (3f, new Color(.74f, .91f, 1f), .4f),
            "thunder_body" => (3f, new Color(.34f, .42f, .91f), .24f),
            "thunder_corona" => (15f, new Color(.34f, .23f, 1f, .26f), .4f),
            "thunder_arc" => (15f, new Color(.71f, .82f, 1f, .98f), .08f),
            "thunder_arc_fork" => (15f, new Color(.48f, .62f, 1f, .83f), .27f),
            "spirit_pearl" => (2f, new Color(.57f, .88f, .94f), .25f),
            "solar_light" => (6f, new Color(1f, .84f, .39f), .32f),
            "solar_gold" => (6f, new Color(1f, .43f, .07f), .45f),
            "spirit_beam_edge" => (15f, new Color(.26f, .74f, .93f, .59f), .42f),
            "spirit_beam_core" => (15f, new Color(.69f, .96f, 1f, .92f), .2f),
            "solar_beam_edge" => (26f, new Color(1f, .65f, .19f, .53f), .33f),
            "solar_beam_flank" => (26f, new Color(1f, .28f, .049f, .39f), .54f),
            "solar_beam_core" => (26f, new Color(1f, .86f, .47f, .92f), .23f),
            "solar_kernel" => (26f, new Color(1f, .66f, .17f, .97f), .10f),
            "blade_steel" => (0f, new Color(.38f, .47f, .57f), .25f),
            "silver" => (0f, new Color(.83f, .90f, .94f), .14f),
            "five_shale" => (2f, new Color(.44f, .30f, .22f), .57f),
            "five_silt" => (2f, new Color(.72f, .56f, .38f), .49f),
            "battle_copper" => (2f, new Color(.91f, .32f, .16f), .34f),
            "guard_enamel" => (2f, new Color(.29f, .58f, .81f), .31f),
            "haste_gold" => (2f, new Color(.94f, .73f, .24f), .33f),
            "purification_pearl" => (2f, new Color(.75f, .94f, .84f), .27f),
            "bronze" => (0f, new Color(.52f, .31f, .12f), .30f),
            "spirit_core" => (14f, new Color(.55f, .85f, .94f, .92f), .24f),
            "yin_depth" => (14f, new Color(.055f, .075f, .16f, .96f), .32f),
            "yin_moon" => (14f, new Color(.42f, .48f, .72f, .78f), .24f),
            "gold" => (0f, new Color(.96f, .68f, .16f), .22f),
            "lightning_dense" => (14f, new Color(.39f, .54f, 1f, .94f), .22f),
            "poison_liquid" => (10f, new Color(.17f, .36f, .035f, .9f), .23f),
            "poison_film" => (23f, new Color(.32f, .42f, .052f, .94f), .24f),
            "poison_etch" => (24f, new Color(.53f, .73f, .15f, .93f), .34f),
            "poison_vapor" => (11f, new Color(.31f, .53f, .07f, .48f), .9f),
            "poison_core" => (11f, new Color(.13f, .25f, .025f, .55f), .9f),
            "entropy_obsidian" => (2f, new Color(.22f, .13f, .34f), .52f),
            "entropy_section" => (2f, new Color(.63f, .24f, .54f), .39f),
            "entropy_veil" => (12f, new Color(.30f, .10f, .46f, .55f), .9f),
            "entropy_depth" => (12f, new Color(.045f, .02f, .07f, .78f), .9f),
            "signal_ceramic" => (2f, new Color(.16f, .52f, .55f), .51f),
            "signal_gold" => (2f, new Color(.96f, .73f, .32f), .34f),
            "soul_seal_rim" => (2f, new Color(.48f, .45f, .53f), .41f),
            "soul_seal_glass" => (14f, new Color(.88f, .87f, .84f), .28f),
            "time_stamp_slate" => (16f, new Color(.24f, .27f, .34f), .68f),
            "time_stamp_brass" => (2f, new Color(.80f, .58f, .28f), .6f),
            "space_destination" => (27f, Color.white, .87f),
            "space_gate_stone" => (16f, new Color(.23f, .31f, .36f), .85f),
            "space_gate_inlay" => (2f, new Color(.56f, .49f, .34f), .66f),
            "rift_core" => (8f, new Color(.055f, .018f, .095f), .9f),
            "rift_lip" => (9f, new Color(.37f, .10f, .61f), .55f),
            "mirror" => (7f, new Color(.72f, .81f, .84f), .045f),
            "molten_iron" => (6f, new Color(1f, .31f, .035f), .28f),
            "aegis_jade" => (14f, new Color(.38f, .77f, .73f, .44f), .3f),
            "aegis_lightning" => (14f, new Color(.52f, .64f, .98f, .51f), .27f),
            "aegis_solar" => (26f, new Color(1f, .71f, .19f, .48f), .35f),
            "wind" => (13f, new Color(.55f, .81f, .86f, .57f), .32f),
            "flame" => (6f, new Color(1f, .31f, .035f), .8f),
            "flame_body" => (20f, new Color(1f, .38f, .065f, .85f), .76f),
            "wood" => (1f, new Color(.36f, .21f, .10f), .8f),
            "wood_leaf" => (2f, new Color(.31f, .53f, .12f), .88f),
            "leather" => (2f, new Color(.16f, .08f, .05f), .85f),
            "stone" => (2f, new Color(.34f, .31f, .27f), .96f),
            "stone_strata" => (16f, new Color(.46f, .39f, .30f), .96f),
            "stone_ridge" => (21f, new Color(.48f, .41f, .31f), .96f),
            "wind_open" => (17f, new Color(.72f, .88f, .84f, .76f), .55f),
            "wind_pressure" => (17f, new Color(.48f, .70f, .69f, .78f), .68f),
            "wind_shear" => (22f, new Color(.20f, .16f, .23f, .92f), .62f),
            "wind_fold_dark" => (22f, new Color(.39f, .30f, .42f, .74f), .76f),
            "yin_basin" => (25f, new Color(.22f, .22f, .39f, .94f), .54f),
            "yin_wrap" => (18f, new Color(.24f, .20f, .44f, .96f), .62f),
            "yin_flow" => (18f, new Color(.36f, .32f, .56f, .98f), .62f),
            "yin_shadow" => (18f, new Color(.25f, .21f, .37f, .92f), .62f),
            "polarity_ink" => (18f, new Color(.12f, .13f, .25f, .94f), .43f),
            "polarity_depth" => (18f, new Color(.46f, .43f, .65f, .83f), .32f),
            "polarity_pearl" => (26f, new Color(.97f, .89f, .68f, .82f), .34f),
            "polarity_gold" => (26f, new Color(1f, .58f, .20f, .89f), .38f),
            "polarity_core" => (26f, new Color(1f, .73f, .30f, .97f), .28f),
            "meteor_heat" => (19f, new Color(.96f, .29f, .024f, .64f), .62f),
            "meteor_crown" => (19f, new Color(1f, .43f, .04f, .91f), .54f),
            "skin" => (2f, new Color(.86f, .65f, .48f), .65f),
            "horn" => (2f, new Color(.56f, .73f, .79f), .34f),
            "ivory" => (2f, new Color(.78f, .73f, .55f), .4f),
            "feather" => (2f, new Color(.66f, .75f, .82f), .9f),
            "membrane" => (2f, new Color(.30f, .16f, .27f), .8f),
            "fur" => (2f, new Color(.72f, .51f, .29f), .9f),
            "shell" => (2f, new Color(.13f, .23f, .22f), .5f),
            "foam" => (5f, new Color(.84f, .96f, .97f, .93f), .95f),
            "water" => (3f, new Color(.06f, .38f, .57f, .84f), .14f),
            "ice" => (4f, new Color(.48f, .79f, .88f, .73f), .16f),
            _ => throw new InvalidDataException("形体未提供显示表面：" + surface)
        };
        EnsureBaseAssets();
        found = new Material(shader)
        {
            name = "Cultiway_Form_" + surface + (native ? "_Native" : "_Worldfall") +
                   (transparent ? "_Transparent" : ""),
            hideFlags = HideFlags.DontSave, enableInstancing = true
        };
        Own(found);
        found.SetTexture("_MainTex", texture);
        found.SetColor("_Color", color);
        found.SetFloat("_Kind", kind);
        found.SetFloat("_Roughness", roughness);
        found.SetFloat("_ZWrite", !transparent && (kind < 3f || kind >= 6f && kind < 10f || kind == 16f || kind == 27f) ? 1f : 0f);
        found.SetShaderPassEnabled(native ? "CultiwayWorldfall" : "Always", false);
        found.SetShaderPassEnabled(native ? "CultiwayWorldfallTransparent" : "CultiwayNativeTransparent", false);
        if (native) found.SetShaderPassEnabled("CultiwayWorldfallSolid", false);
        materials.Add((surface, native, transparent), found);
        return found;
    }

    /// <summary>保留原版贴图参数和颜色，换用共同深度，不改原版共享材质。</summary>
    /// <param name="original">原版当前表面。</param>
    internal Material NativeSprite(Material original)
    {
        if (nativeSprites.TryGetValue(original, out Material material)) return material;
        EnsureBaseAssets();
        material = new Material(nativeSpriteShader)
            { name = "Cultiway.Native." + original.name, hideFlags = HideFlags.DontSave };
        material.CopyPropertiesFromMaterial(original);
        Own(material);
        nativeSprites.Add(original, material);
        return material;
    }

    /// <summary>图集中的原画面已经包含颜色，平面只负责共同深度与透明度。</summary>
    internal Material NativeStamp()
    {
        if (nativeStamp != null) return nativeStamp;
        EnsureBaseAssets();
        nativeStamp = new Material(nativeSpriteShader)
            { name = "Cultiway.NativeOriginal", hideFlags = HideFlags.DontSave };
        nativeStamp.SetFloat("_NativeStamped", 1);
        Own(nativeStamp);
        return nativeStamp;
    }

    /// <summary>加载当前平台的资源包，与离线构建的名字保持一致。</summary>
    private AssetBundle Bundle
    {
        get
        {
            if (bundle != null) return bundle;
            string platform = Application.platform switch
            {
                RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => "linux",
                RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "windows",
                RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => "osx",
                _ => throw new PlatformNotSupportedException("当前平台没有形体资源包")
            };
            string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath,
                "Content/AssetBundles/cultiway_forms_" + platform);
            bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidDataException("形体资源包无法加载：" + path);
            return bundle;
        }
    }
}
