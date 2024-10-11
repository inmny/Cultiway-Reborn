using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct ObjCollisionTrigger : IEventTrigger<ObjCollisionTrigger, ObjCollisionContext>
{
    public bool                                                        actor;
    public bool                                                        building;
    public bool                                                        friend;
    public bool                                                        enemy;
    public TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> TriggerActionMeta { get; set; }
}