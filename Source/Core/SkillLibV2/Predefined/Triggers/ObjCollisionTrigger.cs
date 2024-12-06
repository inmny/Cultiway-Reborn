using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct ObjCollisionTrigger() : IEventTrigger<ObjCollisionTrigger, ObjCollisionContext>
{
    public bool actor    = false;
    public bool building = false;
    public bool friend   = false;
    public bool enemy    = false;


    public bool                                                        Enabled           { get; set; } = true;
    public TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> TriggerActionMeta { get; set; } = null;
}