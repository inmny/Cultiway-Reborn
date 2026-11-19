using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>显示肢段的实际归属，供不同身体比例连接真实肩点；不改变接触位置。</summary>
public enum BodyVisualSegment
{
    /// <summary>普通形体，不进行身体适配。</summary>
    None,
    /// <summary>右侧上臂。</summary>
    UpperRightArm,
    /// <summary>右侧前臂。</summary>
    LowerRightArm,
    /// <summary>左侧上臂。</summary>
    UpperLeftArm,
    /// <summary>左侧前臂。</summary>
    LowerLeftArm,
    /// <summary>右手，保留共同腕点和实际掌面。</summary>
    RightHand,
    /// <summary>左手，保留共同腕点和实际掌面。</summary>
    LeftHand
}

/// <summary>一次已经完成的形体显示状态，两个视角只读取，不负责推进动作。</summary>
public readonly struct FormVisualInstance
{
    /// <summary>共同绝对高度使用的地表；空值保留旧来源的相对高度约定。</summary>
    public readonly AbilityHeightField Terrain;
    /// <summary>共用形体。</summary>
    public readonly FormResource Resource;
    /// <summary>同一完成姿态的成形进度，绘制不自行推进。</summary>
    public readonly float BuildProgress;
    /// <summary>地图位置、朝向和尺寸。</summary>
    public readonly Matrix4x4 Transform;
    /// <summary>各部件当前控制变换；为空时均使用单位变换。</summary>
    public readonly Matrix4x4[] Parts;
    /// <summary>明确覆盖显示表面；空值使用部件默认表面。</summary>
    public readonly string Surface;
    /// <summary>本次部件的实际衣物或器物色，不改动共享材质。</summary>
    public readonly Color? Tint;
    /// <summary>连在同一身体上的部件共用地面承托点。</summary>
    public readonly Vector2 GroundAnchor;
    /// <summary>实际身体编号，三维适配据此处理游泳、跳起和桥面。</summary>
    public readonly long GroundActorId;
    /// <summary>完成姿态的固定32项蒙皮矩阵，空值为刚性资源。</summary>
    public readonly Matrix4x4[] Skin;
    /// <summary>完成姿态的模型空间保守边界。</summary>
    public readonly Bounds? PoseBounds;
    /// <summary>实际材料选择的主体表面。</summary>
    public readonly string MatterSurface;
    /// <summary>无材料槽的部件是否允许更换主体表面。</summary>
    private readonly bool followMatter;
    /// <summary>只影响显示的染色倍率；透明通道保留动作淡去后的可见比例。</summary>
    public readonly Color? Dye;
    /// <summary>与此完成姿态一同发布的实际材料及物态。</summary>
    public readonly AbilityMatter Matter;
    /// <summary>实际浅层流散曲面，空值使用资源原形。</summary>
    public readonly FormLayerFrame Layer;
    /// <summary>确认后发布的自有形体，由发布方回收。</summary>
    internal readonly Mesh Geometry;
    /// <summary>动态形体不使用原资源的模型细节和蒙皮。</summary>
    internal bool DynamicGeometry => Geometry != null || Layer != null;
    /// <summary>相连肢段的身体语义，独立形体和器物不使用。</summary>
    public readonly BodyVisualSegment BodySegment;
    /// <summary>当前表面位置及已确认的短暂强度，默认不改变材质。</summary>
    public readonly FormSurfaceFeedback Feedback;

    /// <summary>记录已经确定的姿态，发布后不修改部件数组。</summary>
    /// <param name="resource">共享资源。</param>
    /// <param name="transform">地图空间变换。</param>
    /// <param name="parts">与资源部件对应的控制变换。</param>
    /// <param name="surface">显示表面覆盖。</param>
    /// <param name="tint">本次表面色。</param>
    /// <param name="groundAnchor">共同承托位置。</param>
    /// <param name="groundActorId">借用的真实身体编号。</param>
    /// <param name="skin">发布后不再修改的共同蒙皮姿态。</param>
    /// <param name="poseBounds">同一姿态的保守边界。</param>
    /// <param name="matterSurface">实际材料的主体表面。</param>
    /// <param name="dye">仅显示的染色倍率。</param>
    /// <param name="matter">共同实际材料。</param>
    /// <param name="terrain">绝对高度对应的只读地表。</param>
    /// <param name="layer">已经完成推进的动态浅层曲面。</param>
    /// <param name="bodySegment">需要按实际身体比例连接的肢段。</param>
    /// <param name="buildProgress">与同一姿态确认的成形进度。</param>
    /// <param name="followMatter">是否明确要求主体跟随材料。</param>
    /// <param name="feedback">同一姿态的局部接触反馈。</param>
    public FormVisualInstance(FormResource resource, Matrix4x4 transform, Matrix4x4[] parts = null, string surface = null,
        Color? tint = null, Vector2? groundAnchor = null, long groundActorId = 0, Matrix4x4[] skin = null, Bounds? poseBounds = null,
        string matterSurface = null, Color? dye = null, AbilityMatter matter = null, AbilityHeightField terrain = null, FormLayerFrame layer = null,
        BodyVisualSegment bodySegment = BodyVisualSegment.None, float buildProgress = 1f, FormSurfaceFeedback feedback = default, bool followMatter = false)
    {
        Resource = resource; Transform = transform; Parts = parts; Surface = surface;
        BuildProgress = buildProgress;
        Tint = tint; GroundAnchor = groundAnchor ?? (Vector2)(Vector3)transform.GetColumn(3); GroundActorId = groundActorId;
        Skin = skin; PoseBounds = poseBounds;
        MatterSurface = matterSurface; this.followMatter = followMatter; Dye = dye; Matter = matter; Terrain = terrain; Layer = layer;
        BodySegment = bodySegment; Feedback = feedback; Geometry = null;
    }

    /// <summary>发布世界绝对坐标中的短暂自有形体。</summary>
    internal FormVisualInstance(FormResource resource, Mesh geometry, Color tint, AbilityHeightField terrain)
        : this(resource, geometry, Matrix4x4.identity, tint, terrain) { }

    /// <summary>让短暂自有形体复用已经采集的模型，逐帧只改变显示姿态。</summary>
    internal FormVisualInstance(FormResource resource, Mesh geometry, Matrix4x4 transform, Color tint, AbilityHeightField terrain,
        string surface = null)
        : this(resource, transform, surface: surface, tint: tint, terrain: terrain)
    {
        Geometry = geometry;
    }

    /// <summary>明确覆盖优先，其次是主体材料，握柄、眼部及泡沫保留资源自身表面。</summary>
    /// <param name="part">发布资源中的部件。</param>
    public string SurfaceFor(int part) => Resource.SelectPartSurface(part, Surface, MatterSurface, followMatter);

    /// <summary>取得用于模型、挂点和作用形状的相同部位变换。</summary>
    /// <param name="part">部件索引。</param>
    public Matrix4x4 PartTransform(int part) => Layer != null ? Matrix4x4.identity : Transform * (Parts == null ? Matrix4x4.identity : Parts[part]) *
        (Skin != null && Resource.PartControls[part] >= 0 ? Skin[Resource.PartControls[part]] : Matrix4x4.identity);

    /// <summary>动态形体只提供一个共用部件。</summary>
    public int PartCount => DynamicGeometry ? 1 : Resource.Meshes.Length;
    /// <summary>两端显示共用同一份已发布几何。</summary>
    /// <param name="part">实际部件。</param>
    /// <param name="level">已经按实际视点确定的显示等级。</param>
    internal Mesh MeshFor(int part, int level = 0) => Geometry ?? (Layer == null ? Resource.DetailMesh(part, level) : FormLayerMeshes.Get(Layer));
}

/// <summary>保存最后一次完整发布的形体状态，不持有动作时钟或游戏修改入口。</summary>
public static class FormVisualScene
{
    /// <summary>各实际生产者的完整状态，避免能力更新覆盖开发探针或其他来源。</summary>
    private static readonly Dictionary<string, FormVisualInstance[]> sources = new(StringComparer.Ordinal);
    /// <summary>本轮可供两个显示方式读取的固定状态。</summary>
    public static IReadOnlyList<FormVisualInstance> Instances { get; private set; } = Array.Empty<FormVisualInstance>();

    /// <summary>发布完整状态，调用方之后不再修改数组和部件变换。</summary>
    /// <param name="instances">已经完成更新的全部形体。</param>
    public static void Publish(FormVisualInstance[] instances) => Publish("probe", instances);

    /// <summary>替换一个来源的整份状态，各来源不直接修改其他施放的状态。</summary>
    /// <param name="source">稳定的生产者编号。</param>
    /// <param name="instances">已经完成更新的形体。</param>
    public static void Publish(string source, FormVisualInstance[] instances)
    {
        if (instances.Length == 0)
        {
            if (!sources.Remove(source)) return;
        }
        else sources[source] = instances;
        var combined = new List<FormVisualInstance>();
        foreach (FormVisualInstance[] state in sources.Values) combined.AddRange(state);
        Instances = combined.ToArray();
    }

    /// <summary>切换世界时清理显示引用，共用资源继续复用。</summary>
    public static void Clear()
    {
        sources.Clear(); Instances = Array.Empty<FormVisualInstance>();
    }
}
