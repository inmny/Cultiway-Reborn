using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IEventTrigger<TEventTrigger, TEventContext> : IComponent
    where TEventContext : struct, IEventContext
    where TEventTrigger : struct, IEventTrigger<TEventTrigger, TEventContext>
{
    public TriggerActionMeta<TEventTrigger, TEventContext> TriggerActionMeta { get; }
}