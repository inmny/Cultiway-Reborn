using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct TimeReachTrigger() : ITriggerComponent<float>
{
    public float  target_time;
    public bool   Enabled         { get; set; } = true;
    public float  Val             => target_time;
    public Entity ActionContainer { get; set; }
}