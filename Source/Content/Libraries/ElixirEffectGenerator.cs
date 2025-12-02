using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;
using UnityEngine;
using strings;
using Random = System.Random;

namespace Cultiway.Content.Libraries;

public static class ElixirEffectGenerator
{
    private static Random _rng;
    private static bool _dataGainStatHookRegistered;
    private static readonly Dictionary<string, string> AttributeAlias =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "hp_max", S.health },
            { "health", S.health },
            { "max_health", S.health },
            { "intelligence", S.intelligence },
            { "wisdom", S.intelligence },
            { "lifespan", S.lifespan },
            { "longevity", S.lifespan }
        };

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

    internal class ElixirEffect
    {
        public ElixirEffectType effect_type;
        public string effect_description;
        public Dictionary<string, float> bonus_stats;
    }
    internal class DataGainEffect
    {
        public string effect_type;
        public string chosen;
        public string effect_description;
        public Dictionary<string, float> attributes;
        public int max_stack = 1;
        public List<string> traits;
        public Dictionary<string, float> fallback_attribute;
        public List<string> operations;
        public Dictionary<string, string> operation_args;
    }
    [Hotfixable]
    private static bool GenerateStatusGainElixirActions(ElixirAsset elixir)
    {
        string[] param = new string[elixir.ingredients.Length];
        for (int i = 0; i < param.Length; i++)
        {
            param[i] = elixir.ingredients[i].ingredient_name;
        }

        var content = ElixirEffectJsonGenerator.Instance.GenerateName(param);
        if (string.IsNullOrEmpty(content)) return false;
        ElixirEffect effect = JsonConvert.DeserializeObject<ElixirEffect>(content);
        if (effect == null) return false;
        var name = ElixirNameGenerator.Instance.GenerateName(param.Prepend(effect.effect_description).ToArray());
        elixir.name_key = name;
        elixir.description_key = effect.effect_description;

        var bonus_stats = new BaseStats();
        foreach (var kv in effect.bonus_stats)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (!AssetManager.base_stats_library.has(kv.Key)) continue;
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
        string[] param = new string[elixir.ingredients.Length];
        for (int i = 0; i < param.Length; i++)
        {
            param[i] = elixir.ingredients[i].ingredient_name;
        }

        var content = ElixirDataGainJsonGenerator.Instance.GenerateName(param);
        if (string.IsNullOrEmpty(content)) return false;
        DataGainEffect effect = JsonConvert.DeserializeObject<DataGainEffect>(content);
        if (effect == null) return false;

        var name = ElixirNameGenerator.Instance.GenerateName(param.Prepend(effect.effect_description).ToArray());
        elixir.name_key = name;
        elixir.description_key = effect.effect_description;

        var chosen = effect.chosen?.ToLowerInvariant();
        var attributes = FilterAttributes(effect.attributes);
        var fallbackAttributes = FilterAttributes(effect.fallback_attribute);
        var traits = NormalizeTraits(effect.traits);
        var operations = NormalizeOperations(effect.operations);
        var maxStack = effect.max_stack <= 0 ? 1 : effect.max_stack;

        elixir.consumable_check_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
        {
            if (chosen == "attribute" && maxStack > 0)
            {
                return GetDataGainStack(ae.Base, elixir_component.elixir_id) < maxStack;
            }

            return true;
        };
        elixir.effect_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
        {
            var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
            var applied = false;
            if (chosen == "attribute")
            {
                applied = ApplyAttributeGain(ae, attributes, multiplier);
                if (applied) IncreaseDataGainStack(ae.Base, elixir_component.elixir_id);
            }
            else if (chosen == "trait")
            {
                applied = ApplyTraitGain(ae, traits);
                if (!applied && fallbackAttributes.Count > 0)
                {
                    applied = ApplyAttributeGain(ae, fallbackAttributes, multiplier);
                }
            }
            else if (chosen == "one_time")
            {
                applied = ApplyOperations(ae, operations, effect.operation_args, multiplier);
            }
            else
            {
                if (attributes.Count > 0)
                {
                    applied = ApplyAttributeGain(ae, attributes, multiplier);
                    if (applied) IncreaseDataGainStack(ae.Base, elixir_component.elixir_id);
                }
            }

            if (applied)
            {
                ae.Base.setStatsDirty();
            }
        };
        return true;
    }

    private static Dictionary<string, float> FilterAttributes(Dictionary<string, float> source)
    {
        var res = new Dictionary<string, float>();
        if (source == null) return res;
        foreach (var kv in source)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (kv.Value == 0) continue;
            var key = kv.Key;
            if (AttributeAlias.TryGetValue(kv.Key, out var mapped))
            {
                key = mapped;
            }

            if (!AssetManager.base_stats_library.has(key)) continue;
            res[key] = kv.Value;
        }

        return res;
    }

    private static List<string> NormalizeTraits(List<string> source)
    {
        var list = new List<string>();
        if (source == null) return list;
        foreach (var trait in source)
        {
            if (string.IsNullOrEmpty(trait)) continue;
            var id = trait.Trim();
            if (!AssetManager.traits.has(id)) continue;
            if (list.Contains(id)) continue;
            list.Add(id);
        }

        return list;
    }

    private static List<string> NormalizeOperations(List<string> source)
    {
        var list = new List<string>();
        if (source == null) return list;
        var opLib = ModClass.L.OperationLibrary;
        foreach (var op in source)
        {
            if (string.IsNullOrEmpty(op)) continue;
            var id = op.ToLowerInvariant();
            if (opLib == null || !opLib.has(id)) continue;
            if (list.Contains(id)) continue;
            list.Add(id);
        }

        return list;
    }

    private static bool ApplyAttributeGain(ActorExtend ae, Dictionary<string, float> attributes, float multiplier)
    {
        if (attributes == null || attributes.Count == 0) return false;
        ref var record = ref ae.GetOrAddComponent<PermanentStats>();
        record.Stats ??= new BaseStats();
        foreach (var kv in attributes)
        {
            record.Stats[kv.Key] += kv.Value * multiplier;
        }

        return true;
    }

    private static bool ApplyTraitGain(ActorExtend ae, List<string> traits)
    {
        if (traits == null || traits.Count == 0) return false;
        var added = false;
        foreach (var traitId in traits)
        {
            if (ae.Base.hasTrait(traitId)) continue;
            ae.Base.addTrait(traitId);
            added = true;
        }

        return added;
    }

    private static bool ApplyOperations(ActorExtend ae, List<string> operations, Dictionary<string, string> opArgs, float multiplier)
    {
        if (operations == null || operations.Count == 0) return false;
        var opLib = ModClass.L.OperationLibrary;
        if (opLib == null) return false;
        var applied = false;
        foreach (var operation in operations)
        {
            if (!opLib.has(operation)) continue;
            var opAsset = opLib.get(operation);
            if (opAsset == null || opAsset.Action == null) continue;
            if (!opAsset.PreCheck.Invoke(ae, multiplier, opArgs)) continue;
            if (opAsset.Action.Invoke(ae, multiplier, opArgs))
            {
                applied = true;
            }
        }

        return applied;
    }

    private static int GetDataGainStack(Actor actor, string elixirId)
    {
        actor.data.get(ContentActorDataKeys.ElixirDataGainStackPrefix + elixirId, out int count, 0);
        return count;
    }

    private static void IncreaseDataGainStack(Actor actor, string elixirId)
    {
        var key = ContentActorDataKeys.ElixirDataGainStackPrefix + elixirId;
        actor.data.get(key, out int count, 0);
        actor.data.set(key, count + 1);
    }
}
