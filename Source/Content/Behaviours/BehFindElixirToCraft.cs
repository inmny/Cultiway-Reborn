using System;
using System.Linq;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

public class BehFindElixirToCraft : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        var actorExtend = actor.GetExtend();
        if (actorExtend.HasItem<CraftingElixir>()) return BehResult.Continue;

        var recipes = actorExtend.GetAllMaster<ElixirAsset>()
            .Select(entry => entry.Item1)
            .Where(asset => asset != null)
            .OrderBy(asset => asset.id, StringComparer.Ordinal)
            .ToArray();
        if (recipes.Length == 0) return BehResult.Stop;

        var startIndex = Randy.randomInt(0, recipes.Length);
        for (var offset = 0; offset < recipes.Length; offset++)
        {
            var asset = recipes[(startIndex + offset) % recipes.Length];
            if (!asset.QueryInventoryForIngredients(actorExtend, out var ingredients)) continue;

            var craftingElixir = SpecialItemUtils
                .StartBuild(ItemShapes.Ball, World.world.getCurWorldTime(), actor.getName())
                .AddComponent(new CraftingElixir { elixir_id = asset.id })
                .AddTag<TagUncompleted>()
                .Build();
            actorExtend.AddSpecialItem(craftingElixir);
            foreach (var ingredient in ingredients)
            {
                craftingElixir.AddRelation(new CraftOccupyingRelation { item = ingredient });
                ingredient.AddTag<TagConsumed>();
            }
            return BehResult.Continue;
        }

        return BehResult.Stop;
    }
}
