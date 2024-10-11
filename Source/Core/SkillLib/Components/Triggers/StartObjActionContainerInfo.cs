namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct StartObjActionContainerInfo(ActionMeta<StartObjTrigger, BaseSimObject> meta)
    : IActionContainerInfo<StartObjTrigger, BaseSimObject>
{
    public ActionMeta<StartObjTrigger, BaseSimObject> Meta { get; set; } = meta;
}