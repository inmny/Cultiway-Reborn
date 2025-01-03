using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct StatusRelation: ILinkRelation
{
    public Entity status;

    public Entity GetRelationKey()
    {
        return status;
    }
}