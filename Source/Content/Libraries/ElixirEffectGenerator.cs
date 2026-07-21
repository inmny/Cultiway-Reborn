using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>把规范语义组合结果同步编译为可执行丹药效果。</summary>
public static class ElixirEffectGenerator
{
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

    public static void GenerateElixirActions(ElixirAsset elixir)
    {
        var composition = ElixirEffectComposer.Compose(elixir);
        elixir.name_key = composition.Name;
        elixir.description_key = composition.Description;
        switch (composition.EffectType)
        {
            case ElixirEffectType.StatusGain:
                ApplyStatus(elixir, composition);
                break;
            case ElixirEffectType.DataGain:
                ApplyDataGain(elixir, composition);
                break;
            case ElixirEffectType.Restore:
                ApplyRestore(elixir);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ApplyStatus(ElixirAsset elixir, ElixirEffectComposition composition)
    {
        var stats = FilterAttributes(composition.StatusStats);
        var bonusStats = new BaseStats();
        foreach (var entry in stats) bonusStats[entry.Key] = entry.Value;

        var status = StatusEffectAsset.StartBuild(elixir.id)
            .SetDuration(60f)
            .SetStats(bonusStats)
            .SetName(elixir.name_key + "药效")
            .SetDescription(elixir.description_key)
            .Build();
        elixir.craft_action += (_, elixirEntity, _) =>
            elixirEntity.AddComponent(new StatusOverwriteStats { stats = bonusStats });
        elixir.SetupStatusGain((ActorExtend actor, Entity _, ref Elixir component) =>
        {
            var statusEntity = status.NewEntity();
            var multiplier = PotencyMultiplier(component.value);
            statusEntity.GetComponent<AliveTimeLimit>().value *= multiplier;
            statusEntity.AddComponent(new StatusStatsMultiplier { Value = multiplier });
            actor.AddSharedStatus(statusEntity);
        });
        elixir.consumable_check_action = null;
    }

    private static void ApplyDataGain(ElixirAsset elixir, ElixirEffectComposition composition)
    {
        var attributes = FilterAttributes(composition.DataAttributes);
        var fallbackAttributes = FilterAttributes(composition.FallbackAttributes);
        var traits = NormalizeTraits(composition.DataTraits);
        var operations = composition.DataOperations
            .Where(operation => operation != null)
            .Distinct()
            .ToList();

        switch (composition.DataGainKind)
        {
            case ElixirDataGainKind.OneTime:
                elixir.consumable_check_action = (ActorExtend actor, Entity _, ref Elixir component) =>
                    CanApplyOperation(actor, operations, composition.OperationArgs,
                        PotencyMultiplier(component.value));
                elixir.SetupDataGain((ActorExtend actor, Entity _, ref Elixir component) =>
                    ApplyOperations(actor, operations, composition.OperationArgs,
                        PotencyMultiplier(component.value)));
                break;
            case ElixirDataGainKind.Trait:
                elixir.consumable_check_action = (ActorExtend actor, Entity _, ref Elixir component) =>
                    traits.Any(trait => !actor.Base.hasTrait(trait)) ||
                    fallbackAttributes.Count > 0 && GetDataGainStack(actor.Base, component.elixir_id) < 3;
                elixir.SetupDataGain((ActorExtend actor, Entity _, ref Elixir component) =>
                {
                    if (ApplyTraitGain(actor, traits)) return;
                    if (!ApplyAttributeGain(actor, fallbackAttributes, PotencyMultiplier(component.value))) return;
                    IncreaseDataGainStack(actor.Base, component.elixir_id);
                });
                break;
            default:
                elixir.consumable_check_action = (ActorExtend actor, Entity _, ref Elixir component) =>
                    GetDataGainStack(actor.Base, component.elixir_id) < 3;
                elixir.SetupDataGain((ActorExtend actor, Entity _, ref Elixir component) =>
                {
                    if (!ApplyAttributeGain(actor, attributes, PotencyMultiplier(component.value))) return;
                    IncreaseDataGainStack(actor.Base, component.elixir_id);
                });
                break;
        }
    }

    private static void ApplyRestore(ElixirAsset elixir)
    {
        elixir.consumable_check_action = (ActorExtend actor, Entity _, ref Elixir _) =>
            actor.HasCultisys<Xian>() && actor.GetCultisys<Xian>().wakan <
            actor.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit;
        elixir.SetupRestore((ActorExtend actor, Entity _, ref Elixir component) =>
            actor.RestoreWakan(component.value));
    }

    private static float PotencyMultiplier(float value)
    {
        return Mathf.Log(Mathf.Max(0f, value) + 1f) + 1f;
    }

    private static Dictionary<string, float> FilterAttributes(IReadOnlyDictionary<string, float> source)
    {
        Dictionary<string, float> result = new();
        if (source == null) return result;
        foreach (var entry in source)
        {
            if (string.IsNullOrEmpty(entry.Key) || entry.Value == 0f) continue;
            var key = AttributeAlias.TryGetValue(entry.Key, out var mapped) ? mapped : entry.Key;
            if (AssetManager.base_stats_library.has(key)) result[key] = entry.Value;
        }
        return result;
    }

    private static List<string> NormalizeTraits(IEnumerable<string> source)
    {
        return (source ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrEmpty(id) && AssetManager.traits.has(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool ApplyAttributeGain(
        ActorExtend actor,
        IReadOnlyDictionary<string, float> attributes,
        float multiplier)
    {
        if (attributes == null || attributes.Count == 0) return false;
        ref var record = ref actor.GetOrAddComponent<PermanentStats>();
        record.Stats ??= new BaseStats();
        foreach (var entry in attributes) record.Stats[entry.Key] += entry.Value * multiplier;
        actor.MarkCultiwayStatsDirty();
        return true;
    }

    private static bool ApplyTraitGain(ActorExtend actor, IEnumerable<string> traits)
    {
        var applied = false;
        foreach (var trait in traits)
        {
            if (actor.Base.hasTrait(trait)) continue;
            actor.Base.addTrait(trait);
            applied = true;
        }
        return applied;
    }

    private static bool CanApplyOperation(
        ActorExtend actor,
        IEnumerable<OperationAsset> operations,
        Dictionary<string, string> arguments,
        float multiplier)
    {
        foreach (var asset in operations)
        {
            if (asset?.Action == null || asset.PreCheck == null) continue;
            if (asset.PreCheck(actor, multiplier, arguments)) return true;
        }
        return false;
    }

    private static bool ApplyOperations(
        ActorExtend actor,
        IEnumerable<OperationAsset> operations,
        Dictionary<string, string> arguments,
        float multiplier)
    {
        var applied = false;
        foreach (var asset in operations)
        {
            if (asset?.Action == null || asset.PreCheck == null ||
                !asset.PreCheck(actor, multiplier, arguments)) continue;
            applied |= asset.Action(actor, multiplier, arguments);
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
