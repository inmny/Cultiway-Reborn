using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Content.Visuals;

internal static class TalismanVfxFrameLibrary
{
    private const int CanvasSize = 64;
    private const float PixelsPerUnit = 64f;
    private const int SourceActivationFrameCount = 15;
    private const int VisibleActivationFrameCount = 11;
    private const int AwakeFrameCount = SourceActivationFrameCount / 3;
    private const int BurnFrameCount = SourceActivationFrameCount - AwakeFrameCount;
    private static readonly Dictionary<string, Sprite[]> ActivationFrames = new();
    private static readonly HashSet<int> LoggedUnreadableSprites = new();

    public static Sprite[] GetActivationFrames(Sprite icon, Color color, Color accentColor)
    {
        if (icon == null) return System.Array.Empty<Sprite>();

        var key = $"{icon.GetInstanceID()}:{ColorUtility.ToHtmlStringRGBA(color)}:" +
                  $"{ColorUtility.ToHtmlStringRGBA(accentColor)}";
        if (ActivationFrames.TryGetValue(key, out var cached)) return cached;

        var source = ReadSpritePixels(icon);
        var awakeFrames = BuildAwakeFrames(source, color, accentColor);
        var burnFrames = BuildBurnFrames(source, color, accentColor);
        var frames = awakeFrames.Concat(burnFrames).Take(VisibleActivationFrameCount).ToArray();
        ActivationFrames[key] = frames;
        return frames;
    }

    private static Sprite[] BuildAwakeFrames(Color[] source, Color color, Color accentColor)
    {
        const int count = AwakeFrameCount;
        var frames = new Color[count][];
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 1f : i / (count - 1f);
            var eased = EaseOut(t);
            var scale = Mathf.Lerp(0.68f, 1.05f, eased);
            var alpha = Mathf.Lerp(0.18f, 1f, eased);
            var tintAmount = Mathf.Lerp(0.08f, 0.34f, Mathf.Sin(t * Mathf.PI));
            var frame = DrawScaled(source, scale, alpha, Color.Lerp(color, accentColor, 0.4f), tintAmount);
            AddGlow(frame, accentColor, 2, Mathf.Lerp(0.22f, 0.48f, eased));
            frames[i] = frame;
        }

        return frames.Select((pixels, i) => CreateSprite(pixels, $"talisman_awake_{i:00}")).ToArray();
    }

    private static Sprite[] BuildBurnFrames(Color[] source, Color color, Color accentColor)
    {
        const int count = BurnFrameCount;
        var frames = new Color[count][];
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 1f : i / (count - 1f);
            var frame = BuildBurnFrame(source, color, accentColor, t);
            AddGlow(frame, accentColor, 3, Mathf.Lerp(0.42f, 0.18f, t));
            frames[i] = frame;
        }

        return frames.Select((pixels, i) => CreateSprite(pixels, $"talisman_burn_{i:00}")).ToArray();
    }

    private static Color[] BuildBurnFrame(Color[] source, Color color, Color accentColor, float t)
    {
        var frame = CreateEmptyPixels();
        var burnEdgeColor = Color.Lerp(color, accentColor, 0.7f);
        var progress = Mathf.Lerp(0.06f, 1.18f, FastBurnProgress(t));
        var center = (CanvasSize - 1) * 0.5f;

        for (var y = 0; y < CanvasSize; y++)
        {
            var yNorm = y / (CanvasSize - 1f);
            for (var x = 0; x < CanvasSize; x++)
            {
                var index = ToIndex(x, y);
                var pixel = source[index];
                if (pixel.a <= 0.01f) continue;

                var edgeDistance = Mathf.Min(Mathf.Min(x, y), Mathf.Min(CanvasSize - 1 - x, CanvasSize - 1 - y)) /
                                   center;
                edgeDistance = Mathf.Clamp01(edgeDistance);
                var noise = Hash01(x, y);
                var burnOrder = yNorm * 0.74f + edgeDistance * 0.18f + noise * 0.08f;
                var front = burnOrder - progress;
                if (front < -0.06f) continue;

                if (front < 0.09f)
                {
                    var heat = Mathf.Clamp01(1f - front / 0.09f);
                    var c = Color.Lerp(accentColor, burnEdgeColor, 0.55f + noise * 0.35f);
                    c.a = pixel.a * Mathf.Lerp(0.42f, 1f, heat);
                    frame[index] = c;
                    continue;
                }

                var paper = Color.Lerp(pixel, color, 0.12f + Mathf.Sin(t * Mathf.PI) * 0.12f);
                paper.a *= Mathf.Lerp(1f, 0.32f, Mathf.Clamp01((t - 0.48f) / 0.47f));
                frame[index] = paper;
            }
        }

        return frame;
    }

    private static Color[] DrawScaled(Color[] source, float scale, float alpha, Color tint, float tintAmount)
    {
        var frame = CreateEmptyPixels();
        var center = (CanvasSize - 1) * 0.5f;
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var sx = Mathf.RoundToInt((x - center) / scale + center);
                var sy = Mathf.RoundToInt((y - center) / scale + center);
                if (sx < 0 || sx >= CanvasSize || sy < 0 || sy >= CanvasSize) continue;

                var sourcePixel = source[ToIndex(sx, sy)];
                if (sourcePixel.a <= 0.01f) continue;

                var pixel = Color.Lerp(sourcePixel, tint, tintAmount);
                pixel.a = sourcePixel.a * alpha;
                frame[ToIndex(x, y)] = pixel;
            }
        }

        return frame;
    }

    private static void AddGlow(Color[] frame, Color glowColor, int radius, float alpha)
    {
        var source = frame.ToArray();
        radius = Mathf.Clamp(radius, 1, 5);
        for (var y = 0; y < CanvasSize; y++)
        {
            for (var x = 0; x < CanvasSize; x++)
            {
                var index = ToIndex(x, y);
                if (source[index].a > 0.62f) continue;

                var glow = 0f;
                for (var oy = -radius; oy <= radius; oy++)
                {
                    var ny = y + oy;
                    if (ny < 0 || ny >= CanvasSize) continue;
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var nx = x + ox;
                        if (nx < 0 || nx >= CanvasSize) continue;
                        var distance = Mathf.Sqrt(ox * ox + oy * oy);
                        if (distance > radius) continue;
                        var neighborAlpha = source[ToIndex(nx, ny)].a;
                        if (neighborAlpha <= 0.01f) continue;
                        glow = Mathf.Max(glow, neighborAlpha * (1f - distance / (radius + 0.01f)));
                    }
                }

                if (glow <= 0.01f) continue;
                var c = glowColor;
                c.a = Mathf.Clamp01(glow * alpha * (1f - frame[index].a * 0.35f));
                frame[index] = AlphaBlend(frame[index], c);
            }
        }
    }

    private static Color[] ReadSpritePixels(Sprite sprite)
    {
        var pixels = CreateEmptyPixels();
        try
        {
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            var sourceWidth = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var sourceHeight = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var scale = Mathf.Min((CanvasSize - 8f) / sourceWidth, (CanvasSize - 8f) / sourceHeight);
            var drawWidth = Mathf.Clamp(Mathf.RoundToInt(sourceWidth * scale), 1, CanvasSize);
            var drawHeight = Mathf.Clamp(Mathf.RoundToInt(sourceHeight * scale), 1, CanvasSize);
            var offsetX = (CanvasSize - drawWidth) / 2;
            var offsetY = (CanvasSize - drawHeight) / 2;

            for (var y = 0; y < drawHeight; y++)
            {
                var sourceY = Mathf.Clamp(
                    Mathf.FloorToInt(rect.y + y / Mathf.Max(1f, drawHeight - 1f) * (sourceHeight - 1)),
                    0, texture.height - 1);
                for (var x = 0; x < drawWidth; x++)
                {
                    var sourceX = Mathf.Clamp(
                        Mathf.FloorToInt(rect.x + x / Mathf.Max(1f, drawWidth - 1f) * (sourceWidth - 1)),
                        0, texture.width - 1);
                    pixels[ToIndex(offsetX + x, offsetY + y)] = texture.GetPixel(sourceX, sourceY);
                }
            }

            return pixels;
        }
        catch
        {
            if (LoggedUnreadableSprites.Add(sprite.GetInstanceID()))
            {
                ModClass.LogWarning($"[TalismanVfx] 无法读取符箓图标像素，使用默认符纸轮廓: {sprite.name}");
            }

            return BuildFallbackPaper();
        }
    }

    private static Color[] BuildFallbackPaper()
    {
        var pixels = CreateEmptyPixels();
        var paper = new Color(0.86f, 0.72f, 0.36f, 1f);
        var ink = new Color(0.35f, 0.08f, 0.03f, 1f);
        for (var y = 12; y < 54; y++)
        {
            for (var x = 20; x < 44; x++)
            {
                pixels[ToIndex(x, y)] = paper;
            }
        }

        for (var y = 20; y < 47; y += 7)
        {
            for (var x = 25; x < 39; x++)
            {
                pixels[ToIndex(x, y)] = ink;
            }
        }

        return pixels;
    }

    private static Sprite CreateSprite(Color[] pixels, string name)
    {
        var texture = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, CanvasSize, CanvasSize), new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        sprite.name = name;
        return sprite;
    }

    private static Color[] CreateEmptyPixels()
    {
        var pixels = new Color[CanvasSize * CanvasSize];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        return pixels;
    }

    private static Color AlphaBlend(Color background, Color foreground)
    {
        var a = foreground.a + background.a * (1f - foreground.a);
        if (a <= 0.001f) return Color.clear;
        return new Color(
            (foreground.r * foreground.a + background.r * background.a * (1f - foreground.a)) / a,
            (foreground.g * foreground.a + background.g * background.a * (1f - foreground.a)) / a,
            (foreground.b * foreground.a + background.b * background.a * (1f - foreground.a)) / a,
            a);
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            var n = x * 73856093 ^ y * 19349663 ^ 83492791;
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static float FastBurnProgress(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 1.35f);
    }

    private static int ToIndex(int x, int y)
    {
        return y * CanvasSize + x;
    }
}
