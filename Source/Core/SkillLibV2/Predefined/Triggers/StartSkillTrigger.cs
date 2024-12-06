using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct StartSkillTrigger() : IEventTrigger<StartSkillTrigger, StartSkillContext>
{
    public bool                                                    Enabled           => true;
    public TriggerActionMeta<StartSkillTrigger, StartSkillContext> TriggerActionMeta { get; set; } = null;
}