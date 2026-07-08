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
            // 炼成：删材料 → 去掉进行中标记/组件 → 加成品组件与命名 → 入城库
            var ingredient_array = new Entity[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++)
            {
                ingredient_array[i] = ingredients[i].item;
                ingredients[i].item.DeleteEntity();
            }

            crafting_entity.RemoveComponent<CraftingArtifact>();
            crafting_entity.RemoveTag<TagUncompleted>();
            crafting_entity.AddComponent(new Artifact());
            // 用器形的候选名（如"剑"）作为显示名占位
            var label = crafting.shape?.PickIngredientNameCandidate(0);
            crafting_entity.AddComponent(new EntityName(string.IsNullOrEmpty(label) ? "器" : label));
            crafting_entity.AddComponent(new ItemLevel { Stage = 0, Level = 0 });

            pObject.city.GetExtend().AddSpecialItem(crafting_entity);
            ModClass.LogInfo($"{pObject.data.id} 完成炼制 {crafting.shape?.id} 送与 {pObject.city.name}");
            return BehResult.Continue;
        }

        CraftOccupyingRelation ing_to_show = ingredients[crafting.progress];
        crafting.progress++;
        pObject.timer_action = Randy.randomFloat(1, 3);

        return BehResult.Continue;
    }
}
