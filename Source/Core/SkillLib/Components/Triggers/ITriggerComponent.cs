using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public interface ITriggerComponent<T> : IComponent
{
    public bool   Enabled         { get; set; }
    public T      Val             { get; }
    public Entity ActionContainer { get; set; }
}