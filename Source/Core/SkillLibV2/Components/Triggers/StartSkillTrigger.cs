using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct StartSkillTrigger : IEventTrigger<StartSkillTrigger, StartSkillContext>
{
    public TriggerActionMeta<StartSkillTrigger, StartSkillContext> TriggerActionMeta { get; }
}