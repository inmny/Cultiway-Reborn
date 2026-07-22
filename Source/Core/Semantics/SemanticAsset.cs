using System;
using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 一条可跨系统共享的规范语义。ID 是稳定身份，别名只用于兼容旧数据和外部输入。
/// </summary>
public sealed class SemanticAsset : Asset
{
    public string facet_id;
    public string name_key;
    public string description_key;
    public string icon_path;
    /// <summary>用于规则化命名的候选词干；为空表示该语义不直接参与命名。</summary>
    public string[] naming_stems = Array.Empty<string>();
    /// <summary>语义进入规则化命名候选时的显著度倍率。</summary>
    public float naming_salience;
    /// <summary>该语义用于图标与特效展示的规范基础色；未配置时 alpha 为 0。</summary>
    public Color visual_color = Color.clear;
    /// <summary>该语义参与调色板排序时的视觉显著度；小于等于 0 表示不参与配色。</summary>
    public float color_salience;
    public string[] aliases = Array.Empty<string>();
    public string[] parent_ids = Array.Empty<string>();
    public SemanticImplication[] implications = Array.Empty<SemanticImplication>();

    public SemanticFacetAsset Facet { get; internal set; }
    private Sprite icon;

    /// <summary>判断该语义是否显式声明了可用于展示的规范颜色。</summary>
    public bool HasVisualColor => color_salience > 0f && visual_color.a > 0f;

    public string GetName()
    {
        var key = string.IsNullOrEmpty(name_key) ? $"Cultiway.Semantic.{id}" : name_key;
        var localized = key.Localize();
        return localized == key ? id : localized;
    }

    public string GetDescription()
    {
        if (string.IsNullOrEmpty(description_key)) return string.Empty;
        var localized = description_key.Localize();
        return localized == description_key ? string.Empty : localized;
    }

    /// <summary>返回语义专属图标；未配置时使用所属维度的图标。</summary>
    public Sprite GetIcon()
    {
        if (icon != null) return icon;
        if (!string.IsNullOrEmpty(icon_path)) icon = SpriteTextureLoader.getSprite(icon_path);
        return icon != null ? icon : Facet?.GetIcon();
    }
}

/// <summary>
/// 从一个语义到另一个语义的弱蕴含。强度会乘入派生证据，取值范围为 0 到 1。
/// </summary>
public struct SemanticImplication
{
    public string semantic_id;
    public float strength;

    public SemanticImplication(string semanticId, float strength = 1f)
    {
        semantic_id = semanticId;
        this.strength = strength;
    }
}
