using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct ObjCollisionTrigger() : ITriggerComponent<BaseSimObject>
{
    public uint          collision_flag;
    public float         radius;
    public bool          Enabled         { get; set; } = true;
    public BaseSimObject Val             { get; set; }
    public Entity        ActionContainer { get; set; }
}