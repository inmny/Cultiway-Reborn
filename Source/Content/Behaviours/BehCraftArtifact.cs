using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 逐材料推进炼制；进度满时把进行中的 CraftingArtifact 实体就地升级为成品法器。
/// 器形、名字、等级、atom 和图标实例在开工时已经写入实体组件。
/// </summary>
public class BehCraftArtifact : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        ActorExtend ae = pObject.GetExtend();
        if (!ae.HasItem<CraftingArtifact>()) return BehResult.Continue;
        Entity crafting_entity = ae.GetFirstItemWithComponent<CraftingArtifact>();
        var ingredients = crafting_entity.GetRelations<CraftOccupyingRelation>();

        ref CraftingArtifact crafting = ref crafting_entity.GetComponent<CraftingArtifact>();
        if (ingredients.Length == 0)
        {
            ModClass.LogWarning($"{pObject.data.id} 炼器失败，原料不足(可能有原料过期了)");
            crafting_entity.AddTag<TagRecycle>();
            return BehResult.Continue;
        }
        if (crafting.progress >= ingredients.Length)
        {
            ArtifactProductionResultEvent result = ArtifactProductionService.DispatchResult(
                ae,
                ArtifactProductionProcesses.ArtifactRefining,
                crafting_entity.GetComponent<ArtifactMaterialData>(),
                crafting_entity);
            if (result.QualityBonus != 0)
            {
                ref ItemLevel level = ref crafting_entity.GetComponent<ItemLevel>();
                level = ItemLevel.FromValue(level + result.QualityBonus);
            }

            // 先完整拷贝材料数组，再删除——DeleteEntity 会移除 crafting_entity 上的
            // CraftOccupyingRelation，导致 relations 快照失效，故不能边遍历边删。
            var ingredient_array = new Entity[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++)
            {
                ingredient_array[i] = ingredients[i].item;
            }
            for (int i = 0; i < ingredient_array.Length; i++)
            {
                ingredient_array[i].DeleteEntity();
            }

            crafting_entity.RemoveComponent<CraftingArtifact>();
            crafting_entity.RemoveTag<TagUncompleted>();
            crafting_entity.AddComponent(new Artifact());
            crafting_entity.GetComponent<AliveTimeLimit>().value = crafting_entity.GetComponent<ItemLevel>() * 10 * TimeScales.SecPerYear;
            int outputCount = ArtifactProductionService.ResolveOutputCount(result.YieldMultiplier);
            for (int i = 1; i < outputCount; i++)
            {
                ae.AddSpecialItem(ArtifactProductionService.CloneProduct(crafting_entity));
            }
            ae.EquipArtifact(crafting_entity);

            ModClass.LogInfo($"{pObject.getName()}[{pObject.data.id}] 完成炼制 {crafting_entity.Name} x{outputCount}");
            return BehResult.Continue;
        }

        ArtifactProductionStepEvent step = ArtifactProductionService.DispatchStep(
            ae,
            ArtifactProductionProcesses.ArtifactRefining,
            crafting_entity.GetComponent<ArtifactMaterialData>(),
            crafting_entity,
            Randy.randomFloat(1f, 3f));
        crafting.progress += System.Math.Max(1, step.ProgressGain);
        pObject.timer_action = Mathf.Max(0.15f, step.Duration);

        return BehResult.Continue;
    }
}
