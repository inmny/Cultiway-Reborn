using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

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
            AddDynamic(asset);
        else
            add(asset);

        return asset;
    }

    public ElixirAsset NewElixir(Entity[] ingredients, ActorExtend creator)
    {
        var asset = new ElixirAsset()
        {
            id = Guid.NewGuid().ToString()
        };
        
        var type = Randy.randomChance(0.1f) ? ElixirEffectType.DataGain : ElixirEffectType.StatusGain;

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
        asset.has_recipe_semantic = true;
        asset.recipe_semantic = BuildRecipeSemantic(ingredients, creator);
        asset.base_level = new ItemLevel
        {
            Stage = asset.recipe_semantic.quality_stage,
            Level = asset.recipe_semantic.quality_level
        };
        // 生成丹药的服用检查和效果
        asset.seed_for_random_effect = Randy.randomInt(0, int.MaxValue);
        asset.effect_ready = false;
        asset.consumable_check_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir_component) => false; // 未生成前禁止服用
        AddDynamic(asset);
        ElixirEffectGenerator.GenerateElixirActions(asset);
        return asset;
    }

    public ElixirAsset GetRandom()
    {
        return list.GetRandom();
    }

    private static ElixirRecipeContext BuildRecipeSemantic(Entity[] ingredients, ActorExtend creator)
    {
        var snapshot = new ElixirRecipeContext
        {
            ingredient_count = ingredients?.Length ?? 0,
            primary_element_index = NamingRuleUtils.NoElement,
            secondary_element_index = NamingRuleUtils.NoElement,
            effect_hint = "body"
        };

        if (ingredients == null || ingredients.Length == 0)
        {
            return snapshot;
        }

        var elementValues = new float[8];
        var shapeCounts = new Dictionary<string, int>();
        var jindanCounts = new Dictionary<string, int>();
        var qualitySum = 0;
        var strengthSum = 0f;
        var contextCount = 0;

        foreach (var ingredient in ingredients)
        {
            var context = IngredientNameGenerator.CreateContext(ingredient);
            contextCount++;
            NamingRuleUtils.AddElementValue(elementValues, context.PrimaryElementIndex, context.PrimaryElementValue);
            NamingRuleUtils.AddElementValue(elementValues, context.SecondaryElementIndex, context.SecondaryElementValue * 0.5f);
            strengthSum += Mathf.Max(context.ElementStrength, 0f) + Mathf.Max(context.JindanStrength, 0f);
            qualitySum += context.QualityStage * 9 + context.QualityLevel;
            NamingRuleUtils.Increment(shapeCounts, context.ShapeId);
            NamingRuleUtils.Increment(jindanCounts, context.JindanId);
        }

        snapshot.primary_element_index = NamingRuleUtils.GetMaxIndex(elementValues, out snapshot.primary_element_value);
        snapshot.secondary_element_index = NamingRuleUtils.GetSecondMaxIndex(elementValues, snapshot.primary_element_index, out snapshot.secondary_element_value);
        snapshot.main_shape_id = NamingRuleUtils.PickTopKey(shapeCounts);
        snapshot.main_jindan_id = NamingRuleUtils.PickTopKey(jindanCounts);
        snapshot.strength = strengthSum / Mathf.Max(1, ingredients.Length);

        var avgQuality = contextCount > 0 ? qualitySum / contextCount : Mathf.Clamp(creator?.HasCultisys<Xian>() == true ? creator.GetCultisys<Xian>().CurrLevel : 0, 0, 35);
        snapshot.quality_stage = Mathf.Clamp(avgQuality / 9, 0, 3);
        snapshot.quality_level = Mathf.Clamp(avgQuality % 9, 0, 8);
        snapshot.effect_hint = ElixirEffectComposer.ResolveEffectHint(snapshot);
        return snapshot;
    }
}
