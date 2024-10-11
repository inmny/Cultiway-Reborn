namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct TimeIntervalActionContainerInfo(ActionMeta<TimeIntervalTrigger, float> meta)
    : IActionContainerInfo<TimeIntervalTrigger, float>
{
    public ActionMeta<TimeIntervalTrigger, float> Meta { get; set; } = meta;
}