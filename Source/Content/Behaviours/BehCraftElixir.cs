using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehCraftElixir : BehCity
{
    public override BehResult execute(Actor pObject)
    {
        ActorExtend ae = pObject.GetExtend();
        if (ae.HasItem<CraftingElixir>()) return BehResult.Continue;
        Entity crafting_elixir_entity = ae.GetFirstItemWithComponent<CraftingElixir>();
        var ingredients = crafting_elixir_entity.GetRelations<CraftOccupyingRelation>().ToArray();

        ref CraftingElixir crafting_elixir = ref crafting_elixir_entity.GetComponent<CraftingElixir>();

        if (crafting_elixir.progress >= ingredients.Length)
        {
            ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.get(crafting_elixir.elixir_id);
            elixir_asset.Craft(ae, crafting_elixir_entity, pObject.city.GetExtend(),
                ingredients.Select(x => x.item).ToArray());
            ModClass.LogInfo($"{pObject.data.id} 完成制作 {elixir_asset.id} 送与 {pObject.city.getCityName()}");
            return BehResult.Continue;
        }

        CraftOccupyingRelation ing_to_show = ingredients[crafting_elixir.progress];
        crafting_elixir.progress++;
        pObject.timer_action = Toolbox.randomFloat(1, 3);

        return BehResult.Continue;
    }
}