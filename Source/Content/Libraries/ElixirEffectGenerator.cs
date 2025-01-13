using System;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;

namespace Cultiway.Content.Libraries;

public static class ElixirEffectGenerator
{
    private static Random _rng;
    public static void GenerateElixirActions(ElixirAsset elixir)
    {
        _rng = new Random(elixir.seed_for_random_effect);
        elixir.craft_action += (crafter, elixir_entity, ingredients) =>
        {
            var strength = 0f;
            for (int i = 0; i < ingredients.Length; i++)
            {
                var ing = ingredients[i];
                var single_strength = 1f;
                if (ing.TryGetComponent(out Jindan jindan))
                {
                    strength *= (1 + jindan.strength);
                }
                if (ing.TryGetComponent(out ElementRoot element_root))
                {
                    strength *= (1+element_root.GetStrength());
                }
                strength += single_strength;
            }
            elixir_entity.GetComponent<Elixir>().value = strength;
        };
        if (elixir.effect_type == ElixirEffectType.DataGain)
        {
            GenerateDataGainElixirActions(elixir);
        }
        else if (elixir.effect_type == ElixirEffectType.StatusGain)
        {
            GenerateStatusGainElixirActions(elixir);
        }
        else
        {
            throw new NotImplementedException();
        }
    }
    
    private static void GenerateStatusGainElixirActions(ElixirAsset elixir)
    {
        elixir.consumable_check_action = null;
        elixir.effect_action = null;
    }

    private static void GenerateDataGainElixirActions(ElixirAsset elixir)
    {
        elixir.consumable_check_action = null;
        elixir.effect_action = null;
    }
}