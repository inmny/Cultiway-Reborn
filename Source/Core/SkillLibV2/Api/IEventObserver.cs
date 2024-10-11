using System.Collections.Generic;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IEventObserver<TEventTrigger, TEventContext> : IEventObserverBase
    where TEventContext : struct, IEventContext
    where TEventTrigger : struct, IEventTrigger<TEventTrigger, TEventContext>
{
    public IEnumerable<TEventTrigger> Triggers { get; }
    public void                       GetObservation(ref TEventContext context);
}