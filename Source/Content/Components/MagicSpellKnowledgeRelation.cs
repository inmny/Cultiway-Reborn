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
    /// <summary>当前掌握的法术容器，也是关系键。</summary>
    public Entity SkillContainer;

    /// <summary>当前版本法术被施法者掌握时的世界时间。</summary>
    public double LearnedWorldTime;

    /// <summary>当前版本法术的知识来源。</summary>
    public MagicSpellKnowledgeSource Source;

    /// <summary>该法术族累计完成的施法序列数量，改进后继续继承。</summary>
    public long TotalCastCount;

    /// <summary>尚未用于改进的使用积累；一次有效施法序列增加一点。</summary>
    public float ImprovementProgress;

    /// <summary>该法术族已经成功完成的个人改进次数。</summary>
    public int ImprovementCount;

    /// <summary>最近一次完成有效施法序列的世界时间。</summary>
    public double LastUsedWorldTime;

    /// <summary>最近一次成功改进法术的世界时间。</summary>
    public double LastImprovedWorldTime;

    /// <summary>下一次允许尝试生成改进候选的世界时间，用于失败退避。</summary>
    public double NextImprovementAttemptWorldTime;

    /// <summary>
    /// 返回当前关系指向的法术容器。
    /// </summary>
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

/// <summary>
/// 魔法师研读实体卷轴时保留的进度。卷轴失效后该状态会被清空。
/// </summary>
public struct MagicScrollStudyState : IComponent
{
    /// <summary>当前正在研读的卷轴实体。</summary>
    public Entity Scroll;

    /// <summary>同族升级时待替换的旧法术；学习新法术时为空。</summary>
    public Entity Replacement;

    /// <summary>跨阅读轮次保留的累计理解进度。</summary>
    public float Progress;

    /// <summary>当前阅读轮次的剩余时间。</summary>
    public float SessionRemaining;
}
