using System;
using System.Linq;
using ai.behaviours;
using Cultiway.Content.Crafting;
using Cultiway.Content.Libraries;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehFindElixirToCraft : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        if (ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out long orderId))
        {
            if (!ControlledTaskOrderService.TryTakeExecutionContext(actor.getID(),
                    out ControlledElixirCraftContext context))
            {
                ControlledTaskOrderService.ReportExecutionFailure(actor,
                    "Cultiway.ControlledTask.Reason.ExecutionContextMissing");
                return BehResult.Continue;
            }

            if (CraftSessionService.TryBeginElixir(actor, context.RecipeId, orderId,
                    out _, out string reasonLocaleKey))
                ControlledTaskOrderService.MarkExecutionCommitted(actor, true);
            else
                ControlledTaskOrderService.ReportExecutionFailure(actor, reasonLocaleKey);
            return BehResult.Continue;
        }

        var recipes = actor.GetExtend().GetAllMaster<ElixirAsset>()
            .Select(entry => entry.Item1)
            .Where(asset => asset != null)
            .OrderBy(asset => asset.id, StringComparer.Ordinal)
            .ToArray();
        if (recipes.Length == 0) return BehResult.Stop;

        int startIndex = Randy.randomInt(0, recipes.Length);
        for (int offset = 0; offset < recipes.Length; offset++)
        {
            ElixirAsset recipe = recipes[(startIndex + offset) % recipes.Length];
            if (!recipe.QueryInventoryForIngredients(actor.GetExtend(), out _)) continue;
            return CraftSessionService.TryBeginElixir(actor, recipe.id, 0, out _, out _)
                ? BehResult.Continue
                : BehResult.Stop;
        }
        return BehResult.Stop;
    }
}
