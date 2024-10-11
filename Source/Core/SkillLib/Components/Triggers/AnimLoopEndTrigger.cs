using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct AnimLoopEndTrigger() : ITriggerComponent<int>
{
    public int    target_loop_times;
    public int    loop_times;
    public bool   Enabled         { get; set; } = true;
    public int    Val             => loop_times;
    public Entity ActionContainer { get; set; }
}