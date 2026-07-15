using System;
using Cultiway.Content.Components;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public enum ArtifactAtomCategory
{
    Shape,
    Material,
    Finish,
}

public class ArtifactAtomAsset : Asset
{
    public string tag;
    public ArtifactAtomCategory category;
    public ArtifactShapeAsset artifact_shape;
    public string[] name_stems = [];
    public string[] variant_biases = [];
    public string[] color_scheme_biases = [];
    public ArtifactMaterialTrait[] semantic_traits = [];
    public float minimum_score = 1f;
    public int priority;
    public Func<ArtifactRecipeContext, float> ScoreRecipe;

    public float ScoreFor(ArtifactRecipeContext context)
    {
        return Mathf.Max(0f, ScoreRecipe?.Invoke(context) ?? 0f);
    }

    public string PickNameStem(int seed)
    {
        if (name_stems == null || name_stems.Length == 0) return string.Empty;
        return name_stems[(int)(unchecked((uint)seed) % (uint)name_stems.Length)];
    }

    public bool BiasesVariant(string moduleKey, string variantKey)
    {
        if (variant_biases == null) return false;
        var full = $"{moduleKey}.{variantKey}";
        return Array.IndexOf(variant_biases, full) >= 0 || Array.IndexOf(variant_biases, variantKey) >= 0;
    }

    public bool BiasesColorScheme(string schemeKey)
    {
        if (color_scheme_biases == null || string.IsNullOrEmpty(schemeKey)) return false;
        return Array.IndexOf(color_scheme_biases, schemeKey) >= 0;
    }
}

/// <summary>
/// 组合阶段解析出的 atom 资产与贡献强度。
/// </summary>
public readonly struct ArtifactAtomSelection
{
    public readonly ArtifactAtomAsset Atom;
    public readonly float Strength;

    public ArtifactAtomSelection(ArtifactAtomAsset atom, float strength)
    {
        Atom = atom;
        Strength = strength;
    }
}
