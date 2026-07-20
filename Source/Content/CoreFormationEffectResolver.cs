using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>解析角色当前形成快照、合并同族效果并同步有界运行时状态。</summary>
public static class CoreFormationEffectResolver
{
    /// <summary>角色当前核心形成快照及其强度上下文。</summary>
    public readonly struct FormationSource
    {
        /// <summary>金丹或元婴的组合快照。</summary>
        public readonly CoreFormationSnapshot Snapshot;

        /// <summary>当前显化阶段。</summary>
        public readonly int Stage;

        /// <summary>形成组件保存的累计强度。</summary>
        public readonly float Strength;

        /// <summary>创建一份不可变形成来源。</summary>
        public FormationSource(CoreFormationSnapshot snapshot, int stage, float strength)
        {
            Snapshot = snapshot;
            Stage = stage;
            Strength = strength;
        }
    }

    /// <summary>取得角色当前有效的元婴或金丹形成来源，元婴优先。</summary>
    public static bool TryGetFormation(ActorExtend actor, out FormationSource source)
    {
        if (actor != null && actor.TryGetComponent(out Yuanying yuanying) && yuanying.formation.IsValid)
        {
            source = new FormationSource(yuanying.formation, yuanying.stage, yuanying.strength);
            return true;
        }
        if (actor != null && actor.TryGetComponent(out Jindan jindan) && jindan.formation.IsValid)
        {
            source = new FormationSource(jindan.formation, jindan.stage, jindan.strength);
            return true;
        }
        source = default;
        return false;
    }

    /// <summary>解析全部已显化效果，并让同效果族仅保留 rank 最高的定义。</summary>
    public static void Resolve(ActorExtend actor, IList<CoreFormationResolvedEffect> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        output.Clear();
        if (!TryGetFormation(actor, out FormationSource source)) return;

        CoreFormationAtomState[] states = source.Snapshot.atoms ?? [];
        for (var i = 0; i < states.Length; i++)
        {
            CoreFormationAtomState state = states[i];
            if (!state.IsActive(source.Stage)) continue;
            CoreFormationAtomAsset atom = Libraries.Manager.CoreFormationAtomLibrary.get(state.atom_id);
            if (atom == null) continue;
            CoreFormationEffectDefinition[] definitions = atom.effects ?? [];
            for (var j = 0; j < definitions.Length; j++)
            {
                CoreFormationEffectDefinition definition = definitions[j];
                if (definition == null || string.IsNullOrEmpty(definition.family_id)) continue;
                var resolved = new CoreFormationResolvedEffect(
                    definition,
                    atom,
                    state,
                    ResolvePotency(source, state, definition));
                Merge(output, resolved);
            }
        }

        SortByFamily(output);
        if (output.Count > CoreFormationEffectRuntime.MaxEntries)
            while (output.Count > CoreFormationEffectRuntime.MaxEntries) output.RemoveAt(output.Count - 1);
    }

    /// <summary>确保角色运行时与当前合并效果族一致，并按效果族保留已有状态。</summary>
    public static bool Synchronize(ActorExtend actor, IList<CoreFormationResolvedEffect> resolved = null)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt()) return false;
        if (resolved == null)
        {
            using var effects = new ListPool<CoreFormationResolvedEffect>();
            Resolve(actor, effects);
            return Synchronize(actor, effects);
        }
        if (!TryGetFormation(actor, out FormationSource source) || resolved.Count == 0)
        {
            if (actor.E.HasComponent<CoreFormationEffectRuntime>())
                actor.E.RemoveComponent<CoreFormationEffectRuntime>();
            return false;
        }

        CoreFormationEffectRuntime previous = actor.E.TryGetComponent(out CoreFormationEffectRuntime current)
            ? current
            : default;
        if (MatchesCurrentRuntime(previous, source.Snapshot.signature, resolved)) return true;

        var entries = new CoreFormationEffectRuntimeEntry[resolved.Count];
        for (var i = 0; i < resolved.Count; i++)
        {
            CoreFormationResolvedEffect effect = resolved[i];
            int previousIndex = previous.FindIndex(effect.Definition.family_id);
            CoreFormationEffectRuntimeEntry entry = previousIndex >= 0
                ? previous.entries[previousIndex]
                : new CoreFormationEffectRuntimeEntry { family_id = effect.Definition.family_id };
            entry.family_id = effect.Definition.family_id;
            entry.rank = effect.Definition.rank;
            entries[i] = entry;
        }

        var runtime = new CoreFormationEffectRuntime
        {
            signature = source.Snapshot.signature,
            entries = entries,
        };
        if (actor.E.HasComponent<CoreFormationEffectRuntime>())
            actor.E.GetComponent<CoreFormationEffectRuntime>() = runtime;
        else
            actor.E.AddComponent(runtime);
        return true;
    }

    /// <summary>判断已有运行时是否已经与当前形成签名及效果族解析结果一致。</summary>
    private static bool MatchesCurrentRuntime(
        CoreFormationEffectRuntime runtime,
        string signature,
        IList<CoreFormationResolvedEffect> resolved)
    {
        if (!string.Equals(runtime.signature, signature, StringComparison.Ordinal) ||
            runtime.entries == null || runtime.entries.Length != resolved.Count)
            return false;
        for (var i = 0; i < resolved.Count; i++)
        {
            CoreFormationEffectDefinition definition = resolved[i].Definition;
            CoreFormationEffectRuntimeEntry entry = runtime.entries[i];
            if (!string.Equals(entry.family_id, definition.family_id, StringComparison.Ordinal) ||
                entry.rank != definition.rank)
                return false;
        }
        return true;
    }

    /// <summary>解析指定效果族当前生效的定义。</summary>
    public static bool TryResolveFamily(
        ActorExtend actor,
        string familyId,
        out CoreFormationResolvedEffect resolved)
    {
        using var effects = new ListPool<CoreFormationResolvedEffect>();
        Resolve(actor, effects);
        for (var i = 0; i < effects.Count; i++)
        {
            if (!string.Equals(effects[i].Definition.family_id, familyId, StringComparison.Ordinal)) continue;
            Synchronize(actor, effects);
            resolved = effects[i];
            return true;
        }
        resolved = default;
        return false;
    }

    /// <summary>按境界、品质、形成强度和原子贡献计算 0.75 至 2.5 的有界倍率。</summary>
    private static float ResolvePotency(
        FormationSource source,
        CoreFormationAtomState state,
        CoreFormationEffectDefinition definition)
    {
        float realm = source.Snapshot.realm == CoreFormationRealm.Yuanying ? 1.25f : 1f;
        float quality = 1f + 0.1f * Mathf.Clamp(source.Snapshot.quality, 0, 3);
        float strength = 1f + 0.12f * Mathf.Log(1f + Mathf.Clamp(source.Strength, 0f, 31f), 2f);
        float reference = Mathf.Max(0.01f, definition.reference_weight);
        float weight = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(state.weight / reference));
        return Mathf.Clamp(realm * quality * strength * weight, 0.75f, 2.5f);
    }

    /// <summary>把新解析结果合并进列表，同族优先 rank，其次优先效果倍率。</summary>
    private static void Merge(IList<CoreFormationResolvedEffect> output, CoreFormationResolvedEffect candidate)
    {
        for (var i = 0; i < output.Count; i++)
        {
            CoreFormationResolvedEffect current = output[i];
            if (!string.Equals(current.Definition.family_id, candidate.Definition.family_id,
                    StringComparison.Ordinal)) continue;
            if (candidate.Definition.rank > current.Definition.rank ||
                candidate.Definition.rank == current.Definition.rank && candidate.Potency > current.Potency)
                output[i] = candidate;
            return;
        }
        output.Add(candidate);
    }

    /// <summary>按效果族 ID 对有界结果执行稳定原地插入排序。</summary>
    private static void SortByFamily(IList<CoreFormationResolvedEffect> output)
    {
        for (var i = 1; i < output.Count; i++)
        {
            CoreFormationResolvedEffect value = output[i];
            var write = i - 1;
            while (write >= 0 && string.Compare(
                       output[write].Definition.family_id,
                       value.Definition.family_id,
                       StringComparison.Ordinal) > 0)
            {
                output[write + 1] = output[write];
                write--;
            }
            output[write + 1] = value;
        }
    }
}
