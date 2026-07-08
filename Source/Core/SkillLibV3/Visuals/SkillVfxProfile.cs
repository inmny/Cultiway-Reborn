using Cultiway.Utils;
using UnityEngine;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;
using SeriesTag = Cultiway.Core.SkillLibV3.SkillTags.Series;

namespace Cultiway.Core.SkillLibV3.Visuals;

public enum SkillVfxWeight
{
    Light,
    Medium,
    Heavy,
    Extreme
}

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

public class SkillVfxProfileAsset : Asset
{
    public SkillVfxElementStyle Style;
    public string CastPath;
    public string CastAccentPath;
    public string MuzzlePath;
    public string TrailPath;
    public string TrailAccentPath;
    public string ImpactPath;
    public string ImpactAccentPath;
    public string ResidualPath;
    public float TrailInterval;
    public float CastScale;
    public float MuzzleScale;
    public float TrailScale;
    public float ImpactScale;
    public float ResidualScale;
    public bool CastFixedUpright;
    public bool ImpactFixedUpright;
    public bool ResidualFixedUpright;

    public static SkillVfxProfileAsset Create(string id, SkillVfxElementStyle style,
        float trailInterval, float castScale, float muzzleScale, float trailScale, float impactScale,
        float residualScale, bool castFixedUpright = false, bool impactFixedUpright = false,
        bool residualFixedUpright = false)
    {
        return new SkillVfxProfileAsset
        {
            id = id,
            Style = style,
            CastPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Cast),
            CastAccentPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Residual),
            MuzzlePath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Muzzle),
            TrailPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Trail),
            TrailAccentPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Residual),
            ImpactPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Impact),
            ImpactAccentPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Muzzle),
            ResidualPath = SkillVfxResourceResolver.ResolvePhase(style, SkillVfxPhase.Residual),
            TrailInterval = trailInterval,
            CastScale = castScale,
            MuzzleScale = muzzleScale,
            TrailScale = trailScale,
            ImpactScale = impactScale,
            ResidualScale = residualScale,
            CastFixedUpright = castFixedUpright,
            ImpactFixedUpright = impactFixedUpright,
            ResidualFixedUpright = residualFixedUpright
        };
    }

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

        Pick(element.wood, SkillVfxElementStyle.Wood, ref max, ref style);
        Pick(element.water, SkillVfxElementStyle.Water, ref max, ref style);
        Pick(element.fire, SkillVfxElementStyle.Fire, ref max, ref style);
        Pick(element.earth, SkillVfxElementStyle.Earth, ref max, ref style);
        Pick(element.neg, SkillVfxElementStyle.Neg, ref max, ref style);
        Pick(element.pos, SkillVfxElementStyle.Pos, ref max, ref style);
        Pick(element.entropy, SkillVfxElementStyle.Entropy, ref max, ref style);

        if (style is not (SkillVfxElementStyle.Neg or SkillVfxElementStyle.Pos or SkillVfxElementStyle.Entropy))
        {
            if (element.water > 0.25f && element.fire > 0.25f) return SkillVfxElementStyle.Lightning;
            if (element.water > 0.25f && element.wood > 0.25f) return SkillVfxElementStyle.Wind;
        }

        return max <= 0f ? SkillVfxElementStyle.Generic : style;
    }

    private static void Pick(float value, SkillVfxElementStyle candidate, ref float max,
        ref SkillVfxElementStyle style)
    {
        if (value <= max) return;

        max = value;
        style = candidate;
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

        var accent = Color.Lerp(color, target, 0.55f);
        accent.a = 0.9f;
        return accent;
    }
}
