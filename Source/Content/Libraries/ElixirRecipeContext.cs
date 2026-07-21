using System;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 运行时丹方的紧凑语义上下文。只保存直接语义贡献，查询时再通过语义库展开。
/// </summary>
public struct ElixirRecipeContext
{
    public int ingredient_count;
    public int quality_stage;
    public SemanticContribution[] semantics;

    public bool HasSemantics => semantics is { Length: > 0 };

    public SemanticScore GetSemanticScore(SemanticAsset target)
    {
        if (target == null || semantics == null || semantics.Length == 0)
            return new SemanticScore(0f, 0f);

        var positive = 0f;
        var negative = 0f;
        var library = ModClass.L.SemanticLibrary;
        for (var i = 0; i < semantics.Length; i++)
        {
            var contribution = semantics[i];
            if (!library.TryResolve(contribution.semantic_id, out var origin)) continue;

            var expansion = library.Expand(origin);
            for (var j = 0; j < expansion.Count; j++)
            {
                if (expansion[j].semantic != target) continue;
                var value = contribution.strength * contribution.confidence * expansion[j].strength;
                if (contribution.polarity == SemanticPolarity.Positive) positive += value;
                else negative += value;
                break;
            }
        }

        return new SemanticScore(positive, negative);
    }

    public bool HasSemantic(SemanticAsset semantic, float minimumScore = 0.01f)
    {
        return GetSemanticScore(semantic).Net >= minimumScore;
    }

    public SemanticAsset GetDominantSemantic(SemanticFacetAsset facet)
    {
        if (facet == null || !HasSemantics) return null;

        SemanticAsset result = null;
        var resultScore = 0.01f;
        foreach (var semantic in ModClass.L.SemanticLibrary.list)
        {
            if (semantic.Facet != facet) continue;
            var score = GetSemanticScore(semantic).Net;
            if (score < resultScore) continue;
            if (score == resultScore && result != null &&
                string.CompareOrdinal(semantic.id, result.id) >= 0) continue;
            result = semantic;
            resultScore = score;
        }

        return result;
    }
}
