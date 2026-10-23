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

        bool wrote = false;
        if (Randy.randomChance(.5f) &&
            Skills.ConstructedAbilityService.TryPickBook(actor, out var firstTarget, out string firstId))
            wrote = ScriptureWritingService.TryWriteConstruction(actor, firstTarget, firstId, out _);
        else if (SectScriptureContributionPlanner.TryPickSkillbookTarget(actor, out var target, out var skill))
            wrote = ScriptureWritingService.TryWriteSkill(actor, target, skill, out _);
        else if (Skills.ConstructedAbilityService.TryPickBook(actor, out var fallbackTarget, out string fallbackId))
            wrote = ScriptureWritingService.TryWriteConstruction(actor, fallbackTarget, fallbackId, out _);
        if (wrote)
            actor.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
