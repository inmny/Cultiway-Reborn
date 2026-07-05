using System;
using UnityEngine;

namespace Cultiway.Utils;

public static class SpritePixelUtils
{
    /// <summary>
    /// 测量 sprite 底部指定数量非透明行的横向跨度，包含两段像素之间的透明间隔。
    /// </summary>
    public static int MeasureBottomOpaqueSpan(Sprite sprite, int rowCount = 2)
    {
        if (sprite == null || rowCount <= 0) return 0;

        Texture2D tex = sprite.texture;
        if (tex == null) return 0;

        try
        {
            Rect rect = sprite.textureRect;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(rect.x), 0, tex.width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(rect.y), 0, tex.height);
            int x1 = Mathf.Clamp(x0 + Mathf.CeilToInt(rect.width), x0, tex.width);
            int y1 = Mathf.Clamp(y0 + Mathf.CeilToInt(rect.height), y0, tex.height);
            if (x1 <= x0 || y1 <= y0) return 0;

            Color32[] pixels = tex.GetPixels32();
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int rowsFound = 0;

            for (int y = y0; y < y1 && rowsFound < rowCount; y++)
            {
                int rowMinX = int.MaxValue;
                int rowMaxX = int.MinValue;
                int row = y * tex.width;

                for (int x = x0; x < x1; x++)
                {
                    if (pixels[row + x].a == 0) continue;

                    rowMinX = Math.Min(rowMinX, x);
                    rowMaxX = Math.Max(rowMaxX, x);
                }

                if (rowMaxX < rowMinX) continue;

                rowsFound++;
                minX = Math.Min(minX, rowMinX);
                maxX = Math.Max(maxX, rowMaxX);
            }

            return maxX >= minX ? maxX - minX + 1 : 0;
        }
        catch (UnityException)
        {
            return 0;
        }
    }
}
