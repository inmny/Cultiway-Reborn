using System;
using UnityEngine;

namespace Cultiway.Utils;

public static class TextureMaskUtils
{
    public static void ApplyAverageColors(
        bool[] mask,
        Color32[] pixels,
        long[] red,
        long[] green,
        long[] blue,
        int[] counts)
    {
        ValidateSameLength(mask, pixels, red, green, blue, counts);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (!mask[i]) continue;
            if (counts[i] <= 0)
            {
                throw new InvalidOperationException($"写入聚合颜色失败：像素 {i} 没有颜色样本");
            }

            pixels[i] = ColorUtils.AverageRgb(red[i], green[i], blue[i], counts[i]);
        }
    }

    public static void DrawOutline(bool[] mask, Color32[] pixels, int width, int height, Color32 outline)
    {
        if (mask.Length != pixels.Length)
        {
            throw new InvalidOperationException("绘制 mask 描边失败：mask 与像素数组长度不一致");
        }

        if (mask.Length != width * height)
        {
            throw new InvalidOperationException("绘制 mask 描边失败：mask 尺寸与宽高不一致");
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (mask[index]) continue;
                if (!HasNeighbor(mask, width, height, x, y)) continue;

                pixels[index] = outline;
            }
        }
    }

    private static bool HasNeighbor(bool[] mask, int width, int height, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= height) continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                if (nx < 0 || nx >= width) continue;
                if (mask[ny * width + nx]) return true;
            }
        }

        return false;
    }

    private static void ValidateSameLength(
        bool[] mask,
        Color32[] pixels,
        long[] red,
        long[] green,
        long[] blue,
        int[] counts)
    {
        int length = pixels.Length;
        if (mask.Length == length &&
            red.Length == length &&
            green.Length == length &&
            blue.Length == length &&
            counts.Length == length)
        {
            return;
        }

        throw new InvalidOperationException("写入聚合颜色失败：输入数组长度不一致");
    }
}
