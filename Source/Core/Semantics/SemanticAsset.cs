using System;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 一条可跨系统共享的规范语义。ID 是稳定身份，别名只用于兼容旧数据和外部输入。
/// </summary>
public sealed class SemanticAsset : Asset
{
    public string facet_id;
    public string name_key;
    public string description_key;
    public string[] aliases = Array.Empty<string>();
    public string[] parent_ids = Array.Empty<string>();
    public SemanticImplication[] implications = Array.Empty<SemanticImplication>();

    public SemanticFacetAsset Facet { get; internal set; }

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
