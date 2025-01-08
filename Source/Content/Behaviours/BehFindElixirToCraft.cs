using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindElixirToCraft : BehCity
{
    public override BehResult execute(Actor pObject)
    {
        // 如果人物的背包里有CraftingElixir的实体，那么直接返回BehResult.Continue
        ActorExtend ae = pObject.GetExtend();
        if (ae.HasItem<CraftingElixir>()) return BehResult.Continue;
        // 找配方以及材料，把材料都放到背包里，并开启一个CraftingElixir的实体，并添加到人物的背包里，并标记占用
        ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.GetRandom();
        if (elixir_asset.QueryInventoryForIngrediants(pObject.city.GetExtend(), out var ingredients))
        {
            Entity crafting_elixir = SpecialItemUtils
                .StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime(), pObject.getName())
                .AddComponent(new CraftingElixir
                {
                    elixir_id = elixir_asset.id
                })
                .Build();
            ae.AddSpecialItem(crafting_elixir);
            foreach (Entity ing in ingredients)
            {
                ae.AddSpecialItem(ing);
                crafting_elixir.AddRelation(new CraftOccupyingRelation { item = ing });
            }

            //ModClass.LogInfo($"{pObject.data.id} 开始制作 {elixir_asset.id}");
            return BehResult.Continue;
        }

        return BehResult.Stop;
    }
}