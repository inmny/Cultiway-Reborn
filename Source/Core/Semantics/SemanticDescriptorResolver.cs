using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>对语义描述执行维度过滤、主语义解析和带权契合计算。</summary>
public static class SemanticDescriptorResolver
{
    /// <summary>将描述在指定维度中的全部正向语义写入目标集合。</summary>
    public static void CollectFacet(
        SemanticDescriptor descriptor,
        SemanticLibrary library,
        SemanticFacetAsset facet,
        ISet<SemanticAsset> result)
    {
        var scores = BuildFacetScores(descriptor, library, facet);
        foreach (var pair in scores)
        {
            if (pair.Value > 0f) result.Add(pair.Key);
        }
    }

    /// <summary>
    /// 计算两个描述在指定维度中的带权余弦契合度；任一侧没有正向语义时返回 false。
    /// </summary>
    public static bool TryGetAffinity(
        SemanticDescriptor left,
        SemanticDescriptor right,
        SemanticLibrary library,
        SemanticFacetAsset facet,
        out float affinity)
    {
        var leftScores = BuildFacetScores(left, library, facet);
        var rightScores = BuildFacetScores(right, library, facet);
        var leftMagnitude = 0f;
        var rightMagnitude = 0f;
        var dot = 0f;

        foreach (var pair in leftScores)
        {
            if (pair.Value <= 0f) continue;
            leftMagnitude += pair.Value * pair.Value;
            if (rightScores.TryGetValue(pair.Key, out var rightValue) && rightValue > 0f)
                dot += pair.Value * rightValue;
        }
        foreach (var pair in rightScores)
        {
            if (pair.Value > 0f) rightMagnitude += pair.Value * pair.Value;
        }

        var denominator = Mathf.Sqrt(leftMagnitude * rightMagnitude);
        if (denominator <= 0f)
        {
            affinity = 0f;
            return false;
        }

        affinity = Mathf.Clamp01(dot / denominator);
        return true;
    }

    /// <summary>展开描述并汇总指定维度中每个语义的净证据分值。</summary>
    private static Dictionary<SemanticAsset, float> BuildFacetScores(
        SemanticDescriptor descriptor,
        SemanticLibrary library,
        SemanticFacetAsset facet)
    {
        var result = new Dictionary<SemanticAsset, float>();
        if (descriptor == null) return result;

        for (var i = 0; i < descriptor.contributions.Length; i++)
        {
            var contribution = descriptor.contributions[i];
            if (!library.TryResolve(contribution.semantic_id, out var semantic)) continue;
            var polarity = contribution.polarity == SemanticPolarity.Positive ? 1f : -1f;
            var baseScore = contribution.strength * contribution.confidence * polarity;
            var expansion = library.Expand(semantic);
            for (var j = 0; j < expansion.Count; j++)
            {
                var expanded = expansion[j];
                if (expanded.semantic.Facet != facet) continue;
                result.TryGetValue(expanded.semantic, out var previous);
                result[expanded.semantic] = previous + baseScore * expanded.strength;
            }
        }
        return result;
    }
}
