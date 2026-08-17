using ai.behaviours;
using Cultiway.Content.AIGC;
using Cultiway.Core.ControlledTasks;

namespace Cultiway.Content.Behaviours;

public sealed class BehImproveCultibook : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        if (!CultibookRequestService.TryGetActive(actor.getID(), CultibookRequestKind.Improve,
                out CultibookRequestRecord request))
        {
            long orderId = ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out long activeOrderId)
                ? activeOrderId
                : 0;
            if (!CultibookRequestService.TryStartImprove(actor, orderId, out request,
                    out string reasonLocaleKey))
            {
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
                CultibookRequestService.RemoveTerminal(request?.RequestId);
                return BehResult.Continue;
            }
        }

        if (request.State == CultibookRequestState.Pending)
        {
            StayInside(actor);
            actor.timer_action = 0.25f;
            return BehResult.RepeatStep;
        }

        if (request.State != CultibookRequestState.Succeeded)
            ControlledTaskOrderService.ReportExecutionFailure(actor, request.ErrorReasonLocaleKey);
        CultibookRequestService.RemoveTerminal(request.RequestId);
        return BehResult.Continue;
    }

    private static void StayInside(Actor actor)
    {
        if (actor.beh_building_target != null)
            actor.stayInBuilding(actor.beh_building_target);
        else if (actor.inside_building != null)
            actor.stayInBuilding(actor.inside_building);
    }
}
