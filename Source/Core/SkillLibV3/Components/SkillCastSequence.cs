using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillCastSequence : IComponent
{
    public ActorExtend Caster;
    /// <summary>为 true 时，Caster 仅作为序列生命周期关联对象，生成的技能没有来源对象。</summary>
    public bool Sourceless;
    public Entity SkillContainer;
    public SkillCastStep[] Steps;
    public Kingdom AttackKingdom;
    public SkillCastFundingSource FundingSource;
    public int NextIndex;
    public int EmittedCount;
    public float Elapsed;
    public float Strength;
    public float PowerLevel;
    public SkillCastRuntimeData RuntimeData;
    public int MaxEmitPerTick;
    public SkillCastSequenceOptions Options;
}
