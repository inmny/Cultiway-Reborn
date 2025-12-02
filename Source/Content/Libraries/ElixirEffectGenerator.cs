using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using Newtonsoft.Json;
using UnityEngine;
using strings;
using System.Threading.Tasks;
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
        RequestGeneration(elixir);
        return true;
    }

    internal class ElixirEffect
    {
        public ElixirEffectType effect_type;
        public string effect_description;
        public Dictionary<string, float> bonus_stats;
    }
    public class StatusEffectDraft
    {
        public string name;
        public string effect_description;
        public Dictionary<string, float> bonus_stats;
    }
    public class DataGainEffect
    {
        public string name;
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
    private static void RequestGeneration(ElixirAsset elixir)
    {
        elixir.effect_ready = false;
        elixir.effect_action = null;
        elixir.consumable_check_action ??= (ActorExtend ae, Entity elixir_entity, ref Elixir elixir_component) => false;
        _ = GenerateAsync(elixir);
    }

    private static async Task GenerateAsync(ElixirAsset elixir)
    {
        try
        {
            if (elixir.effect_type == ElixirEffectType.StatusGain)
            {
                var draft = await BuildStatusDraftAsync(elixir) ?? BuildStatusFallback(elixir);
                EventSystemHub.Publish(new ElixirEffectGeneratedEvent
                {
                    ElixirId = elixir.id,
                    EffectType = ElixirEffectType.StatusGain,
                    StatusDraft = draft
                });
            }
            else if (elixir.effect_type == ElixirEffectType.DataGain)
            {
                var draft = await BuildDataGainDraftAsync(elixir) ?? BuildDataGainFallback(elixir);
                EventSystemHub.Publish(new ElixirEffectGeneratedEvent
                {
                    ElixirId = elixir.id,
                    EffectType = ElixirEffectType.DataGain,
                    DataGainDraft = draft
                });
            }
        }
        catch (Exception e)
        {
            ModClass.LogErrorConcurrent(e.ToString());
            if (elixir.effect_type == ElixirEffectType.StatusGain)
            {
                EventSystemHub.Publish(new ElixirEffectGeneratedEvent
                {
                    ElixirId = elixir.id,
                    EffectType = ElixirEffectType.StatusGain,
                    StatusDraft = BuildStatusFallback(elixir)
                });
            }
            else if (elixir.effect_type == ElixirEffectType.DataGain)
            {
                EventSystemHub.Publish(new ElixirEffectGeneratedEvent
                {
                    ElixirId = elixir.id,
                    EffectType = ElixirEffectType.DataGain,
                    DataGainDraft = BuildDataGainFallback(elixir)
                });
            }
        }
    }

    private static async Task<StatusEffectDraft> BuildStatusDraftAsync(ElixirAsset elixir)
    {
        string[] param = elixir.ingredients?.Select(x => x.ingredient_name).ToArray() ?? Array.Empty<string>();
        var content = await ElixirEffectJsonGenerator.Instance.GenerateNameAsync(param);
        if (string.IsNullOrEmpty(content)) return null;
        ElixirEffect effect = JsonConvert.DeserializeObject<ElixirEffect>(content);
        if (effect == null) return null;
        var name = await ElixirNameGenerator.Instance.GenerateNameAsync(param.Prepend(effect.effect_description).ToArray());

        return new StatusEffectDraft
        {
            name = name,
            effect_description = effect.effect_description,
            bonus_stats = FilterAttributes(effect.bonus_stats)
        };
    }

    private static async Task<DataGainEffect> BuildDataGainDraftAsync(ElixirAsset elixir)
    {
        string[] param = elixir.ingredients?.Select(x => x.ingredient_name).ToArray() ?? Array.Empty<string>();
        var content = await ElixirDataGainJsonGenerator.Instance.GenerateNameAsync(param);
        if (string.IsNullOrEmpty(content)) return null;
        var effect = JsonConvert.DeserializeObject<DataGainEffect>(content);
        if (effect == null) return null;
        effect.attributes = FilterAttributes(effect.attributes);
        effect.fallback_attribute = FilterAttributes(effect.fallback_attribute);
        effect.traits = NormalizeTraits(effect.traits);
        effect.operations = NormalizeOperations(effect.operations);
        effect.max_stack = effect.max_stack <= 0 ? 1 : effect.max_stack;
    
        if (string.IsNullOrEmpty(effect.effect_description))
        {
            effect.effect_description = "平衡体魄的丹药";
        }
        if (string.IsNullOrEmpty(effect.name))
        {
            effect.name = "无名丹药";
        }
        if (string.IsNullOrEmpty(effect.chosen))
        {
            effect.chosen = "attribute";
        }
        if (string.IsNullOrEmpty(effect.effect_type))
        {
            effect.effect_type = ElixirEffectType.DataGain.ToString();
        }
        return effect;
    }

    private static StatusEffectDraft BuildStatusFallback(ElixirAsset elixir)
    {
        var baseName = elixir.ingredients == null
            ? "无名丹药"
            : string.Join("+", elixir.ingredients.Select(x => x.GetName()).Where(x => !string.IsNullOrEmpty(x)));
        return new StatusEffectDraft
        {
            name = baseName,
            effect_description = "服用后略有益处",
            bonus_stats = new Dictionary<string, float>
            {
                { S.health, 10 }
            }
        };
    }

    private static DataGainEffect BuildDataGainFallback(ElixirAsset elixir)
    {
        return new DataGainEffect
        {
            chosen = "attribute",
            effect_description = "淬炼血气，增强体魄",
            attributes = new Dictionary<string, float> { { S.health, 5 } },
            max_stack = 3
        };
    }

    public static void ApplyStatusDraft(ElixirAsset elixir, StatusEffectDraft draft)
    {
        if (elixir == null || draft == null) return;
        var name = string.IsNullOrEmpty(draft.name)
            ? (elixir.ingredients == null
                ? "无名丹药"
                : string.Join("+", elixir.ingredients.Select(x => x.GetName()).Where(x => !string.IsNullOrEmpty(x))))
            : draft.name;
        elixir.name_key = name;
        elixir.description_key = draft.effect_description;

        var bonus_stats = new BaseStats();
        foreach (var kv in draft.bonus_stats ?? new Dictionary<string, float>())
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (!AssetManager.base_stats_library.has(kv.Key)) continue;
            bonus_stats[kv.Key] = kv.Value;
        }
        var status = StatusEffectAsset.StartBuild(elixir.id)
            .SetDuration(60)
            .SetStats(bonus_stats)
            .SetName(name + "药效")
            .SetDescription(draft.effect_description)
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
        elixir.effect_ready = true;
    }

    public static void ApplyDataGainDraft(ElixirAsset elixir, DataGainEffect effect)
    {
        if (elixir == null || effect == null) return;
        var chosen = effect.chosen?.ToLowerInvariant();
        var attributes = FilterAttributes(effect.attributes ?? new Dictionary<string, float>());
        var fallbackAttributes = FilterAttributes(effect.fallback_attribute ?? new Dictionary<string, float>());
        var traits = NormalizeTraits(effect.traits ?? new List<string>());
        var operations = NormalizeOperations(effect.operations ?? new List<string>());
        var maxStack = effect.max_stack <= 0 ? 1 : effect.max_stack;

        elixir.name_key = effect.name;
        elixir.description_key = effect.effect_description;

        switch(chosen)
        {
            case "one_time":
                elixir.consumable_check_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) => 
                {
                    var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
                    if (!PreCheckOperations(ae, operations, effect.operation_args, multiplier)) return false;
                    return true;
                };
                elixir.effect_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
                {
                    var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
                    ApplyOperations(ae, operations, effect.operation_args, multiplier);
                };
                break;
            case "trait":
                elixir.effect_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
                {
                    var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
                    var applied = false;
                    applied = ApplyTraitGain(ae, traits);
                    if (!applied && fallbackAttributes.Count > 0)
                    {
                        applied = ApplyAttributeGain(ae, fallbackAttributes, multiplier);
                    }
                };
                break;
            case "attribute":
            default:
                elixir.consumable_check_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
                {
                    return GetDataGainStack(ae.Base, elixir_component.elixir_id) < maxStack;
                };
                elixir.effect_action = (ActorExtend ae, Entity _, ref Elixir elixir_component) =>
                {
                    var multiplier = Mathf.Log(elixir_component.value + 1) + 1;
                    var applied = false;
                    applied = ApplyAttributeGain(ae, attributes, multiplier);
                    if (applied) IncreaseDataGainStack(ae.Base, elixir_component.elixir_id);
                };
                break;
        }
        elixir.effect_ready = true;
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
    private static bool PreCheckOperations(ActorExtend ae, List<string> operations, Dictionary<string, string> opArgs, float multiplier)
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
            if (opAsset.PreCheck.Invoke(ae, multiplier, opArgs))
            {
                applied = true;
            }
        }
        return applied;
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
            if (!PreCheckOperations(ae, operations, opArgs, multiplier)) continue;
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
