namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct ObjCollisionActionContainerInfo(ActionMeta<ObjCollisionTrigger, BaseSimObject> meta)
    : IActionContainerInfo<ObjCollisionTrigger, BaseSimObject>
{
    public ActionMeta<ObjCollisionTrigger, BaseSimObject> Meta { get; set; } = meta;
}