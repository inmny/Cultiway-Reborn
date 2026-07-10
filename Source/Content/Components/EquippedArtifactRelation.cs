using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 角色实体指向已装备法器实体的关系。持有状态仍由 InventoryRelation 单独表示。
/// </summary>
public struct EquippedArtifactRelation : ILinkRelation
{
    public Entity artifact;

    public Entity GetRelationKey()
    {
        return artifact;
    }
}
