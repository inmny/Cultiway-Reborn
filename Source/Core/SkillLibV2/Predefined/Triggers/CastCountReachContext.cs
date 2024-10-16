using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct CastCountReachContext : ICustomValueReachContext<int>
{
    public bool JustTriggered { get; set; }
    public int  Value         { get; set; }
}