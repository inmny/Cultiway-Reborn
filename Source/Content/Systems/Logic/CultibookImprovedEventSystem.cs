using Cultiway.Content.AIGC;
using Cultiway.Content.Events;
using Cultiway.Core.EventSystem;
using Cultiway.Core.ControlledTasks;

namespace Cultiway.Content.Systems.Logic;

public sealed class CultibookImprovedEventSystem : GenericEventSystem<CultibookImprovedEvent>
{
    protected override void HandleEvent(CultibookImprovedEvent evt)
    {
        if (!CultibookRequestService.TryMatchPending(evt.RequestId, CultibookRequestKind.Improve,
                evt.ActorId, evt.OrderId, evt.WorldSessionId, out CultibookRequestRecord request))
            return;
        if (evt.OriginalCultibookId != request.OriginalCultibookId)
        {
            CultibookRequestService.MarkFailed(request,
                "Cultiway.ControlledTask.Reason.MainCultibookChanged",
                evt.UsedFallback, evt.GeneratorError);
            return;
        }

        CultibookCommitResult result = CultibookCommitService.TryCommit(
            request, evt.ImprovedDraft, evt.UsedFallback);
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
