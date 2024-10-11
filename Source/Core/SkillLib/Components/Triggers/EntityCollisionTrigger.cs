using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct EntityCollisionTrigger() : ITriggerComponent<Entity>
{
    public uint collision_flag = 0;

    public bool   Enabled         { get; set; } = true;
    public Entity Val             { get; }      = default;
    public Entity ActionContainer { get; set; } = default;
}