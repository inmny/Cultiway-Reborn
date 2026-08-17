using System;
using ai.behaviours;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Crafting;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehCraftElixir : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor actor)
    {
        ActorExtend extend = actor.GetExtend();
        if (!extend.HasItem<CraftingElixir>())
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor,
                "Cultiway.ControlledTask.Reason.CraftingProcessMissing");
            return BehResult.Continue;
        }

        Entity craftingItem = extend.GetFirstItemWithComponent<CraftingElixir>();
        if (!CraftSessionService.ValidateSession(actor, craftingItem, CraftProcessType.Alchemy,
                out string sessionReason))
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor, sessionReason);
            CraftFailureService.Fail(craftingItem, CraftFailureReason.Interrupted);
            return BehResult.Continue;
        }

        var ingredients = craftingItem.GetRelations<CraftOccupyingRelation>();
        ref CraftingElixir crafting = ref craftingItem.GetComponent<CraftingElixir>();
        ElixirAsset recipe = Libraries.Manager.ElixirLibrary.get(crafting.elixir_id);
        int requiredIngredientCount = recipe?.ingredients?.Length ?? 0;
        if (requiredIngredientCount == 0 || ingredients.Length < requiredIngredientCount)
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor,
                "Cultiway.ControlledTask.Reason.IngredientsMissing");
            CraftFailureService.Fail(craftingItem, CraftFailureReason.IngredientsMissing);
            return BehResult.Continue;
        }

        if (crafting.progress >= requiredIngredientCount)
        {
            var ingredientArray = new Entity[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++) ingredientArray[i] = ingredients[i].item;
            try
            {
                recipe.Craft(extend, craftingItem, actor.city.GetExtend(), ingredientArray);
                if (craftingItem.HasComponent<CraftSession>()) craftingItem.RemoveComponent<CraftSession>();
                extend.Master(recipe, extend.GetMaster(recipe) + 1);
                ControlledTaskOrderService.MarkExecutionCompleted(actor);
                ModClass.LogInfo($"{actor.data.id} 完成制作 {recipe.GetName()} 送与 {actor.city.name}");
            }
            catch (Exception exception)
            {
                ModClass.LogError($"[CraftSession] elixir completion failed actor={actor.getID()}: {exception}");
                ControlledTaskOrderService.ReportExecutionFailure(actor,
                    "Cultiway.ControlledTask.Reason.CraftingCompletionFailed");
                CraftFailureService.Fail(craftingItem, CraftProcessType.Alchemy,
                    CraftFailureReason.InvalidProcess);
            }
            return BehResult.Continue;
        }

        ArtifactProductionStepEvent productionStep = ArtifactProductionService.DispatchStep(
            extend,
            ArtifactProductionProcesses.Alchemy,
            recipe,
            craftingItem,
            Randy.randomFloat(1f, 3f));
        ElixirCraftStepEvent step = new(recipe, craftingItem, productionStep.Duration)
        {
            ProgressGain = productionStep.ProgressGain,
        };
        ArtifactAbilityDispatcher.Dispatch(extend.E, step);
        crafting.progress += Math.Max(1, step.ProgressGain);
        actor.timer_action = Math.Max(0.15f, step.Duration);
        return BehResult.RepeatStep;
    }
}
