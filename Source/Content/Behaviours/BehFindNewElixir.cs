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

public class BehFindNewElixir : BehCity
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        if (ae.HasItem<CraftingElixir>()) return BehResult.Continue;

        var inv = (IHasInventory)ae;

        using var list = new ListPool<Entity>(inv.GetItems().Where(x=>x.Tags.Has<TagIngredient>()));
        if (!list.Any()) return BehResult.Stop;
        var count = ((IList<Entity>)list).Count;

        var ing_count = Math.Min(Toolbox.randomInt(1, (int)Mathf.Log(count) + 2), count);
        
        var ingredients = list.SampleOut(ing_count);
        var new_asset = Libraries.Manager.ElixirLibrary.NewElixir(ingredients, ae);
        if (new_asset == null)
        {
            return BehResult.Stop;
        }
        ModClass.LogInfo($"{ae} creates new elixir {new_asset.name_key}");
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
            ing.AddTag<TagOccupied>();
        }
        pObject.timer_action = Toolbox.randomFloat(TimeScales.SecPerMonth, TimeScales.SecPerYear);

        return BehResult.Continue;
    }
}