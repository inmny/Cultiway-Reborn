using System;
using UnityEngine;

namespace Cultiway.Utils;

public static class ColorUtils
{
    /// <summary>取 sprite 贴图（textureRect 区域内、跳过全透明像素）的平均 RGB 颜色。</summary>
    public static Color32 GetAverageColor(Sprite sprite)
    {
        Texture2D tex = sprite.texture;
        if (tex == null) return Color.white;

        Rect rect = sprite.textureRect;
        int x0 = Mathf.FloorToInt(rect.x);
        int y0 = Mathf.FloorToInt(rect.y);
        int x1 = x0 + Mathf.FloorToInt(rect.width);
        int y1 = y0 + Mathf.FloorToInt(rect.height);
        int tex_w = tex.width;

        Color32[] pixels = tex.GetPixels32();
        long r = 0, g = 0, b = 0;
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            int row = y * tex_w;
            for (int x = x0; x < x1; x++)
            {
                Color32 c = pixels[row + x];
                if (c.a == 0) continue;
                r += c.r;
                g += c.g;
                b += c.b;
                count++;
            }
        }

        return count > 0 ? ColorUtils.AverageRgb(r, g, b, count) : Color.white;
    }
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

    public static Color Darken(Color color, float value)
    {
        var r = Mathf.Clamp(color.r * (1 - value), 0, 1);
        var g = Mathf.Clamp(color.g * (1 - value), 0, 1);
        var b = Mathf.Clamp(color.b * (1 - value), 0, 1);
        return new Color(r, g, b);
    }
}
