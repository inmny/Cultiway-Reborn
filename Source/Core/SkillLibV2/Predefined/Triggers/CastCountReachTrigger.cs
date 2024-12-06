using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct CastCountReachTrigger() : ICustomValueReachTrigger<CastCountReachTrigger, CastCountReachContext, int>
{
    public bool Enabled { get; set; } = true;
    public TriggerActionMeta<CastCountReachTrigger, CastCountReachContext> TriggerActionMeta { get; set; } = null;
    public CompareResult ExpectedResult { get; set; } = CompareResult.EqualToTarget;

    public int TargetValue { get; set; } = 0;
}