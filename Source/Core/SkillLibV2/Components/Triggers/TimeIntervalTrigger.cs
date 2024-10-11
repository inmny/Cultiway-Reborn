using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct TimeIntervalTrigger : IEventTrigger<TimeIntervalTrigger, TimeIntervalContext>
{
    public float                                                       interval_time;
    public TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> TriggerActionMeta { get; set; }
}