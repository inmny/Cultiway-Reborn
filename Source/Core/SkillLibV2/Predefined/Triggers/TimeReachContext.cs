using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TimeReachContext : IEventContext
{
    public   bool  JustTriggered { get; set; }
    public   float next_trigger_time;
    internal float timer;
}