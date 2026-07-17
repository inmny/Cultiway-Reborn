using System;
using Cultiway.Content.Components;
using NeoModLoader.General;
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
    /// <summary>组合器内部使用的稳定原子键，不参与跨系统语义查询。</summary>
    public string key;
    /// <summary>本地化名称键；未配置文本时回退到 atom 的稳定键。</summary>
    public string name_key;
    /// <summary>本地化说明键；用于编辑器、图鉴和后续炼器入口。</summary>
    public string description_key;
    /// <summary>编辑器使用的独立图标资源路径，不包含文件扩展名。</summary>
    public string editor_icon_path;
    public ArtifactAtomCategory category;
    public ArtifactShapeAsset artifact_shape;
    public string[] name_stems = [];
    public string[] variant_biases = [];
    public string[] color_scheme_biases = [];
    public ArtifactMaterialTrait[] material_traits = [];
    public float minimum_score = 1f;
    public int priority;
    public Func<ArtifactRecipeContext, float> ScoreRecipe;

    public string GetName()
    {
        return !string.IsNullOrEmpty(name_key) && LM.Has(name_key) ? LM.Get(name_key) : key ?? id;
    }

    public string GetDescription()
    {
        return !string.IsNullOrEmpty(description_key) && LM.Has(description_key)
            ? LM.Get(description_key)
            : string.Empty;
    }

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
