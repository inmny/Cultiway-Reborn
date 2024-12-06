using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TimeIntervalTrigger() : IEventTrigger<TimeIntervalTrigger, TimeIntervalContext>
{
    public float interval_time = 1;


    public bool                                                        Enabled           { get; set; } = true;
    public TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> TriggerActionMeta { get; set; } = null;
}