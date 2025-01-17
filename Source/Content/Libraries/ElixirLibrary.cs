using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class ElixirLibrary : DynamicAssetLibrary<ElixirAsset>
{
    public override void init()
    {
        base.init();
        ActorExtend.RegisterActionOnGetStats((ae, stat_id) =>
        {
            var items = ae.GetItems().Where(x => x.HasComponent<Elixir>() && x.Tags.Has<TagElixirStatusGain>());
            Entity elixir_entity = default;
            foreach (var item in items)
            {
                if (item.HasComponent<StatusOverwriteStats>())
                {
                    if (item.GetComponent<StatusOverwriteStats>().stats[stat_id] > 0)
                    {
                        elixir_entity = item;
                        break;
                    }
                }
                else if (item.HasComponent<StatusComponent>())
                {
                    if (item.GetComponent<StatusComponent>().Type.stats[stat_id] > 0)
                    {
                        elixir_entity = item;
                        break;
                    }
                }
            }

            if (elixir_entity.IsNull) return;
            if (ae.TryConsumeElixir(elixir_entity))
            {
                ae.Base.setStatsDirty();
                ae.Base.updateStats();
            }
        });
    }

    public ElixirAsset NewElixir(bool dynamic = true)
    {
        ElixirAsset asset = new()
        {
            id = Guid.NewGuid().ToString()
        };
        if (dynamic)
            add_dynamic(asset);
        else
            add(asset);

        return asset;
    }

    public ElixirAsset NewElixir(ElixirAsset reference, Entity[] ingredients, ActorExtend creator)
    {
        var asset = new ElixirAsset()
        {
            id = Guid.NewGuid().ToString()
        };
        
        var type = Toolbox.randomChance(0.1f) ? ElixirEffectType.DataGain : ElixirEffectType.StatusGain;

        asset.effect_type = type;
        asset.ingredients = new ElixirIngredientCheck[ingredients.Length];
        for (int i = 0; i < ingredients.Length; i++)
        {
            var ing_check = new ElixirIngredientCheck();

            var ing = ingredients[i];
            if (ing.TryGetComponent(out EntityName name))
            {
                ing_check.ingredient_name = name.value;
            }

            asset.ingredients[i] = ing_check;
        }
        // 生成丹药的服用检查和效果
        asset.seed_for_random_effect = Toolbox.randomInt(0, int.MaxValue);
        if (ElixirEffectGenerator.GenerateElixirActions(asset))
        {
            add_dynamic(asset);
            return asset;
        }
        return null;
    }

    public ElixirAsset GetRandom()
    {
        return list.GetRandom();
    }
}