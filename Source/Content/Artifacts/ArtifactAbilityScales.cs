using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 材料语义解析出的六项通用能力尺度。它们只作为参数生成的共同基准，不是固定数值模板。
/// </summary>
public readonly struct ArtifactAbilityScales
{
    public readonly float Potency;
    public readonly float Range;
    public readonly float Duration;
    public readonly float Efficiency;
    public readonly float Precision;
    public readonly float Capacity;

    private ArtifactAbilityScales(
        float potency,
        float range,
        float duration,
        float efficiency,
        float precision,
        float capacity)
    {
        Potency = potency;
        Range = range;
        Duration = duration;
        Efficiency = efficiency;
        Precision = precision;
        Capacity = capacity;
    }

    public static ArtifactAbilityScales Resolve(ArtifactRecipeContext recipe)
    {
        int quality = recipe.quality_stage * 9 + recipe.quality_level;
        float qualityScale = quality * 0.035f;
        float spirituality = Root(recipe.GetTrait(ArtifactMaterialTraits.Spirituality));
        float stability = Mathf.Clamp01(recipe.GetTrait(ArtifactMaterialTraits.Stability));
        float volatility = Root(recipe.GetTrait(ArtifactMaterialTraits.Volatility));

        return new ArtifactAbilityScales(
            1f + quality * 0.055f + spirituality * 0.16f +
            Root(recipe.GetTrait(ArtifactMaterialTraits.Impact) +
                 recipe.GetTrait(ArtifactMaterialTraits.Amplification)) * 0.18f,
            1f + qualityScale +
            Root(recipe.GetTrait(ArtifactMaterialTraits.Mobility) +
                 recipe.GetTrait(ArtifactMaterialTraits.Projection) +
                 recipe.GetTrait(ArtifactMaterialTraits.Perception)) * 0.16f,
            1f + qualityScale + stability * 0.35f +
            Root(recipe.GetTrait(ArtifactMaterialTraits.Sustain) +
                 recipe.GetTrait(ArtifactMaterialTraits.Ward)) * 0.18f,
            Mathf.Max(0.35f, 1f + qualityScale + stability * 0.45f - volatility * 0.08f),
            1f + qualityScale + stability * 0.2f +
            Root(recipe.GetTrait(ArtifactMaterialTraits.Perception) +
                 recipe.GetTrait(ArtifactMaterialTraits.Edge) +
                 recipe.GetTrait(ArtifactMaterialTraits.Resonance)) * 0.16f,
            1f + quality * 0.045f +
            Root(recipe.GetTrait(ArtifactMaterialTraits.Capacity) +
                 recipe.GetTrait(ArtifactMaterialTraits.Storage) +
                 recipe.GetTrait(ArtifactMaterialTraits.Quantity)) * 0.2f);
    }

    private static float Root(float value)
    {
        return Mathf.Sqrt(Mathf.Max(0f, value));
    }
}
