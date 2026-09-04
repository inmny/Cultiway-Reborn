using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.Components;

/// <summary>持续状态与来源器官的关系处理方式。</summary>
public enum CreatureStatusRemovalPolicy : byte
{
    /// <summary>来源器官消失后仍然自然到期。</summary>
    PersistUntilExpiry,

    /// <summary>来源器官消失时立刻移除。</summary>
    RemoveWhenSourceMissing
}

/// <summary>记录一条持续状态由哪个器官的哪次身体变更产生。</summary>
public struct CreatureEffectStatusSource : IComponent
{
    /// <summary>被施加状态的生物单位编号。</summary>
    public long OwnerActorId;

    /// <summary>施加状态时的身体变更序号。</summary>
    public int PhenotypeRevision;

    /// <summary>来源器官占用的身体位置。</summary>
    public string SlotId;

    /// <summary>来源器官编号。</summary>
    public string OrganId;

    /// <summary>来源效果类别编号。</summary>
    public string EffectFamilyId;

    /// <summary>来源消失后的处理方式。</summary>
    public CreatureStatusRemovalPolicy RemovalPolicy;
}
