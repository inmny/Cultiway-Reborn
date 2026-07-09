using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 逐材料推进炼制；进度满时把进行中的 CraftingArtifact 实体就地升级为成品法器。
/// 器形与效果属性均来自 CraftingArtifact.shape，第一阶段效果属性不实现。
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
            // 用器形的候选名（如"剑"）作为显示名占位
            var label = crafting.shape?.PickIngredientNameCandidate(0);
            crafting_entity.AddComponent(new EntityName(string.IsNullOrEmpty(label) ? "器" : label));
            crafting_entity.AddComponent(new ItemLevel { Stage = 0, Level = 0 });

            ae.AddSpecialItem(crafting_entity);
            ModClass.LogInfo($"{pObject.getName()}[{pObject.data.id}] 完成炼制 {crafting.shape?.id}");
            return BehResult.Continue;
        }

        CraftOccupyingRelation ing_to_show = ingredients[crafting.progress];
        crafting.progress++;
        pObject.timer_action = Randy.randomFloat(1, 3);

        return BehResult.Continue;
    }
}
