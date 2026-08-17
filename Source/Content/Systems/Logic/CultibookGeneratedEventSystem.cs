using Cultiway.Content.AIGC;
using Cultiway.Content.Events;
using Cultiway.Core.EventSystem;
using Cultiway.Core.ControlledTasks;

namespace Cultiway.Content.Systems.Logic;

public sealed class CultibookGeneratedEventSystem : GenericEventSystem<CultibookGeneratedEvent>
{
    protected override void HandleEvent(CultibookGeneratedEvent evt)
    {
        if (!CultibookRequestService.TryMatchPending(evt.RequestId, CultibookRequestKind.Create,
                evt.ActorId, evt.OrderId, evt.WorldSessionId, out CultibookRequestRecord request))
            return;

        CultibookCommitResult result = CultibookCommitService.TryCommit(
            request, evt.Draft, evt.UsedFallback);
        if (result.Success)
        {
            CultibookRequestService.MarkSucceeded(request, evt.UsedFallback, evt.GeneratorError);
            Actor actor = World.world?.units?.get(evt.ActorId);
            ControlledTaskOrderService.MarkExecutionCommitted(actor);
        }
        else
            CultibookRequestService.MarkFailed(request, result.ReasonLocaleKey,
                evt.UsedFallback, evt.GeneratorError);
    }
}
