using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Sects;
using Cultiway.Core.ControlledTasks;

namespace Cultiway.Content.Behaviours;

public class BehWriteSkillbook : BehCityActor
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
            {
                ControlledTaskOrderService.MarkExecutionCommitted(actor);
                actor.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
            }
            else
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
            return BehResult.Continue;
        }

        if (!SectScriptureContributionPlanner.TryPickSkillbookTarget(actor,
                out ScriptureBookDestination target, out var skill))
            return BehResult.Continue;
        if (ScriptureWritingService.TryWriteSkill(actor, target, skill, out _))
            actor.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
