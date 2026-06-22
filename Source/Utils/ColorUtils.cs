using System;
using UnityEngine;

namespace Cultiway.Utils;

public static class ColorUtils
{
    public static bool IsSameWith(this Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    public static Color32 WithAlpha(Color32 color, byte alpha)
    {
        color.a = alpha;
        return color;
    }

    public static Color32 Blend(Color32 color, Color32 target, float amount)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(color.r, target.r, amount)), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(color.g, target.g, amount)), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(color.b, target.b, amount)), 0, 255),
            color.a
        );
    }

    public static Color32 AverageRgb(long red, long green, long blue, int count, byte alpha = 255)
    {
        if (count <= 0)
        {
            throw new InvalidOperationException("颜色平均值计算失败：颜色数量必须大于 0");
        }

        return new Color32(
            (byte)Mathf.Clamp((int)(red / count), 0, 255),
            (byte)Mathf.Clamp((int)(green / count), 0, 255),
            (byte)Mathf.Clamp((int)(blue / count), 0, 255),
            alpha);
    }

    public static readonly Color IronColor = new Color(0.70f, 0.70f, 0.75f);
    public static readonly Color WoodColor = new Color(0.20f, 0.60f, 0.20f);
    public static readonly Color WaterColor = new Color(0.20f, 0.40f, 0.90f);
    public static readonly Color FireColor = new Color(0.95f, 0.30f, 0.10f);
    public static readonly Color EarthColor = new Color(0.55f, 0.35f, 0.20f);
    public static readonly Color NegColor = new Color(0.20f, 0.00f, 0.30f);
    public static readonly Color PosColor = new Color(1.00f, 0.95f, 0.20f);
    public static readonly Color EntropyColor = new Color(0.90f, 0.10f, 0.90f);
    public static Color FromElement(float iron, float wood, float water, float fire, float earth, float neg, float pos,
        float entropy)
    {
        return new Color(Mathf.Clamp01(iron * IronColor.r + wood * WoodColor.r + water * WaterColor.r + fire * FireColor.r + earth * EarthColor.r + neg * NegColor.r + pos * PosColor.r + entropy * EntropyColor.r),
            Mathf.Clamp01(iron * IronColor.g + wood * WoodColor.g + water * WaterColor.g + fire * FireColor.g + earth * EarthColor.g + neg * NegColor.g + pos * PosColor.g + entropy * EntropyColor.g),
            Mathf.Clamp01(iron * IronColor.b + wood * WoodColor.b + water * WaterColor.b + fire * FireColor.b + earth * EarthColor.b + neg * NegColor.b + pos * PosColor.b + entropy * EntropyColor.b));
    }

    public static Color Darken(Color color, float value)
    {
        var r = Mathf.Clamp(color.r * (1 - value), 0, 1);
        var g = Mathf.Clamp(color.g * (1 - value), 0, 1);
        var b = Mathf.Clamp(color.b * (1 - value), 0, 1);
        return new Color(r, g, b);
    }
}
