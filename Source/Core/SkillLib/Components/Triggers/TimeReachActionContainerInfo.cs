namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct TimeReachActionContainerInfo(ActionMeta<TimeReachTrigger, float> meta)
    : IActionContainerInfo<TimeReachTrigger, float>
{
    public ActionMeta<TimeReachTrigger, float> Meta { get; set; } = meta;
}