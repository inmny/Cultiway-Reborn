using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public enum MagicSpellKnowledgeSource
{
    MagicWeb,
    Scroll,
    SelfCreated
}

/// <summary>
/// 记录魔法师对法术的知识语义。SkillMasterRelation 仍单独负责容器生命周期。
/// </summary>
public struct MagicSpellKnowledgeRelation : ILinkRelation
{
    public Entity SkillContainer;
    public double LearnedWorldTime;
    public MagicSpellKnowledgeSource Source;

    public Entity GetRelationKey()
    {
        return SkillContainer;
    }
}

/// <summary>
/// 魔法师当前从魔网研究法术的运行时状态。
/// </summary>
public struct MagicStudyState : IComponent
{
    public Entity Candidate;
    public Entity Replacement;
    public float Progress;
    public float SessionRemaining;
    public double NextStudyWorldTime;
}
