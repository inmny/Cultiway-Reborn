using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 角色实体指向已装备法器实体的关系。持有状态仍由 InventoryRelation 单独表示。
/// </summary>
public struct EquippedArtifactRelation : ILinkRelation
{
    /// <summary>
    /// 该关系指向的法器实体，同时作为关系键。
    /// </summary>
    public Entity artifact;

    /// <summary>
    /// 驾驭者为该法器指定的调度模式。
    /// </summary>
    public ArtifactEquipMode mode;

    /// <summary>
    /// 调度器最近一次计算出的实际控制状态。
    /// </summary>
    public ArtifactControlState state;

    /// <summary>
    /// 人工指定的调度优先级；数值越大，越优先被选择和运转。
    /// </summary>
    public int priority;

    /// <summary>
    /// 是否锁定装备；锁定后自动装备选择不会移除该法器。
    /// </summary>
    public bool locked;

    /// <summary>
    /// 返回法器实体作为装备关系的唯一键。
    /// </summary>
    public Entity GetRelationKey()
    {
        return artifact;
    }
}
