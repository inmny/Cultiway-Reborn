using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct EntityCollisionTrigger() : IEventTrigger<EntityCollisionTrigger, EntityCollisionContext>
{
    public bool                                                              Enabled           { get; set; } = true;
    public TriggerActionMeta<EntityCollisionTrigger, EntityCollisionContext> TriggerActionMeta { get; }      = null;
}