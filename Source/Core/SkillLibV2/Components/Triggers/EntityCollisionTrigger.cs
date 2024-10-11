using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct EntityCollisionTrigger : IEventTrigger<EntityCollisionTrigger, EntityCollisionContext>
{
    public TriggerActionMeta<EntityCollisionTrigger, EntityCollisionContext> TriggerActionMeta { get; }
}