using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TimeIntervalContext : IEventContext
{
    public int   trigger_times;
    public float next_trigger_time;
    public bool  JustTriggered { get; set; }
}