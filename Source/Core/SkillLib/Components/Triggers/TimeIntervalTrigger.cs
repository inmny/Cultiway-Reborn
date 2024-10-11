using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct TimeIntervalTrigger() : ITriggerComponent<float>
{
    public float  interval;
    public bool   Enabled         { get; set; } = true;
    public float  Val             => interval;
    public Entity ActionContainer { get; set; }
}