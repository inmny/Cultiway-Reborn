using System;
using System.Collections.Generic;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Core;

/// <summary>
/// 按地区实际地块轮廓绘制小图标，并暂存已经生成的图片供旗帜和界面重复使用。
/// 地区边界或地块颜色变化时会重画，世界清空时统一释放 Unity 图片资源。
/// </summary>
public static class GeoRegionShapeSpriteCache
{
    // 固定图片尺寸与留白，保证不同地区图标在界面中大小一致且轮廓不贴边。
    private const int TextureSize = 32;
    private const int TransparentBorder = 8;
    private const int OutlinePadding = 1;
    private const int ShapePadding = TransparentBorder + OutlinePadding;
    // 按地区编号保存图片资源和生成依据，内容未变时直接复用。
    private static readonly Dictionary<long, Entry> Cache = new();

    /// <summary>
    /// 取得地区轮廓图标；地块数据未就绪或地区为空时使用分类图标，内容变化时重新绘制。
    /// </summary>
    public static Sprite GetSprite(GeoRegion region)
    {
        if (region == null) throw new InvalidOperationException("GeoRegion 为空");
        if (region.data == null) throw new InvalidOperationException($"GeoRegion 数据为空: id={region.getID()}");

        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager == null || !manager.CanResolveRegionTiles())
        {
            return region.GetCategory().GetSpriteIcon();
        }

        List<WorldTile> tiles = CollectTiles(region, out int minX, out int minY, out int maxX, out int maxY);
        if (tiles.Count == 0)
        {
            if (region.data.TileCount > 0)
            {
                throw new InvalidOperationException(
                    $"GeoRegion tile 索引为空但 TileCount 非零: id={region.getID()}, tiles={region.data.TileCount}");
            }

            return region.GetCategory().GetSpriteIcon();
        }

        long regionId = region.getID();
        string key = BuildKey(region, tiles);
        if (Cache.TryGetValue(regionId, out Entry entry) && entry.Key == key)
        {
            return entry.Sprite;
        }

        if (!Cache.TryGetValue(regionId, out entry))
        {
            entry = CreateEntry(regionId);
            Cache[regionId] = entry;
        }

        RenderRegion(tiles, minX, minY, maxX, maxY, entry.Texture);
        entry.Key = key;
        return entry.Sprite;
    }

    /// <summary>
    /// 标记地区图标需要更新；地区已删除时释放图片，否则立即按当前地块重新绘制。
    /// </summary>
    public static void Invalidate(GeoRegion region)
    {
        if (region?.data == null) return;
        long regionId = region.getID();
        if (!Cache.TryGetValue(regionId, out Entry entry)) return;

        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (region.isRekt() || manager == null || manager.GetTileCount(region) == 0)
        {
            DestroyEntry(entry);
            Cache.Remove(regionId);
            return;
        }

        entry.Key = null;
        _ = GetSprite(region);
    }

    /// <summary>释放所有已生成的图片和纹理，世界切换或地区系统清空时调用。</summary>
    public static void Clear()
    {
        foreach (Entry entry in Cache.Values) DestroyEntry(entry);
        Cache.Clear();
    }

    /// <summary>销毁一项中的 Unity 图片和纹理资源。</summary>
    private static void DestroyEntry(Entry entry)
    {
        if (entry?.Sprite != null) Object.Destroy(entry.Sprite);
        if (entry?.Texture != null) Object.Destroy(entry.Texture);
    }

    /// <summary>为指定地区创建固定尺寸、像素风过滤的空纹理和图片对象。</summary>
    private static Entry CreateEntry(long regionId)
    {
        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = $"GeoRegionShapeTexture_{regionId}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = $"GeoRegionShapeSprite_{regionId}";
        return new Entry(texture, sprite);
    }

    /// <summary>
    /// 汇总会影响图标的地区字段、地块类型和颜色，生成用于判断是否需要重画的标识。
    /// </summary>
    private static string BuildKey(GeoRegion region, List<WorldTile> tiles)
    {
        GeoRegionData data = region.data;
        long tileChecksum = GetTileChecksum(tiles);
        return $"{region.getID()}|border={TransparentBorder}|{(int)data.Layer}|{data.CategoryId}|{data.TileCount}|{data.CenterX}|{data.CenterY}|tiles={tileChecksum}";
    }

    /// <summary>把地区地块缩放到纹理中央，填充各格平均颜色并绘制深色外轮廓。</summary>
    private static void RenderRegion(
        List<WorldTile> tiles,
        int minX,
        int minY,
        int maxX,
        int maxY,
        Texture2D texture)
    {
        Color32[] pixels = new Color32[TextureSize * TextureSize];
        bool[] mask = new bool[pixels.Length];
        long[] red = new long[pixels.Length];
        long[] green = new long[pixels.Length];
        long[] blue = new long[pixels.Length];
        int[] counts = new int[pixels.Length];

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        float scale = (TextureSize - ShapePadding * 2) / (float)Mathf.Max(width, height);
        float offsetX = (TextureSize - width * scale) * 0.5f;
        float offsetY = (TextureSize - height * scale) * 0.5f;

        for (int i = 0; i < tiles.Count; i++)
        {
            WorldTile tile = tiles[i];
            int x0 = Mathf.Clamp(Mathf.FloorToInt(offsetX + (tile.x - minX) * scale), 0, TextureSize - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(offsetY + (tile.y - minY) * scale), 0, TextureSize - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(offsetX + (tile.x - minX + 1) * scale) - 1, x0, TextureSize - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(offsetY + (tile.y - minY + 1) * scale) - 1, y0, TextureSize - 1);

            Color32 tileColor = tile.GetCurrentColor();
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int index = y * TextureSize + x;
                    mask[index] = true;
                    red[index] += tileColor.r;
                    green[index] += tileColor.g;
                    blue[index] += tileColor.b;
                    counts[index]++;
                }
            }
        }

        TextureMaskUtils.ApplyAverageColors(mask, pixels, red, green, blue, counts);
        TextureMaskUtils.DrawOutline(mask, pixels, TextureSize, TextureSize, GetOutlineColor(tiles));
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    /// <summary>收集地区全部地块，并同时计算最小包围范围供绘图缩放。</summary>
    private static List<WorldTile> CollectTiles(GeoRegion region, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;

        int capacity = Mathf.Max(16, region.data.TileCount);
        List<WorldTile> tiles = new(capacity);
        foreach (WorldTile tile in WorldboxGame.I.GeoRegions.EnumerateRegionTiles(region))
        {
            tiles.Add(tile);
            if (tile.x < minX) minX = tile.x;
            if (tile.y < minY) minY = tile.y;
            if (tile.x > maxX) maxX = tile.x;
            if (tile.y > maxY) maxY = tile.y;
        }

        return tiles;
    }

    /// <summary>把地块编号、类型和颜色合并成快速比较值，用于发现图标内容变化。</summary>
    private static long GetTileChecksum(List<WorldTile> tiles)
    {
        unchecked
        {
            long checksum = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                WorldTile tile = tiles[i];
                TileTypeBase type = tile.GetCurrentType();
                Color32 color = tile.GetCurrentColor();
                long colorKey = color.r | ((long)color.g << 8) | ((long)color.b << 16) | ((long)color.a << 24);
                checksum += ((long)tile.data.tile_id + 1) * 73856093L;
                checksum ^= ((long)type.index_id + 1) * 19349663L;
                checksum += colorKey * 83492791L;
            }

            return checksum;
        }
    }

    /// <summary>根据地区平均颜色调暗并设置透明度，得到清晰且协调的轮廓色。</summary>
    private static Color32 GetOutlineColor(List<WorldTile> tiles)
    {
        Color32 color = tiles.GetAverageCurrentColor();
        color = ColorUtils.Blend(color, new Color32(0, 0, 0, 255), 0.45f);
        return ColorUtils.WithAlpha(color, 230);
    }

    /// <summary>保存一个地区可重复使用的纹理、图片，以及上次绘制所依据的内容标识。</summary>
    private sealed class Entry
    {
        /// <summary>实际写入像素颜色的纹理。</summary>
        public readonly Texture2D Texture;
        /// <summary>供界面和旗帜直接使用的图片对象。</summary>
        public readonly Sprite Sprite;
        /// <summary>上次绘制内容的标识；为空表示必须重画。</summary>
        public string Key;

        /// <summary>把成对创建的纹理和图片保存为一项。</summary>
        public Entry(Texture2D texture, Sprite sprite)
        {
            Texture = texture;
            Sprite = sprite;
        }
    }
}
