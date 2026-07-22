using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using UnityEngine;

namespace Cultiway.Core;

public static class ItemIconGenerator
{
    private static readonly Dictionary<int, Sprite> IconCache = new();
    private static readonly Sprite DefaultIcon;

    static ItemIconGenerator()
    {
        DefaultIcon = SpriteTextureLoader.getSprite("cultiway/icons/iconElement");
    }
    public static Sprite GenerateIcon(ItemShape shape, ItemIconData icon_data)
    {
        var icon_hash = Mathf.Abs(icon_data.GetHashCode() + shape.shape_id.GetHashCode());
        if (IconCache.TryGetValue(icon_hash, out var sprite))
            return sprite;

        var texture = GenerateTexture(shape.Type, icon_data, icon_hash);

        if (texture == null)
        {
            return DefaultIcon;
        }

        return IconCache[icon_hash] =
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static Texture2D GenerateTexture(ItemShapeAsset shape_asset, ItemIconData icon_data, int hash)
    {
        const int targetWidth = 48;
        const int targetHeight = 48;
        var texture = new Texture2D(targetWidth, targetHeight);

        var shape_list = shape_asset.major_shapes;
        if (shape_list == null)
        {
            return null;
        }

        if (shape_list.Count == 0) return null;

        var shape = shape_list[hash % shape_list.Count];

        bool color1_available = TryParseColor(icon_data.ColorHex1, out Color color1);
        bool color2_available = TryParseColor(icon_data.ColorHex2, out Color color2);
        bool color3_available = TryParseColor(icon_data.ColorHex3, out Color color3);

        var decorations = new List<Color[]>();
        if (icon_data.Decorations != null)
        {
            foreach (var id in icon_data.Decorations)
            {
                continue;
                /*
                if (Decorations.TryGetValue(id, out var decoration))
                {
                    decorations.Add(decoration.GetPixels());
                }*/
            }
        }

        int shape_width = shape.Direct.GetLength(0);
        int shape_height = shape.Direct.GetLength(1);

        if (shape_width <= 0 || shape_height <= 0)
        {
            ModClass.LogError("Invalid shape size!");
            return texture;
        }

        float scale_x = (float)shape_width / targetWidth;
        float scale_y = (float)shape_height / targetHeight;

        for (int target_x = 0; target_x < targetWidth; target_x++)
        {
            for (int target_y = 0; target_y < targetHeight; target_y++)
            {
                int shape_x = Mathf.Clamp(Mathf.RoundToInt(target_x * scale_x), 0, shape_width - 1);
                int shape_y = Mathf.Clamp(Mathf.RoundToInt(target_y * scale_y), 0, shape_height - 1);

                Color direct_color = shape.Direct[shape_x, shape_y];
                var source = shape.Source[shape_x, shape_y];
                if (source == -1)
                {
                    texture.SetPixel(target_x, target_y, Color.clear);
                }
                else if (source == 0)
                {
                    texture.SetPixel(target_x, target_y, direct_color);
                }
                else if (TryGetRegionColor(source,
                             color1_available, color1,
                             color2_available, color2,
                             color3_available, color3,
                             out Color target_color))
                {
                    texture.SetPixel(target_x, target_y,
                        RecolorPixel(direct_color, target_color, shape, shape_x, shape_y));
                }
                else
                {
                    texture.SetPixel(target_x, target_y, direct_color);
                }
            }
        }

        foreach (var decoration in decorations)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                for (int y = 0; y < targetHeight; y++)
                {
                    int index = y * targetWidth + x;
                    if (index >= 0 && index < decoration.Length)
                    {
                        Color dec_color = decoration[index];
                        if (dec_color.a != 0)
                        {
                            texture.SetPixel(x, y, dec_color);
                        }
                    }
                }
            }
        }

        texture.Apply();

        return texture;
    }

    /// <summary>解析可选的 HTML 颜色；空值与非法值都不会启用对应换色区。</summary>
    private static bool TryParseColor(string color_hex, out Color color)
    {
        if (string.IsNullOrWhiteSpace(color_hex))
        {
            color = Color.clear;
            return false;
        }

        return ColorUtility.TryParseHtmlString(color_hex, out color);
    }

    /// <summary>按模板区域编号取得调用方提供的目标颜色。</summary>
    private static bool TryGetRegionColor(int source,
        bool color1_available, Color color1,
        bool color2_available, Color color2,
        bool color3_available, Color color3,
        out Color color)
    {
        switch (source)
        {
            case 1 when color1_available:
                color = color1;
                return true;
            case 2 when color2_available:
                color = color2;
                return true;
            case 3 when color3_available:
                color = color3;
                return true;
            default:
                color = Color.clear;
                return false;
        }
    }

    /// <summary>应用目标色相，同时保留模板像素的明暗、材质变化、软边与透明度。</summary>
    private static Color RecolorPixel(Color source, Color target, Shape shape, int x, int y)
    {
        Color.RGBToHSV(target, out float hue, out float saturation, out float value);
        hue = Mathf.Repeat(hue + shape.HueOffset[x, y], 1f);
        saturation = Mathf.Clamp01(saturation * shape.SaturationRatio[x, y]);
        value = Mathf.Clamp01(value + shape.ValueOffset[x, y]);

        Color recolored = Color.HSVToRGB(hue, saturation, value);
        recolored.a = source.a;
        return Color.Lerp(source, recolored, shape.ColorBlend[x, y]);
    }
}
