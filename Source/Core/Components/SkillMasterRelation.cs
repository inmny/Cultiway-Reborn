using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct SkillMasterRelation : ILinkRelation
{
    public Entity SkillContainer;

    public Entity GetRelationKey()
    {
        return SkillContainer;
    }
}