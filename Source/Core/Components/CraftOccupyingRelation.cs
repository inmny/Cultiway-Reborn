using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct CraftOccupyingRelation : ILinkRelation
{
    public Entity item;

    public Entity GetRelationKey()
    {
        return item;
    }
}