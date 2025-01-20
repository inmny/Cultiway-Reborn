using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using UnityEngine;
using Random = System.Random;

namespace Cultiway.Content.Libraries;

public static class ElixirEffectGenerator
{
    private static Random _rng;
    public static bool GenerateElixirActions(ElixirAsset elixir)
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
            return GenerateDataGainElixirActions(elixir);
        }
        else if (elixir.effect_type == ElixirEffectType.StatusGain)
        {
            return GenerateStatusGainElixirActions(elixir);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    class ElixirEffect
    {
        public ElixirEffectType effect_type;
        public string effect_description;
        public Dictionary<string, float> bonus_stats;
    }
    private static bool GenerateStatusGainElixirActions(ElixirAsset elixir)
    {
        string[] param = new string[elixir.ingredients.Length];
        for (int i = 0; i < param.Length; i++)
        {
            param[i] = elixir.ingredients[i].ingredient_name;
        }

        var content = ElixirEffectJsonGenerator.Instance.GenerateName(param);
        if (string.IsNullOrEmpty(content)) return false;
        var effect = JsonConvert.DeserializeObject<ElixirEffect>(content);
        if (effect == null) return false;
        var name = ElixirNameGenerator.Instance.GenerateName(param.Prepend(effect.effect_description).ToArray());
        if (string.IsNullOrEmpty(name)) return false;
        elixir.name_key = name;
        elixir.description_key = effect.effect_description;

        var bonus_stats = new BaseStats();
        foreach (var kv in effect.bonus_stats)
        {
            bonus_stats[kv.Key] = kv.Value;
        }
        var status = StatusEffectAsset.StartBuild(elixir.id)
            .SetDuration(60)
            .SetStats(bonus_stats)
            .SetName(name+"药效")
            .SetDescription(effect.effect_description)
            .Build();
        elixir.effect_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir_component) =>
        {
            var status_effect = status.NewEntity();
            var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
            status_effect.GetComponent<AliveTimeLimit>().value *= multiplier;
            status_effect.AddComponent(new StatusStatsMultiplier()
            {
                Value = multiplier
            });
            ae.AddSharedStatus(status_effect);
        };
        
        
        elixir.consumable_check_action = null;
        return true;
    }

    private static bool GenerateDataGainElixirActions(ElixirAsset elixir)
    {
        elixir.consumable_check_action = null;
        elixir.effect_action = null;
        return false;
    }
}