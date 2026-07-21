using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehFindNewElixir : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor actor)
    {
        var actorExtend = actor.GetExtend();
        if (actorExtend.HasItem<CraftingElixir>()) return BehResult.Continue;

        var inventory = (IHasInventory)actorExtend;
        using var pool = new ListPool<Entity>(inventory.GetItems().Where(item =>
            item.Tags.Has<TagIngredient>() &&
            !item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied, TagRecycle>())));
        if (!pool.Any()) return BehResult.Stop;

        var availableCount = ((IList<Entity>)pool).Count;
        var ingredientCount = Math.Min(
            Randy.randomInt(1, Mathf.FloorToInt(Mathf.Log(availableCount)) + 2),
            availableCount);
        var ingredients = pool.SampleOut(ingredientCount);
        var asset = Libraries.Manager.ElixirLibrary.NewElixir(ingredients);
        actorExtend.Master(asset, Mathf.Max(1f, actorExtend.GetMaster(asset)));
        ModClass.LogInfo($"{actorExtend} 推演出丹方 {asset.GetName()}");

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

        actor.timer_action = Randy.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerYear);
        return BehResult.Continue;
    }
}
