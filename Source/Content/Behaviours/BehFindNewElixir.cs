using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
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
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        pObject.data.get(ContentActorDataKeys.WaitingForElixirGeneration_string, out string waitingId, "");
        if (!string.IsNullOrEmpty(waitingId))
        {
            var asset = Libraries.Manager.ElixirLibrary.get(waitingId);
            if (asset == null)
            {
                pObject.data.set(ContentActorDataKeys.WaitingForElixirGeneration_string, "");
                return BehResult.Stop;
            }

            if (!asset.effect_ready)
            {
                StayInside(pObject);
                return BehResult.RepeatStep;
            }

            pObject.data.set(ContentActorDataKeys.WaitingForElixirGeneration_string, "");
            return BehResult.Continue;
        }

        if (ae.HasItem<CraftingElixir>()) return BehResult.Continue;

        var inv = (IHasInventory)ae;

        using var list = new ListPool<Entity>(inv.GetItems().Where(x=>x.Tags.Has<TagIngredient>()));
        if (!list.Any()) return BehResult.Stop;
        var count = ((IList<Entity>)list).Count;

        var ing_count = Math.Min(Randy.randomInt(1, (int)Mathf.Log(count) + 2), count);
        
        var ingredients = list.SampleOut(ing_count);
        var new_asset = Libraries.Manager.ElixirLibrary.NewElixir(ingredients, ae);
        if (new_asset == null)
        {
            return BehResult.Stop;
        }
        ae.Master(new_asset, 1);
        ModClass.LogInfo($"{ae} creates new elixir {new_asset.name_key}");
        Entity crafting_elixir = SpecialItemUtils
            .StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime(), pObject.getName())
            .AddComponent(new CraftingElixir
            {
                elixir_id = new_asset.id
            })
            .AddTag<TagUncompleted>()
            .Build();
        ae.AddSpecialItem(crafting_elixir);
        foreach (Entity ing in ingredients)
        {
            crafting_elixir.AddRelation(new CraftOccupyingRelation { item = ing });
            ing.AddTag<TagConsumed>();
        }
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerYear);
        pObject.data.set(ContentActorDataKeys.WaitingForElixirGeneration_string, new_asset.id);
        StayInside(pObject);
        return BehResult.RepeatStep;
    }

    private static void StayInside(Actor actor)
    {
        if (actor.beh_building_target != null)
        {
            actor.stayInBuilding(actor.beh_building_target);
        }
        else if (actor.inside_building != null)
        {
            actor.stayInBuilding(actor.inside_building);
        }
    }
}
