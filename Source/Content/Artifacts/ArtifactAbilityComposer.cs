using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
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

        HashSet<string> selectedGroups = new(StringComparer.Ordinal);
        HashSet<string> selectedTags = new(StringComparer.Ordinal);
        HashSet<string> blockedTags = new(StringComparer.Ordinal);
        List<ArtifactAbilityInstance> abilities = new(candidates.Count);
        float capacity = ResolveManifestationCapacity(context);
        float spent = 0f;
        while (candidates.Count > 0)
        {
            candidates.RemoveAll(candidate => Conflicts(
                candidate.Ability,
                selectedGroups,
                selectedTags,
                blockedTags));
            if (candidates.Count == 0) break;

            Candidate candidate = candidates
                .OrderByDescending(item => ResolveSelectionValue(item, selectedTags))
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
            spent += cost;
            if (!string.IsNullOrEmpty(ability.exclusive_group)) selectedGroups.Add(ability.exclusive_group);
            AddTags(selectedTags, ability.tags);
            AddTags(blockedTags, ability.conflict_tags);
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

    private static float ResolveSelectionValue(Candidate candidate, HashSet<string> selectedTags)
    {
        int synergyCount = CountMatches(candidate.Ability.synergy_tags, selectedTags);
        float adjustedScore = candidate.Score * (1f + synergyCount * 0.12f);
        return adjustedScore / ResolveManifestationCost(candidate.Ability);
    }

    private static bool Conflicts(
        ArtifactAbilityAsset ability,
        HashSet<string> selectedGroups,
        HashSet<string> selectedTags,
        HashSet<string> blockedTags)
    {
        if (!string.IsNullOrEmpty(ability.exclusive_group) && selectedGroups.Contains(ability.exclusive_group))
        {
            return true;
        }
        return CountMatches(ability.tags, blockedTags) > 0 ||
               CountMatches(ability.conflict_tags, selectedTags) > 0;
    }

    private static int CountMatches(string[] values, HashSet<string> selected)
    {
        if (values == null || values.Length == 0 || selected.Count == 0) return 0;
        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (selected.Contains(values[i])) count++;
        }
        return count;
    }

    private static void AddTags(HashSet<string> target, string[] values)
    {
        if (values == null) return;
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }
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
