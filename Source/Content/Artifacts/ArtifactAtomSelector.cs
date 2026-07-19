using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 从自然炼器产生的材料语义中选择能够实际显化的 Atom。
/// 候选先按类别内显著度过滤，再共同竞争由材料品质和稳定度决定的动态表达容量。
/// </summary>
internal static class ArtifactAtomSelector
{
    private const float MinimumDominance = 0.35f;
    private const float MinimumSelectionCost = 0.25f;

    public static ArtifactAtomSelection[] Select(
        ArtifactRecipeContext context,
        ArtifactAtomAsset shapeAtom,
        IEnumerable<ArtifactAtomAsset> atomAssets)
    {
        List<ArtifactAtomSelection> selected = new();
        if (shapeAtom != null)
        {
            selected.Add(new ArtifactAtomSelection(
                shapeAtom,
                NormalizeStrength(Mathf.Max(1f, shapeAtom.ScoreFor(context)))));
        }

        Candidate[] eligible = atomAssets
            .Where(atom => atom.category != ArtifactAtomCategory.Shape)
            .Select(atom => new Candidate(atom, atom.ScoreFor(context)))
            .Where(candidate => candidate.Score >= candidate.Atom.minimum_score)
            .ToArray();
        if (eligible.Length == 0) return selected.ToArray();

        Dictionary<ArtifactAtomCategory, float> categoryMaxScores = eligible
            .GroupBy(candidate => candidate.Atom.category)
            .ToDictionary(group => group.Key, group => group.Max(candidate => candidate.Score));

        RankedCandidate[] ranked = eligible
            .Select(candidate => Rank(candidate, categoryMaxScores[candidate.Atom.category]))
            .Where(candidate => candidate.Dominance >= MinimumDominance)
            .OrderByDescending(candidate => candidate.Utility)
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Atom.priority)
            .ThenBy(candidate => candidate.Atom.category)
            .ThenBy(candidate => candidate.Atom.id, StringComparer.Ordinal)
            .ToArray();

        float remainingCapacity = ResolveCapacity(context);
        for (int i = 0; i < ranked.Length; i++)
        {
            RankedCandidate candidate = ranked[i];
            if (candidate.Cost > remainingCapacity) continue;

            selected.Add(new ArtifactAtomSelection(candidate.Atom, NormalizeStrength(candidate.Score)));
            remainingCapacity -= candidate.Cost;
        }
        return selected.ToArray();
    }

    private static RankedCandidate Rank(Candidate candidate, float categoryMaxScore)
    {
        float dominance = candidate.Score / Mathf.Max(categoryMaxScore, 0.0001f);
        float baseCost = Mathf.Max(MinimumSelectionCost, candidate.Atom.selection_cost);
        float cost = baseCost * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(dominance));
        return new RankedCandidate(candidate.Atom, candidate.Score, dominance, cost, candidate.Score / cost);
    }

    private static float ResolveCapacity(ArtifactRecipeContext context)
    {
        float qualityBudget = Mathf.Max(0f, context.material_data.quality_budget);
        return 2f +
               Mathf.Log(1f + qualityBudget, 2f) * 0.6f +
               Mathf.Max(0, context.quality_stage) * 0.5f +
               Mathf.Clamp01(context.material_data.stability) * 0.75f;
    }

    private static float NormalizeStrength(float score)
    {
        return 0.75f + Mathf.Log(1f + Mathf.Max(0f, score), 2f) * 0.65f;
    }

    private readonly struct Candidate
    {
        public readonly ArtifactAtomAsset Atom;
        public readonly float Score;

        public Candidate(ArtifactAtomAsset atom, float score)
        {
            Atom = atom;
            Score = score;
        }
    }

    private readonly struct RankedCandidate
    {
        public readonly ArtifactAtomAsset Atom;
        public readonly float Score;
        public readonly float Dominance;
        public readonly float Cost;
        public readonly float Utility;

        public RankedCandidate(
            ArtifactAtomAsset atom,
            float score,
            float dominance,
            float cost,
            float utility)
        {
            Atom = atom;
            Score = score;
            Dominance = dominance;
            Cost = cost;
            Utility = utility;
        }
    }
}
