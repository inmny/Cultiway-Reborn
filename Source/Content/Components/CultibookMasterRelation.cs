using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct CultibookMasterRelation : ILinkRelation
{
    public Entity Cultibook;
    public float MasterValue;
    public Entity GetRelationKey()
    {
        return Cultibook;
    }
}