using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 为自然炼器选择一组边际收益为正的材料。算法没有材料数量上限。
/// </summary>
public static class ArtifactIngredientPlanner
{
    public static Entity[] Select(Actor crafter, IReadOnlyList<Entity> available)
    {
        Candidate[] candidates = available
            .Select(entity => new Candidate(entity, ArtifactMaterialSemantics.Capture(entity)))
            .OrderByDescending(candidate => StandaloneValue(candidate.Record))
            .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Entity.Id)
            .ToArray();
        if (candidates.Length == 0) return [];

        List<Entity> selected = [candidates[0].Entity];
        HashSet<int> selectedIds = [candidates[0].Entity.Id];
        float divineSense = Mathf.Max(0f, crafter.stats[nameof(WorldboxGame.BaseStats.DivineSense)]);
        float complexityTolerance = 1f + Mathf.Log(1f + divineSense / 16f, 2f) * 0.16f;

        while (selected.Count < candidates.Length)
        {
            ArtifactRecipeContext current = ArtifactMaterialSemantics.Build(selected);
            int bestIndex = -1;
            float bestGain = 0f;
            for (int i = 0; i < candidates.Length; i++)
            {
                Candidate candidate = candidates[i];
                if (selectedIds.Contains(candidate.Entity.Id)) continue;

                List<Entity> trial = new(selected) { candidate.Entity };
                ArtifactRecipeContext next = ArtifactMaterialSemantics.Build(trial);
                float gain = MarginalValue(current, next, candidate.Record, selected.Count, complexityTolerance);
                if (gain < bestGain - 0.0001f) continue;
                if (Mathf.Abs(gain - bestGain) <= 0.0001f && bestIndex >= 0 &&
                    CompareStable(candidate, candidates[bestIndex]) >= 0)
                {
                    continue;
                }
                bestGain = gain;
                bestIndex = i;
            }

            if (bestIndex < 0 || bestGain <= 0f) break;
            selected.Add(candidates[bestIndex].Entity);
            selectedIds.Add(candidates[bestIndex].Entity.Id);
        }
        return selected.ToArray();
    }

    private static float MarginalValue(
        ArtifactRecipeContext current,
        ArtifactRecipeContext next,
        ArtifactMaterialRecord candidate,
        int selectedCount,
        float complexityTolerance)
    {
        float qualityValue = 0.8f + candidate.quality / 18f;
        float shapeNovelty = current.material_data.CountShape(candidate.shape_id) == 0 ? 0.55f : 0f;
        float sourceNovelty = current.material_data.CountSource(candidate.source_asset_id) == 0 ? 0.25f : 0f;
        float affinity = ElementAffinity(current, candidate) * 0.45f;
        float stabilityChange = (next.material_data.stability - current.material_data.stability) * 3f;
        float complexityCost = selectedCount * 0.22f / complexityTolerance;
        return qualityValue + shapeNovelty + sourceNovelty + affinity + stabilityChange - complexityCost;
    }

    private static float ElementAffinity(ArtifactRecipeContext current, ArtifactMaterialRecord candidate)
    {
        float[] currentElements =
        [
            current.GetTrait(ArtifactMaterialTraits.Iron),
            current.GetTrait(ArtifactMaterialTraits.Wood),
            current.GetTrait(ArtifactMaterialTraits.Water),
            current.GetTrait(ArtifactMaterialTraits.Fire),
            current.GetTrait(ArtifactMaterialTraits.Earth),
            current.GetTrait(ArtifactMaterialTraits.Neg),
            current.GetTrait(ArtifactMaterialTraits.Pos),
            current.GetTrait(ArtifactMaterialTraits.Entropy),
        ];
        float[] candidateElements =
        [
            candidate.iron,
            candidate.wood,
            candidate.water,
            candidate.fire,
            candidate.earth,
            candidate.neg,
            candidate.pos,
            candidate.entropy,
        ];
        float dot = 0f;
        float currentMagnitude = 0f;
        float candidateMagnitude = 0f;
        for (int i = 0; i < currentElements.Length; i++)
        {
            dot += currentElements[i] * candidateElements[i];
            currentMagnitude += currentElements[i] * currentElements[i];
            candidateMagnitude += candidateElements[i] * candidateElements[i];
        }
        if (currentMagnitude <= 0.0001f || candidateMagnitude <= 0.0001f) return 0f;
        return dot / Mathf.Sqrt(currentMagnitude * candidateMagnitude);
    }

    private static float StandaloneValue(ArtifactMaterialRecord record)
    {
        return record.quality +
               (record.iron + record.wood + record.water + record.fire + record.earth +
                record.neg + record.pos + record.entropy) * 0.2f +
               (record.shen + record.jindan_strength) * 0.5f;
    }

    private static int CompareStable(Candidate left, Candidate right)
    {
        int key = string.CompareOrdinal(left.Key, right.Key);
        return key != 0 ? key : left.Entity.Id.CompareTo(right.Entity.Id);
    }

    private readonly struct Candidate
    {
        public readonly Entity Entity;
        public readonly ArtifactMaterialRecord Record;
        public readonly string Key;

        public Candidate(Entity entity, ArtifactMaterialRecord record)
        {
            Entity = entity;
            Record = record;
            Key = record.GetIdentityKey();
        }
    }
}
