namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct AnimLoopEndActionContainerInfo(ActionMeta<AnimLoopEndTrigger, int> meta)
    : IActionContainerInfo<AnimLoopEndTrigger, int>
{
    public ActionMeta<AnimLoopEndTrigger, int> Meta { get; set; } = meta;
}