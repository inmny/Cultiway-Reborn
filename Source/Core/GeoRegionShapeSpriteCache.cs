using System;
using System.Collections.Generic;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Core;

public static class GeoRegionShapeSpriteCache
{
    private const int TextureSize = 32;
    private const int TransparentBorder = 8;
    private const int OutlinePadding = 1;
    private const int ShapePadding = TransparentBorder + OutlinePadding;
    private static readonly Dictionary<long, Entry> Cache = new();

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
                    $"GeoRegion tile 关系为空但 TileCount 非零: id={region.getID()}, tiles={region.data.TileCount}");
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

    public static void Clear()
    {
        foreach (Entry entry in Cache.Values)
        {
            if (entry.Sprite != null) Object.Destroy(entry.Sprite);
            if (entry.Texture != null) Object.Destroy(entry.Texture);
        }

        Cache.Clear();
    }

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

    private static string BuildKey(GeoRegion region, List<WorldTile> tiles)
    {
        GeoRegionData data = region.data;
        long tileChecksum = GetTileChecksum(tiles);
        return $"{region.getID()}|border={TransparentBorder}|{(int)data.Layer}|{data.CategoryId}|{data.TileCount}|{data.CenterX}|{data.CenterY}|tiles={tileChecksum}";
    }

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

    private static Color32 GetOutlineColor(List<WorldTile> tiles)
    {
        Color32 color = tiles.GetAverageCurrentColor();
        color = ColorUtils.Blend(color, new Color32(0, 0, 0, 255), 0.45f);
        return ColorUtils.WithAlpha(color, 230);
    }

    private sealed class Entry
    {
        public readonly Texture2D Texture;
        public readonly Sprite Sprite;
        public string Key;

        public Entry(Texture2D texture, Sprite sprite)
        {
            Texture = texture;
            Sprite = sprite;
        }
    }
}
