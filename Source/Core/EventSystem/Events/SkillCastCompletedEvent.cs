using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.EventSystem.Events;

/// <summary>
/// 一次技能施法序列完成后的结算事件。
/// </summary>
public readonly struct SkillCastCompletedEvent
{
    public ActorExtend Caster { get; }
    public Entity SkillContainer { get; }
    public int EmittedCount { get; }
    public SkillCastFundingSource FundingSource { get; }
    public SkillCastRuntimeData RuntimeData { get; }

    public SkillCastCompletedEvent(ActorExtend caster, Entity skillContainer, int emittedCount,
        SkillCastFundingSource fundingSource, SkillCastRuntimeData runtimeData = default)
    {
        Caster = caster;
        SkillContainer = skillContainer;
        EmittedCount = emittedCount;
        FundingSource = fundingSource;
        RuntimeData = runtimeData;
    }
}
