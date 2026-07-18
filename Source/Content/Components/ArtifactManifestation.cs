using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>
/// 标记法器本体已经显化在世界中。位置和朝向分别存放在通用的 Position 与 Rotation 组件中。
/// </summary>
public struct ArtifactManifestation : IComponent
{
    /// <summary>
    /// 当前装备调度状态，供表现和能力判断使用。
    /// </summary>
    public ArtifactControlState control_state;

    /// <summary>
    /// 世界贴图最长边应占据的世界尺寸。
    /// </summary>
    public float world_size;

    /// <summary>
    /// 是否水平翻转世界贴图。
    /// </summary>
    public bool flip_x;

    /// <summary>
    /// 当前法器是否应在正常世界视图中显示。
    /// </summary>
    public bool visible;

    /// <summary>
    /// 是否使用施展能力时的完整尺寸、视角和像素细节。
    /// </summary>
    public bool active_visual;
}

/// <summary>
/// 法器显化后的空间本体参数，为后续碰撞、选取、拦截和破坏提供统一入口。
/// </summary>
public struct ArtifactBody : IComponent
{
    /// <summary>用于近似圆形选取和普通碰撞的半径。</summary>
    public float radius;

    /// <summary>本体局部左右方向的最大碰撞距离。</summary>
    public float lateral_extent;

    /// <summary>从本体原点沿局部前向延伸的碰撞距离。</summary>
    public float forward_extent;

    /// <summary>从本体原点沿局部后向延伸的碰撞距离。</summary>
    public float backward_extent;

    /// <summary>透明排序使用的局部纵向支点偏移。</summary>
    public float sort_pivot_y;

    public bool targetable;
    public bool collidable;
}

/// <summary>由外观 Instance 计算并缓存到实体上的模型空间边界。</summary>
public struct ArtifactBodyGeometry : IComponent
{
    public Vector3 local_min;
    public Vector3 local_max;
}

/// <summary>
/// 标记法器的位置正由能力或部署系统控制，默认装备跟随系统不应覆盖其运动。
/// </summary>
public struct ArtifactIndependentMotion : IComponent
{
}

/// <summary>
/// 法器本体上用于对齐世界作用点的空间锚点。
/// </summary>
public enum ArtifactBodyAnchorKind
{
    /// <summary>法器本体的 Position。</summary>
    Center,

    /// <summary>世界贴图局部 +Y 方向上的前端尖点。</summary>
    ForwardTip,
}

/// <summary>
/// 对法器组合模型中一个空间锚点的运行时引用。slot/anchor 缺失时退回旧的几何锚点，兼容已有数据。
/// </summary>
public readonly struct ArtifactBodyAnchorRef
{
    public readonly ArtifactBodyAnchorKind fallback;
    public readonly string slot;
    public readonly string anchor;

    public bool UsesAppearanceAnchor => !string.IsNullOrEmpty(slot) && !string.IsNullOrEmpty(anchor);

    public ArtifactBodyAnchorRef(
        ArtifactBodyAnchorKind fallback,
        string slot = null,
        string anchor = null)
    {
        this.fallback = fallback;
        this.slot = slot;
        this.anchor = anchor;
    }

    public static ArtifactBodyAnchorRef Center => new(ArtifactBodyAnchorKind.Center);
    public static ArtifactBodyAnchorRef ForwardTip => new(ArtifactBodyAnchorKind.ForwardTip);

    public static ArtifactBodyAnchorRef Appearance(
        string slot,
        string anchor,
        ArtifactBodyAnchorKind fallback = ArtifactBodyAnchorKind.Center)
    {
        return new ArtifactBodyAnchorRef(fallback, slot, anchor);
    }
}

/// <summary>
/// 法器本体脱离驾驭者并作为持续场域驻留在世界中的状态。
/// 部署期间位置由能力持有，结束后移除此组件即可恢复装备跟随。
/// </summary>
public struct ArtifactDeployment : IComponent
{
    /// <summary>当前部署法器的驾驭者实体。</summary>
    public Entity controller;

    /// <summary>建立本次部署的能力实例 ID。</summary>
    public string ability_instance_id;

    /// <summary>部署开始的世界时间。</summary>
    public double started_at;

    /// <summary>部署自动结束的世界时间；0 表示不由时长结束。</summary>
    public double expires_at;

    /// <summary>能力在世界中的权威作用原点。</summary>
    public Vector3 origin;

    /// <summary>部署时与作用原点重合的法器本体锚点。</summary>
    public ArtifactBodyAnchorKind body_anchor;

    /// <summary>组合外观中的模块槽位；为空时沿用 body_anchor。</summary>
    public string body_anchor_slot;

    /// <summary>所选 variant 上的锚点 key；为空时沿用 body_anchor。</summary>
    public string body_anchor_key;

    public ArtifactBodyAnchorRef ResolveBodyAnchor()
    {
        return new ArtifactBodyAnchorRef(body_anchor, body_anchor_slot, body_anchor_key);
    }
}
