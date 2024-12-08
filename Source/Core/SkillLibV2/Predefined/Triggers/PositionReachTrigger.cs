using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct PositionReachTrigger() : IEventTrigger<PositionReachTrigger, PositionReachContext>
{
    public bool                                                          Enabled { get; set; } = true;
    public float                                                         distance = 1f;
    public TriggerActionMeta<PositionReachTrigger, PositionReachContext> TriggerActionMeta { get; set; } = null;
}