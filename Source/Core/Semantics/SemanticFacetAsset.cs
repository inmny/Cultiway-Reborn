using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 语义维度资产。维度用于区分元素、形态、用途等彼此独立的语义空间。
/// </summary>
public sealed class SemanticFacetAsset : Asset
{
    public string name_key;
    public string icon_path;
    private Sprite icon;

    public string GetName()
    {
        var key = string.IsNullOrEmpty(name_key) ? $"Cultiway.SemanticFacet.{id}" : name_key;
        var localized = key.Localize();
        return localized == key ? id : localized;
    }

    /// <summary>返回该语义维度用于未配置专属图标时的兜底图标。</summary>
    public Sprite GetIcon()
    {
        if (icon == null && !string.IsNullOrEmpty(icon_path))
            icon = SpriteTextureLoader.getSprite(icon_path);
        return icon;
    }
}
