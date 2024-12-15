using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct InventoryRelation : ILinkRelation
{
    public Entity item;

    public Entity GetRelationKey()
    {
        return item;
    }
}