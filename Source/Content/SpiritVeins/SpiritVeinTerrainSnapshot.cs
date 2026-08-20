using System;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>灵脉生成器使用的单格简单地形数据，不引用游戏对象。</summary>
internal readonly struct SpiritVeinTerrainCell
{
    internal SpiritVeinTerrainCell(
        bool water,
        bool mountain,
        bool highland,
        bool lava,
        bool goo,
        bool beach,
        string biomeId,
        string primaryRegionName,
        string landformRegionName,
        ElementComposition composition)
    {
        IsWater = water;
        IsMountain = mountain;
        IsHighland = highland;
        IsLava = lava;
        IsGoo = goo;
        IsBeach = beach;
        BiomeId = biomeId ?? string.Empty;
        PrimaryRegionName = primaryRegionName ?? string.Empty;
        LandformRegionName = landformRegionName ?? string.Empty;
        Composition = composition;
    }

    internal bool IsWater { get; }
    internal bool IsMountain { get; }
    internal bool IsHighland { get; }
    internal bool IsLava { get; }
    internal bool IsGoo { get; }
    internal bool IsBeach { get; }
    internal string BiomeId { get; }
    internal string PrimaryRegionName { get; }
    internal string LandformRegionName { get; }
    internal ElementComposition Composition { get; }
    internal bool IsUsableLand => !IsWater && !IsLava && !IsGoo;
    internal int Height => Mathf.RoundToInt(
        SpiritVeinSettings.ResolveTerrainHeight(IsMountain, IsHighland, IsWater));
}

/// <summary>当前世界的一次灵脉生成输入快照。</summary>
internal sealed class SpiritVeinTerrainSnapshot
{
    internal SpiritVeinTerrainSnapshot(int worldSeedId, int width, int height, SpiritVeinTerrainCell[] cells)
    {
        int expectedCount = checked(width * height);
        if (cells == null || cells.Length != expectedCount)
            throw new ArgumentException("灵脉地形快照格子数量不正确", nameof(cells));
        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Cells = cells;
    }

    internal int WorldSeedId { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal SpiritVeinTerrainCell[] Cells { get; }
    internal int CellCount => Cells?.Length ?? 0;

    internal SpiritVeinTerrainCell this[int tileId] => Cells[tileId];

    internal static SpiritVeinTerrainSnapshot CaptureCurrentWorld(
        int worldSeedId,
        int width,
        int height)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        int expectedCount = checked(width * height);
        if (tiles == null || tiles.Length != expectedCount)
        {
            throw new InvalidOperationException(
                $"灵脉地形快照尺寸不匹配: map={width}x{height}, tiles={tiles?.Length ?? 0}");
        }

        var cells = new SpiritVeinTerrainCell[expectedCount];
        for (int tileId = 0; tileId < expectedCount; tileId++)
        {
            cells[tileId] = CaptureCell(tiles[tileId], tileId);
        }

        return new SpiritVeinTerrainSnapshot(worldSeedId, width, height, cells);
    }

    /// <summary>在主线程把一个游戏地块转换为后台可用的简单数据。</summary>
    internal static SpiritVeinTerrainCell CaptureCell(WorldTile tile, int tileId)
    {
        if (tile == null || tile.data == null || tile.data.tile_id != tileId)
        {
            throw new InvalidOperationException($"灵脉地形快照遇到错位地块: tile={tileId}");
        }

        TileTypeBase mainType = tile.main_type ?? tile.Type;
        TileTypeBase surfaceType = tile.top_type ?? mainType;
        TileTypeBase displayType = tile.Type ?? surfaceType;
        if (mainType == null || surfaceType == null)
        {
            throw new InvalidOperationException($"灵脉地形快照缺少地块类型: tile={tileId}");
        }

        bool lava = mainType.lava;
        bool goo = mainType.grey_goo;
        bool water = mainType.ocean && !lava && !goo && !mainType.block;
        bool mountain = mainType.mountains || mainType.edge_mountains || displayType.summit;
        bool highland = mountain || displayType.edge_hills || displayType.rocks;
        string biomeId = surfaceType.is_biome ? surfaceType.biome_asset?.id : string.Empty;
        bool beach = surfaceType.sand ||
                     string.Equals(surfaceType.id, "sand", StringComparison.Ordinal) ||
                     string.Equals(surfaceType.id, "snow_sand", StringComparison.Ordinal) ||
                     string.Equals(biomeId, "biome_sand", StringComparison.Ordinal);

        GeoRegion primary = tile.GetExtend().GetGeoRegion(GeoRegionLayer.Primary);
        GeoRegion landform = tile.GetExtend().GetGeoRegion(GeoRegionLayer.Landform);
        return new SpiritVeinTerrainCell(
            water,
            mountain,
            highland,
            lava,
            goo,
            beach,
            biomeId,
            primary?.data?.name,
            landform?.data?.name,
            ResolveComposition(water, mountain, highland, lava, goo, beach, biomeId));
    }

    private static ElementComposition ResolveComposition(
        bool water,
        bool mountain,
        bool highland,
        bool lava,
        bool goo,
        bool beach,
        string biomeId)
    {
        if (lava) return new ElementComposition(fire: 0.72f, pos: 0.2f, entropy: 0.08f, normalize: true);
        if (goo) return new ElementComposition(neg: 0.25f, entropy: 0.65f, water: 0.1f, normalize: true);
        if (water) return new ElementComposition(water: 0.62f, neg: 0.16f, pos: 0.07f, normalize: true);
        if (mountain) return new ElementComposition(iron: 0.25f, earth: 0.48f, neg: 0.08f, normalize: true);

        if (!string.IsNullOrEmpty(biomeId))
        {
            if (biomeId.IndexOf("forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                biomeId.IndexOf("jungle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ElementComposition(wood: 0.62f, water: 0.16f, pos: 0.08f, normalize: true);
            }

            if (biomeId.IndexOf("desert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                biomeId.IndexOf("savanna", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ElementComposition(fire: 0.42f, earth: 0.22f, pos: 0.26f, normalize: true);
            }

            if (biomeId.IndexOf("tundra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                biomeId.IndexOf("permafrost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                biomeId.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ElementComposition(water: 0.35f, neg: 0.36f, iron: 0.12f, normalize: true);
            }
        }

        if (beach) return new ElementComposition(water: 0.35f, earth: 0.25f, pos: 0.2f, normalize: true);
        if (highland) return new ElementComposition(earth: 0.38f, wood: 0.14f, pos: 0.12f, normalize: true);
        return new ElementComposition(earth: 0.25f, wood: 0.15f, pos: 0.15f, normalize: true);
    }
}
