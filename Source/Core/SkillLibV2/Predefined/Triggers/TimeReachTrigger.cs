using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct TimeReachTrigger() : IEventTrigger<TimeReachTrigger, TimeReachContext>
{
    public float                                                 target_time = 0;
    public bool                                                  loop        = false;
    public bool                                                  Enabled           { get; set; } = true;
    public TriggerActionMeta<TimeReachTrigger, TimeReachContext> TriggerActionMeta { get; set; } = null;
}