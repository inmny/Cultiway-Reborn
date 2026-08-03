using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>解析角色当前形成快照、合并同族效果并同步有界运行时状态。</summary>
public static class CoreFormationEffectResolver
{
    /// <summary>角色当前核心形成快照及其强度上下文。</summary>
    public readonly struct FormationSource
    {
        /// <summary>角色当前境界唯一生效的成果快照。</summary>
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

    /// <summary>严格按角色当前仙道境界取得唯一生效成果，历史归档不参与运行时解析。</summary>
    public static bool TryGetFormation(ActorExtend actor, out FormationSource source)
    {
        if (actor == null || !actor.HasCultisys<Xian>())
        {
            source = default;
            return false;
        }

        int level = actor.GetCultisys<Xian>().CurrLevel;
        if (level >= XianLevels.Yuanying &&
            actor.TryGetComponent(out Yuanying yuanying) && yuanying.formation.IsValid)
        {
            source = new FormationSource(yuanying.formation, yuanying.stage, yuanying.strength);
            return true;
        }
        if (level == XianLevels.Jindan &&
            actor.TryGetComponent(out Jindan jindan) && jindan.formation.IsValid)
        {
            source = new FormationSource(jindan.formation, jindan.stage, jindan.strength);
            return true;
        }
        if (level == XianLevels.XianBase &&
            actor.TryGetComponent(out XianBase foundation) && foundation.formation.IsValid)
        {
            source = new FormationSource(
                foundation.formation,
                foundation.formation.refinement,
                foundation.formation.strength);
            return true;
        }
        if (level == XianLevels.QiRefinement &&
            actor.TryGetComponent(out QiRefinementState qi) && qi.formation.IsValid)
        {
            source = new FormationSource(qi.formation, qi.formation.refinement, qi.formation.strength);
            return true;
        }
        source = default;
        return false;
    }

    /// <summary>解析全部已显化效果，并让同效果族仅保留 rank 最高的定义。</summary>
    public static void Resolve(ActorExtend actor, IList<CoreFormationResolvedEffect> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (!TryGetFormation(actor, out FormationSource source))
        {
            output.Clear();
            return;
        }
        Resolve(source, output);
    }

    /// <summary>使用已取得的形成来源解析效果，避免同一次推进重复查询角色组件。</summary>
    internal static void Resolve(in FormationSource source, IList<CoreFormationResolvedEffect> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        output.Clear();
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
        if (output.Count > CoreFormationGrantRuntime.MaxEffects)
            while (output.Count > CoreFormationGrantRuntime.MaxEffects) output.RemoveAt(output.Count - 1);
    }

    /// <summary>确保角色运行时与当前合并效果族一致，并按效果族保留已有状态。</summary>
    public static bool Synchronize(ActorExtend actor, IList<CoreFormationResolvedEffect> resolved = null)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt()) return false;
        if (!TryGetFormation(actor, out FormationSource source))
        {
            RemoveGrant(actor);
            return false;
        }
        if (resolved == null)
        {
            using var effects = new ListPool<CoreFormationResolvedEffect>();
            Resolve(source, effects);
            return Synchronize(actor, source, effects);
        }

        return Synchronize(actor, source, resolved);
    }

    /// <summary>使用同一次推进取得的形成来源同步授予清单。</summary>
    internal static bool Synchronize(
        ActorExtend actor,
        in FormationSource source,
        IList<CoreFormationResolvedEffect> resolved)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt()) return false;
        if (resolved == null) throw new ArgumentNullException(nameof(resolved));
        if (resolved.Count == 0)
        {
            RemoveGrant(actor);
            return false;
        }

        CoreFormationGrantRuntime previous = actor.E.TryGetComponent(out CoreFormationGrantRuntime current)
            ? current
            : default;
        if (MatchesCurrentGrant(previous, source.Snapshot.signature, source.Stage, resolved)) return true;
        if (actor.E.HasComponent<CoreFormationGrantRuntime>()) ClearGrantedState(actor);

        var grants = new CoreFormationGrantedEffect[resolved.Count];
        for (var i = 0; i < resolved.Count; i++)
        {
            CoreFormationEffectDefinition definition = resolved[i].Definition;
            grants[i] = new CoreFormationGrantedEffect
            {
                family_id = definition.family_id,
                rank = definition.rank,
            };
        }

        var grant = new CoreFormationGrantRuntime
        {
            signature = source.Snapshot.signature,
            stage = source.Stage,
            effects = grants,
        };
        if (actor.E.HasComponent<CoreFormationGrantRuntime>())
            actor.E.GetComponent<CoreFormationGrantRuntime>() = grant;
        else
            actor.E.AddComponent(grant);
        return true;
    }

    /// <summary>撤销角色自身由当前形成维护的状态和形成技能冷却。</summary>
    internal static void ClearGrantedState(ActorExtend actor)
    {
        if (actor == null) return;
        CoreFormationStateService.ClearSelfStates(actor);
        CoreFormationSkills.ClearCooldowns(actor);
    }

    /// <summary>撤销形成授予并移除同步标记组件。</summary>
    private static void RemoveGrant(ActorExtend actor)
    {
        if (!actor.E.HasComponent<CoreFormationGrantRuntime>()) return;
        ClearGrantedState(actor);
        actor.E.RemoveComponent<CoreFormationGrantRuntime>();
    }

    /// <summary>判断已有授予清单是否已经与当前形成来源及解析结果完全一致。</summary>
    private static bool MatchesCurrentGrant(
        CoreFormationGrantRuntime grant,
        string signature,
        int stage,
        IList<CoreFormationResolvedEffect> resolved)
    {
        if (!string.Equals(grant.signature, signature, StringComparison.Ordinal) ||
            grant.stage != stage ||
            grant.effects == null ||
            grant.effects.Length != resolved.Count)
            return false;
        for (var i = 0; i < resolved.Count; i++)
        {
            CoreFormationEffectDefinition definition = resolved[i].Definition;
            CoreFormationGrantedEffect granted = grant.effects[i];
            if (!string.Equals(granted.family_id, definition.family_id, StringComparison.Ordinal) ||
                granted.rank != definition.rank)
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

    /// <summary>按境界、形成强度和原子贡献计算 0.4 至 2.5 的有界倍率。</summary>
    private static float ResolvePotency(
        FormationSource source,
        CoreFormationAtomState state,
        CoreFormationEffectDefinition definition)
    {
        float realm = source.Snapshot.realm switch
        {
            CoreFormationRealm.QiRefinement => 0.55f,
            CoreFormationRealm.Foundation => 0.75f,
            CoreFormationRealm.Jindan => 1f,
            _ => 1.25f
        };
        float strength = 1f + 0.12f * Mathf.Log(1f + Mathf.Clamp(source.Strength, 0f, 31f), 2f);
        float reference = Mathf.Max(0.01f, definition.reference_weight);
        float weight = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(state.weight / reference));
        return Mathf.Clamp(realm * strength * weight, 0.4f, 2.5f);
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
