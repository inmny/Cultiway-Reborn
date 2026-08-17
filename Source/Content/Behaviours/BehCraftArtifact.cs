using System;
using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Crafting;
using Cultiway.Content.Events;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehCraftArtifact : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor actor)
    {
        ActorExtend extend = actor.GetExtend();
        if (!extend.HasItem<CraftingArtifact>())
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor,
                "Cultiway.ControlledTask.Reason.CraftingProcessMissing");
            return BehResult.Continue;
        }

        Entity craftingItem = extend.GetFirstItemWithComponent<CraftingArtifact>();
        if (!CraftSessionService.ValidateSession(actor, craftingItem, CraftProcessType.ArtifactRefining,
                out string sessionReason))
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor, sessionReason);
            CraftFailureService.Fail(craftingItem, CraftFailureReason.Interrupted);
            return BehResult.Continue;
        }

        var ingredients = craftingItem.GetRelations<CraftOccupyingRelation>();
        ref CraftingArtifact crafting = ref craftingItem.GetComponent<CraftingArtifact>();
        if (!craftingItem.TryGetComponent(out ArtifactMaterialData materialData) ||
            materialData.ingredient_count <= 0 || ingredients.Length < materialData.ingredient_count)
        {
            ControlledTaskOrderService.ReportExecutionFailure(actor,
                "Cultiway.ControlledTask.Reason.IngredientsMissing");
            CraftFailureService.Fail(craftingItem, CraftFailureReason.IngredientsMissing);
            return BehResult.Continue;
        }

        if (crafting.progress >= materialData.ingredient_count)
        {
            var spawned = new System.Collections.Generic.List<Entity>();
            bool committed = false;
            try
            {
                ArtifactProductionResultEvent result = ArtifactProductionService.DispatchResult(
                    extend,
                    ArtifactProductionProcesses.ArtifactRefining,
                    materialData,
                    craftingItem);
                if (result.QualityBonus != 0)
                {
                    ref ItemLevel level = ref craftingItem.GetComponent<ItemLevel>();
                    level = ItemLevel.FromValue(level + result.QualityBonus);
                }

                var ingredientArray = new Entity[ingredients.Length];
                for (int i = 0; i < ingredients.Length; i++) ingredientArray[i] = ingredients[i].item;
                for (int i = 0; i < ingredientArray.Length; i++) ingredientArray[i].DeleteEntity();

                craftingItem.RemoveComponent<CraftingArtifact>();
                craftingItem.RemoveTag<TagUncompleted>();
                craftingItem.AddComponent(new Artifact());
                craftingItem.GetComponent<AliveTimeLimit>().value =
                    craftingItem.GetComponent<ItemLevel>() * 10 * TimeScales.SecPerYear;
                int outputCount = ArtifactProductionService.ResolveOutputCount(result.YieldMultiplier);
                for (int i = 1; i < outputCount; i++)
                {
                    Entity clone = ArtifactProductionService.CloneProduct(craftingItem);
                    if (clone.HasComponent<CraftSession>()) clone.RemoveComponent<CraftSession>();
                    spawned.Add(clone);
                    extend.AddSpecialItem(clone);
                }
                extend.EquipArtifact(craftingItem);

                // 成品已完成装备和副产物转移，此处之后只允许记录事件，不能再回滚成品。
                if (craftingItem.HasComponent<CraftSession>()) craftingItem.RemoveComponent<CraftSession>();
                committed = true;
                ControlledTaskOrderService.MarkExecutionCompleted(actor);
                ItemLevel finalLevel = craftingItem.GetComponent<ItemLevel>();
                try
                {
                    ProductionLifecycle.PublishCompleted(new ProductionCompletedEvent(
                        extend,
                        ArtifactProductionProcesses.ArtifactRefining,
                        materialData,
                        craftingItem,
                        finalLevel,
                        outputCount));
                }
                catch (Exception publishException)
                {
                    ModClass.LogError($"[CraftSession] artifact completion event failed actor={actor.getID()}: {publishException}");
                }
                ModClass.LogInfo($"{actor.getName()}[{actor.data.id}] 完成炼制 {craftingItem.Name} x{outputCount}");
            }
            catch (Exception exception)
            {
                ModClass.LogError($"[CraftSession] artifact completion failed actor={actor.getID()}: {exception}");
                if (committed)
                {
                    ControlledTaskOrderService.MarkExecutionCompleted(actor);
                }
                else
                {
                    extend.UnequipArtifact(craftingItem, suppressAutoEquip: true);
                    for (int i = 0; i < spawned.Count; i++)
                        if (!spawned[i].IsNull) spawned[i].DeleteEntity();
                    ControlledTaskOrderService.ReportExecutionFailure(actor,
                        "Cultiway.ControlledTask.Reason.CraftingCompletionFailed");
                    CraftFailureService.Fail(craftingItem, CraftProcessType.ArtifactRefining,
                        CraftFailureReason.InvalidProcess);
                }
            }
            return BehResult.Continue;
        }

        ArtifactProductionStepEvent step = ArtifactProductionService.DispatchStep(
            extend,
            ArtifactProductionProcesses.ArtifactRefining,
            materialData,
            craftingItem,
            Randy.randomFloat(1f, 3f));
        crafting.progress += Math.Max(1, step.ProgressGain);
        actor.timer_action = Mathf.Max(0.15f, step.Duration);
        return BehResult.RepeatStep;
    }
}
