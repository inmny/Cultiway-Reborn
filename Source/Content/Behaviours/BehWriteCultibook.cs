using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core.ControlledTasks;

namespace Cultiway.Content.Behaviours;

public class BehWriteCultibook : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        if (ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out _))
        {
            if (!ControlledTaskOrderService.TryTakeExecutionContext(actor.getID(),
                    out ControlledScriptureWriteContext context))
            {
                ControlledTaskOrderService.ReportExecutionFailure(actor,
                    "Cultiway.ControlledTask.Reason.ExecutionContextMissing");
                return BehResult.Continue;
            }

            if (ScriptureWritingService.TryWrite(actor, context, out string reasonLocaleKey))
                ControlledTaskOrderService.MarkExecutionCommitted(actor);
            else
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
            return BehResult.Continue;
        }

        if (!SectScriptureContributionPlanner.TryPickCultibookTarget(actor,
                out ScriptureBookDestination target, out var cultibook, out float mastery))
            return BehResult.Continue;
        if (!ScriptureWritingService.TryWriteCultibook(actor, target, cultibook, mastery, out _))
            return BehResult.Continue;
        return BehResult.Continue;
    }
}
