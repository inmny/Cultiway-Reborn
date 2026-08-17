using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Crafting;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindArtifactToCraft : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        if (ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out long orderId))
        {
            if (!ControlledTaskOrderService.TryTakeExecutionContext(actor.getID(),
                    out ControlledArtifactCraftContext context))
            {
                ControlledTaskOrderService.ReportExecutionFailure(actor,
                    "Cultiway.ControlledTask.Reason.ExecutionContextMissing");
                return BehResult.Continue;
            }

            if (CraftSessionService.TryBeginArtifact(actor, context.Materials, orderId,
                    out _, out string reasonLocaleKey))
                ControlledTaskOrderService.MarkExecutionCommitted(actor, true);
            else
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
            return BehResult.Continue;
        }

        List<Entity> available = actor.GetExtend().GetItems()
            .Where(ArtifactCraftCommandConfigurator.IsValidMaterial)
            .ToList();
        if (available.Count == 0) return BehResult.Stop;
        Entity[] ingredients = ArtifactIngredientPlanner.Select(actor, available);
        return CraftSessionService.TryBeginArtifact(actor, ingredients, 0, out _, out _)
            ? BehResult.Continue
            : BehResult.Stop;
    }
}
