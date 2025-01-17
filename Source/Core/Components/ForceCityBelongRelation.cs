using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct ForceCityBelongRelation : IForceRelation
{
    public Entity GetRelationKey()
    {
        return ForceEntity;
    }

    public Entity ForceEntity { get; set; }
}