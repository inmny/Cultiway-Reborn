using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Triggers;

public struct StartObjTrigger() : ITriggerComponent<BaseSimObject>
{
    public BaseSimObject target;
    public bool          Enabled         { get; set; } = true;
    public BaseSimObject Val             => target;
    public Entity        ActionContainer { get; set; }
}