using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
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

        Color color1 = Color.clear;
        var color1_available = false;
        if (icon_data.ColorHex1 != null)
        {
            color1_available = true;
            ColorUtility.TryParseHtmlString(icon_data.ColorHex1, out color1);
        }

        Color color2 = Color.clear;
        var color2_available = false;
        if (icon_data.ColorHex2 != null)
        {
            color2_available = true;
            ColorUtility.TryParseHtmlString(icon_data.ColorHex2, out color2);
        }

        Color color3 = Color.clear;
        var color3_available = false;
        if (icon_data.ColorHex3 != null)
        {
            color3_available = true;
            ColorUtility.TryParseHtmlString(icon_data.ColorHex3, out color3);
        }

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
                float dark1 = shape.Dark1[shape_x, shape_y];
                float dark2 = shape.Dark2[shape_x, shape_y];
                float dark3 = shape.Dark3[shape_x, shape_y];
                var source = shape.Source[shape_x, shape_y];
                if (source == -1)
                {
                    texture.SetPixel(target_x, target_y, Color.clear);
                }
                else if (source == 0)
                {
                    texture.SetPixel(target_x, target_y, direct_color);
                }
                else if (source == 1 && color1_available)
                {
                    texture.SetPixel(target_x, target_y, ColorUtils.Darken(color1, dark1));
                }
                else if (source == 2 && color2_available)
                {
                    texture.SetPixel(target_x, target_y, ColorUtils.Darken(color2, dark2));
                }
                else if (source == 3 && color3_available)
                {
                    texture.SetPixel(target_x, target_y, ColorUtils.Darken(color3, dark3));
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
}