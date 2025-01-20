using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindNewElixir : BehCity
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        if (ae.HasItem<CraftingElixir>()) return BehResult.Continue;

        var base_asset = Libraries.Manager.ElixirLibrary.GetRandom();
        if (base_asset.QueryInventoryForIngredients(ae, out var ingredients))
        {
            var new_asset = Libraries.Manager.ElixirLibrary.NewElixir(base_asset, ingredients, ae);
            if (new_asset == null)
            {
                pObject.timer_action = Toolbox.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerYear);
                return BehResult.RepeatStep;
            }
            Entity crafting_elixir = SpecialItemUtils
                .StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime(), pObject.getName())
                .AddComponent(new CraftingElixir
                {
                    elixir_id = new_asset.id
                })
                .Build();
            ae.AddSpecialItem(crafting_elixir);
            foreach (Entity ing in ingredients)
            {
                crafting_elixir.AddRelation(new CraftOccupyingRelation { item = ing });
            }
        }

        return BehResult.Continue;
    }
}