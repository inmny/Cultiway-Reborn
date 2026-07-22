using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 由对象语义解析出的最多三种规范颜色。颜色按主次排列，未占用的槽位不会生成替代色。
/// </summary>
public struct SemanticColorPalette
{
    public Color Primary;
    public Color Secondary;
    public Color Tertiary;
    public byte Count;

    /// <summary>判断调色板是否至少包含一种颜色。</summary>
    public bool HasColor => Count > 0;

    /// <summary>按主次索引读取颜色；索引超出已用槽位时返回给定兜底色。</summary>
    public Color GetColor(int index, Color fallback = default)
    {
        return index switch
        {
            0 when Count > 0 => Primary,
            1 when Count > 1 => Secondary,
            2 when Count > 2 => Tertiary,
            _ => fallback
        };
    }

    /// <summary>将指定颜色槽转换为物品图标使用的十六进制文本；空槽返回 null。</summary>
    public string GetHex(int index)
    {
        return index >= 0 && index < Count ? Toolbox.colorToHex(GetColor(index)) : null;
    }

    internal bool TryAdd(Color color, int maximumColors)
    {
        if (Count >= Mathf.Clamp(maximumColors, 0, 3) || Contains(color)) return false;
        color.a = 1f;
        switch (Count)
        {
            case 0:
                Primary = color;
                break;
            case 1:
                Secondary = color;
                break;
            case 2:
                Tertiary = color;
                break;
        }
        Count++;
        return true;
    }

    private bool Contains(Color color)
    {
        var target = (Color32)color;
        for (var i = 0; i < Count; i++)
        {
            if (((Color32)GetColor(i)).Equals(target)) return true;
        }
        return false;
    }
}

/// <summary>
/// 将带权语义档案确定性解析为展示调色板。直接语义优先，推导语义只用于补足空槽。
/// </summary>
public static class SemanticColorResolver
{
    private const float VfxLightenRatio = 0.18f;

    /// <summary>
    /// 解析语义档案的主次颜色。preferredSemantic 用于让已经确定的复合视觉形态占据主色。
    /// </summary>
    public static SemanticColorPalette Resolve(
        SemanticProfile profile,
        SemanticAsset preferredSemantic = null,
        int maximumColors = 3)
    {
        var palette = new SemanticColorPalette();
        maximumColors = Mathf.Clamp(maximumColors, 0, 3);
        if (maximumColors == 0 || profile == null) return palette;

        if (preferredSemantic?.HasVisualColor == true)
        {
            palette.TryAdd(preferredSemantic.visual_color, maximumColors);
        }

        var directRanks = profile.GetDirectRanked(SemanticQueryPolicy.Default);
        AddRankedColors(directRanks, ref palette, maximumColors);
        if (palette.Count >= maximumColors) return palette;

        var directSemantics = new HashSet<SemanticAsset>(directRanks.Select(rank => rank.semantic));
        var inferredRanks = profile.GetRanked(SemanticQueryPolicy.Default)
            .Where(rank => !directSemantics.Contains(rank.semantic));
        AddRankedColors(inferredRanks, ref palette, maximumColors);
        return palette;
    }

    /// <summary>将规范基础色调整为更适合粒子和发光效果的高明度颜色。</summary>
    public static Color ToVfxColor(Color color)
    {
        var alpha = color.a;
        color = Color.Lerp(color, Color.white, VfxLightenRatio);
        color.a = alpha;
        return color;
    }

    private static void AddRankedColors(
        IEnumerable<SemanticRank> ranks,
        ref SemanticColorPalette palette,
        int maximumColors)
    {
        foreach (var rank in ranks
                     .Where(rank => rank.semantic.HasVisualColor)
                     .OrderByDescending(rank => rank.score.Net * rank.semantic.color_salience)
                     .ThenBy(rank => rank.semantic.id, StringComparer.Ordinal))
        {
            palette.TryAdd(rank.semantic.visual_color, maximumColors);
            if (palette.Count >= maximumColors) return;
        }
    }
}
