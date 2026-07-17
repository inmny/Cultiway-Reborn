using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 炼器阶段生成的能力定义与对应初始运行状态。
/// </summary>
public sealed class ArtifactAbilityComposition
{
    public ArtifactAbilitySet ability_set;
    public ArtifactAbilityRuntime runtime;
}

/// <summary>
/// 根据器形、atom 和配方语义，从注册表中组合所有满足条件的法器能力。
/// </summary>
public static class ArtifactAbilityComposer
{
    public static ArtifactAbilityComposition Compose(
        ArtifactRecipeContext recipe,
        ArtifactShapeAsset shape,
        ArtifactAtomSelection[] atoms,
        string compositionKey)
    {
        ArtifactAbilityComposeContext context = new()
        {
            recipe = recipe,
            shape = shape,
            atoms = atoms,
            composition_key = compositionKey,
            scales = ArtifactAbilityScales.Resolve(recipe),
        };
        List<Candidate> candidates = Cultiway.Content.Libraries.Manager.ArtifactAbilityLibrary.All
            .Select(ability => new Candidate(
                ability,
                ability.ScoreFor(context),
                StableTieBreak(compositionKey, ability.id)))
            .Where(candidate => candidate.Score > 0f && candidate.Score >= candidate.Ability.minimum_score)
            .ToList();

        HashSet<ArtifactAbilityExclusivityKey> selectedGroups = new();
        HashSet<SemanticAsset> selectedSemantics = new();
        List<ArtifactAbilityAsset> selectedAssets = new();
        List<ArtifactAbilityInstance> abilities = new(candidates.Count);
        float capacity = ResolveManifestationCapacity(context);
        float spent = 0f;
        while (candidates.Count > 0)
        {
            candidates.RemoveAll(candidate => Conflicts(
                candidate.Ability,
                selectedGroups,
                selectedSemantics,
                selectedAssets));
            if (candidates.Count == 0) break;

            Candidate candidate = candidates
                .OrderByDescending(item => ResolveSelectionValue(item, selectedSemantics))
                .ThenByDescending(item => item.Score)
                .ThenByDescending(item => item.TieBreak)
                .ThenBy(item => item.Ability.id, StringComparer.Ordinal)
                .First();
            candidates.Remove(candidate);

            ArtifactAbilityAsset ability = candidate.Ability;
            float cost = ResolveManifestationCost(ability);
            if (abilities.Count > 0 && spent + cost > capacity)
            {
                continue;
            }

            abilities.Add(ability.ComposeInstance(context));
            selectedAssets.Add(ability);
            spent += cost;
            if (!ability.exclusivity.IsEmpty) selectedGroups.Add(ability.exclusivity);
            selectedSemantics.UnionWith(CollectSemantics(ability));
        }

        ArtifactAbilitySet abilitySet = new() { abilities = abilities.ToArray() };
        return new ArtifactAbilityComposition
        {
            ability_set = abilitySet,
            runtime = ArtifactAbilityRuntime.CreateInitial(abilitySet),
        };
    }

    /// <summary>
    /// 材料数量不限制能力数量；品质、灵性、承载与稳定度共同决定可稳定显化多少复杂能力。
    /// </summary>
    private static float ResolveManifestationCapacity(ArtifactAbilityComposeContext context)
    {
        int quality = context.recipe.quality_stage * 9 + context.recipe.quality_level;
        return 1.75f +
               quality * 0.085f +
               Mathf.Sqrt(Mathf.Max(0f, context.GetTrait(ArtifactMaterialTraits.Spirituality))) * 0.55f +
               Mathf.Sqrt(Mathf.Max(0f, context.GetTrait(ArtifactMaterialTraits.Capacity))) * 0.5f +
               Mathf.Clamp01(context.GetTrait(ArtifactMaterialTraits.Stability)) * 0.55f;
    }

    private static float ResolveManifestationCost(ArtifactAbilityAsset ability)
    {
        return Mathf.Max(
            0.25f,
            ability.manifestation_cost + ability.control_complexity * 0.35f + ability.thread_cost * 0.12f);
    }

    private static float ResolveSelectionValue(Candidate candidate, HashSet<SemanticAsset> selectedSemantics)
    {
        int synergyCount = CountMatches(candidate.Ability.synergy_conditions, selectedSemantics);
        float adjustedScore = candidate.Score * (1f + synergyCount * 0.12f);
        return adjustedScore / ResolveManifestationCost(candidate.Ability);
    }

    private static bool Conflicts(
        ArtifactAbilityAsset ability,
        HashSet<ArtifactAbilityExclusivityKey> selectedGroups,
        HashSet<SemanticAsset> selectedSemantics,
        List<ArtifactAbilityAsset> selectedAssets)
    {
        if (!ability.exclusivity.IsEmpty && selectedGroups.Contains(ability.exclusivity))
        {
            return true;
        }
        if (CountMatches(ability.conflict_conditions, selectedSemantics) > 0) return true;

        var candidateSemantics = CollectSemantics(ability);
        for (var i = 0; i < selectedAssets.Count; i++)
        {
            if (CountMatches(selectedAssets[i].conflict_conditions, candidateSemantics) > 0) return true;
        }
        return false;
    }

    private static int CountMatches(
        SemanticQueryExpression[] conditions,
        HashSet<SemanticAsset> selected)
    {
        if (conditions == null || conditions.Length == 0 || selected.Count == 0) return 0;
        int count = 0;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].Matches(selected, ModClass.L.SemanticLibrary)) count++;
        }
        return count;
    }

    private static HashSet<SemanticAsset> CollectSemantics(ArtifactAbilityAsset ability)
    {
        HashSet<SemanticAsset> result = new();
        ability.semantics.CollectExpanded(ModClass.L.SemanticLibrary, result);

        if (ability.use_profile.offensive > 0f) result.Add(SkillSemantics.Role.Offensive);
        if (ability.use_profile.defensive > 0f) result.Add(SkillSemantics.Role.Defensive);
        if (ability.use_profile.support > 0f) result.Add(SkillSemantics.Role.Support);
        if (ability.use_profile.production > 0f) result.Add(SkillSemantics.Role.Production);
        return result;
    }

    private static int StableTieBreak(string compositionKey, string abilityId)
    {
        unchecked
        {
            int hash = 17;
            string value = $"{compositionKey}|{abilityId}";
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }

    private readonly struct Candidate : IEquatable<Candidate>
    {
        public readonly ArtifactAbilityAsset Ability;
        public readonly float Score;
        public readonly int TieBreak;

        public Candidate(ArtifactAbilityAsset ability, float score, int tieBreak)
        {
            Ability = ability;
            Score = score;
            TieBreak = tieBreak;
        }

        public bool Equals(Candidate other)
        {
            return Ability == other.Ability;
        }

        public override bool Equals(object obj)
        {
            return obj is Candidate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Ability?.id?.GetHashCode() ?? 0;
        }
    }
}
