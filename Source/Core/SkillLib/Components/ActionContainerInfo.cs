using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components;

public interface IActionContainerInfo<TTrigger, TVal> : IComponent where TTrigger : struct, ITriggerComponent<TVal>
{
    public ActionMeta<TTrigger, TVal> Meta { get; set; }
}