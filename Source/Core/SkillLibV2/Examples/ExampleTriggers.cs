using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Examples;

public struct CastCountReachContext : ICustomValueReachContext<int>
{
    public bool JustTriggered { get; set; }
    public int  Value         { get; set; }
}

public struct CastCountReachTrigger : ICustomValueReachTrigger<CastCountReachTrigger, CastCountReachContext, int>
{
    public TriggerActionMeta<CastCountReachTrigger, CastCountReachContext> TriggerActionMeta { get; set; }
    public CompareResult                                                   ExpectedResult    { get; set; }

    public int TargetValue { get; set; }
}