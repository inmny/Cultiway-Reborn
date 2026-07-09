using Cultiway.Core.Components;
using Cultiway.Utils;
using UnityEngine;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;
using SeriesTag = Cultiway.Core.SkillLibV3.SkillTags.Series;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 法术视觉元素风格与颜色工具。
/// 从旧 SkillVfxProfileAsset 剥离，供 TalismanVfx 等系统复用。
/// 新的 VFX 架构重建后此文件可并入或重写。
/// </summary>
public enum SkillVfxElementStyle
{
    Generic,
    Metal,
    Wood,
    Water,
    Fire,
    Earth,
    Neg,
    Pos,
    Entropy,
    Wind,
    Lightning
}

/// <summary>
/// 法术视觉元素风格解析与颜色取值。
/// </summary>
public static class SkillVfxColor
{
    public static SkillVfxElementStyle ResolveStyle(SkillEntityAsset asset)
    {
        if (asset.SeriesTags.Contains(ElementTag.Lightning)) return SkillVfxElementStyle.Lightning;
        if (asset.SeriesTags.Contains(ElementTag.Wind)) return SkillVfxElementStyle.Wind;
        if (asset.SeriesTags.Contains(SeriesTag.Metal)) return SkillVfxElementStyle.Metal;
        if (asset.SeriesTags.Contains(ElementTag.Wood)) return SkillVfxElementStyle.Wood;
        if (asset.SeriesTags.Contains(ElementTag.Water)) return SkillVfxElementStyle.Water;
        if (asset.SeriesTags.Contains(ElementTag.Fire)) return SkillVfxElementStyle.Fire;
        if (asset.SeriesTags.Contains(ElementTag.Earth)) return SkillVfxElementStyle.Earth;
        if (asset.SeriesTags.Contains(ElementTag.Neg)) return SkillVfxElementStyle.Neg;
        if (asset.SeriesTags.Contains(ElementTag.Pos)) return SkillVfxElementStyle.Pos;
        if (asset.SeriesTags.Contains(ElementTag.Entropy)) return SkillVfxElementStyle.Entropy;
        return ResolveStyle(asset.Element);
    }

    public static SkillVfxElementStyle ResolveStyle(ElementComposition element)
    {
        var max = element.iron;
        var style = SkillVfxElementStyle.Metal;
        Pick(element.iron, SkillVfxElementStyle.Metal, ref max, ref style);
        Pick(element.wood, SkillVfxElementStyle.Wood, ref max, ref style);
        Pick(element.water, SkillVfxElementStyle.Water, ref max, ref style);
        Pick(element.fire, SkillVfxElementStyle.Fire, ref max, ref style);
        Pick(element.earth, SkillVfxElementStyle.Earth, ref max, ref style);
        Pick(element.neg, SkillVfxElementStyle.Neg, ref max, ref style);
        Pick(element.pos, SkillVfxElementStyle.Pos, ref max, ref style);
        Pick(element.entropy, SkillVfxElementStyle.Entropy, ref max, ref style);

        // 复合元素映射：水+火=雷，水+木=风
        if (max <= 0f)
        {
            if (element.water > 0.25f && element.fire > 0.25f) return SkillVfxElementStyle.Lightning;
            if (element.water > 0.25f && element.wood > 0.25f) return SkillVfxElementStyle.Wind;
        }

        return max <= 0f ? SkillVfxElementStyle.Generic : style;
    }

    public static Color GetElementColor(ElementComposition element)
    {
        element.Normalize();
        var color = ColorUtils.FromElement(element.iron, element.wood, element.water, element.fire, element.earth,
            element.neg, element.pos, element.entropy);
        color.a = 0.82f;
        return color;
    }

    public static Color GetAccentColor(SkillVfxElementStyle style, Color color)
    {
        var target = style switch
        {
            SkillVfxElementStyle.Metal => new Color(1f, 0.95f, 0.55f),
            SkillVfxElementStyle.Wood => new Color(0.55f, 1f, 0.35f),
            SkillVfxElementStyle.Water => new Color(0.62f, 0.95f, 1f),
            SkillVfxElementStyle.Fire => new Color(1f, 0.82f, 0.35f),
            SkillVfxElementStyle.Earth => new Color(0.92f, 0.68f, 0.36f),
            SkillVfxElementStyle.Neg => new Color(0.52f, 0.22f, 0.88f),
            SkillVfxElementStyle.Pos => new Color(1f, 0.96f, 0.42f),
            SkillVfxElementStyle.Entropy => new Color(1f, 0.22f, 0.92f),
            SkillVfxElementStyle.Wind => new Color(0.78f, 1f, 0.92f),
            SkillVfxElementStyle.Lightning => new Color(0.72f, 0.95f, 1f),
            _ => Color.white
        };

        // 降低向高明度 target 的偏移，避免叠加后发白发亮。
        var accent = Color.Lerp(color, target, 0.35f);
        accent.a = 0.75f;
        return accent;
    }

    private static void Pick(float value, SkillVfxElementStyle candidate, ref float max,
        ref SkillVfxElementStyle style)
    {
        if (value <= max) return;

        max = value;
        style = candidate;
    }
}
