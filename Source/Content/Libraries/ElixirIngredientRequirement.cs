using System;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>丹方材料槽中的一条带权语义要求。</summary>
public readonly struct ElixirSemanticRequirement
{
    public readonly SemanticAsset semantic;
    public readonly float weight;
    public readonly SemanticPolarity polarity;

    public ElixirSemanticRequirement(
        SemanticAsset semantic,
        float weight,
        SemanticPolarity polarity = SemanticPolarity.Positive)
    {
        this.semantic = semantic;
        this.weight = weight;
        this.polarity = polarity;
    }
}

/// <summary>可由语义相近材料满足的单个丹方材料槽。</summary>
public struct ElixirIngredientRequirement
{
    public const float MinimumSimilarity = 0.7f;
    private const float FullCoverageScore = 0.25f;

    public ElixirSemanticRequirement[] semantics;
    public SemanticAsset[] required_semantics;
    public int minimum_quality_stage;

    public float Match(Entity ingredient, SemanticProfile profile)
    {
        if (ingredient.IsNull || profile == null || semantics == null || semantics.Length == 0) return -1f;

        var stage = ingredient.TryGetComponent(out ItemLevel level) ? level.Stage : 0;
        if (stage < minimum_quality_stage) return -1f;

        if (required_semantics != null)
        {
            for (var i = 0; i < required_semantics.Length; i++)
            {
                if (profile.GetScore(required_semantics[i], SemanticQueryPolicy.Default).Net < 0.01f) return -1f;
            }
        }

        var matched = 0f;
        var totalWeight = 0f;
        for (var i = 0; i < semantics.Length; i++)
        {
            var requirement = semantics[i];
            if (requirement.semantic == null || requirement.weight <= 0f) continue;
            var score = profile.GetScore(requirement.semantic, SemanticQueryPolicy.Default);
            var available = requirement.polarity == SemanticPolarity.Positive ? score.positive : score.negative;
            matched += requirement.weight * Mathf.Clamp01(available / FullCoverageScore);
            totalWeight += requirement.weight;
        }

        if (totalWeight <= 0f) return -1f;
        var similarity = matched / totalWeight;
        return similarity >= MinimumSimilarity ? similarity : -1f;
    }

    public string GetName()
    {
        var names = (semantics ?? Array.Empty<ElixirSemanticRequirement>())
            .Where(x => x.semantic != null && x.polarity == SemanticPolarity.Positive)
            .OrderByDescending(x => x.weight)
            .ThenBy(x => x.semantic.id, StringComparer.Ordinal)
            .Take(2)
            .Select(x => x.semantic.GetName())
            .ToArray();
        var semanticName = names.Length == 0 ? "灵材" : string.Join("/", names);
        var stageName = LM.Get($"Cultiway.Stage.{Mathf.Clamp(minimum_quality_stage, 0, 3)}");
        return $"{semanticName} · {stageName}阶";
    }
}
