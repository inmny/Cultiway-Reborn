using System;
using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehCraftElixir : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        ActorExtend ae = pObject.GetExtend();
        if (!ae.HasItem<CraftingElixir>()) return BehResult.Continue;
        Entity crafting_elixir_entity = ae.GetFirstItemWithComponent<CraftingElixir>();
        var ingredients = crafting_elixir_entity.GetRelations<CraftOccupyingRelation>().ToArray();

        ref CraftingElixir crafting_elixir = ref crafting_elixir_entity.GetComponent<CraftingElixir>();
        ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.get(crafting_elixir.elixir_id);
        if (ingredients.Length < elixir_asset.ingredients.Length)
        {
            ModClass.LogWarning($"{pObject.data.id} 炼丹失败，原料不足(可能有原料过期了)");
            crafting_elixir_entity.AddTag<TagRecycle>();
            foreach (var ingredient in ingredients)
            {
                ingredient.item.AddTag<TagRecycle>();
            }
            return BehResult.Continue;
        }
        if (crafting_elixir.progress >= ingredients.Length)
        {
            elixir_asset.Craft(ae, crafting_elixir_entity, pObject.city.GetExtend(),
                ingredients.Select(x => x.item).ToArray());
            ae.Master(elixir_asset, ae.GetMaster(elixir_asset) + 1);
            ModClass.LogInfo($"{pObject.data.id} 完成制作 {elixir_asset.GetName()} 送与 {pObject.city.name}");
            return BehResult.Continue;
        }

        CraftOccupyingRelation ing_to_show = ingredients[crafting_elixir.progress];
        crafting_elixir.progress++;
        //ModClass.LogInfo(
        //    $"{pObject.data.id} 正在制作({crafting_elixir.progress}/{ingredients.Length}) {crafting_elixir.elixir_id}");
        pObject.timer_action = Randy.randomFloat(1, 3);

        return BehResult.Continue;
    }
}