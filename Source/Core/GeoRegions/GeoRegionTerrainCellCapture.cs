using System;
using Cultiway.Core.GeoRegions.Partitioning;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程读取一个游戏格子的地形，并转换成后台计算可安全使用的普通数据。
/// 它只负责如实提取信息，不决定格子最终属于哪个地区。
/// </summary>
internal static class GeoRegionTerrainCellCapture
{
    /// <summary>
    /// 读取一个格子并直接合成为地区划分所需的地形数据。
    /// </summary>
    internal static GeoRegionTerrainCell Capture(
        WorldTile tile,
        int tileId,
        int width,
        GeoRegionRuleSnapshot rules)
    {
        return CaptureObservation(tile, tileId, width, rules).Compose();
    }

    /// <summary>
    /// 分别读取格子的主体结构、表面生物群系和冰冻覆盖状态，供后续判断各类变化持续了多久。
    /// </summary>
    internal static GeoRegionTerrainObservation CaptureObservation(
        WorldTile tile,
        int tileId,
        int width,
        GeoRegionRuleSnapshot rules)
    {
        ValidateTile(tile, tileId, width, rules);

        TileTypeBase mainType = tile.main_type ??
                                throw new InvalidOperationException($"GeoRegion tile 缺少 main_type: tile={tileId}");
        TileTypeBase surfaceType = tile.top_type ?? mainType;
        TileTypeBase displayType = tile.Type ?? surfaceType;

        GeoRegionTerrainLayer layer = GeoRegionRuleSnapshotFactory.EncodeLayer(mainType.layer_type);
        bool isLava = layer == GeoRegionTerrainLayer.Lava || mainType.lava;
        bool isGoo = layer == GeoRegionTerrainLayer.Goo || mainType.grey_goo;
        // 普通山地和边缘山地都按阻挡型陆地处理，避免同一种山地因标记不同被拆成两类。
        bool isBlock = layer == GeoRegionTerrainLayer.Block ||
                       mainType.block ||
                       mainType.mountains ||
                       mainType.edge_mountains;
        bool isWater = (layer == GeoRegionTerrainLayer.Ocean || mainType.ocean) &&
                       !isLava && !isGoo && !isBlock;
        GeoRegionTerrainKind terrainKind = isLava
            ? GeoRegionTerrainKind.Lava
            : isGoo
                ? GeoRegionTerrainKind.Goo
                : isBlock
                    ? GeoRegionTerrainKind.Block
                    : isWater
                        ? GeoRegionTerrainKind.Water
                        : layer == GeoRegionTerrainLayer.Ground
                            ? GeoRegionTerrainKind.Ground
                            : GeoRegionTerrainKind.Other;
        GeoRegionTerrainLayer capturedLayer = terrainKind == GeoRegionTerrainKind.Block
            ? GeoRegionTerrainLayer.Block
            : layer;

        string biomeId = surfaceType.is_biome ? surfaceType.biome_asset?.id : null;
        var structure = new GeoRegionTerrainStructure(
            capturedLayer,
            terrainKind,
            mainType.id,
            mainType.ocean,
            mainType.can_be_filled_with_ocean,
            isLava,
            isGoo,
            isBlock);
        var surface = new GeoRegionTerrainSurface(
            rules.ResolvePrimaryBiomeCode(biomeId),
            surfaceType.id,
            biomeId,
            IsBeachMaterial(surfaceType, biomeId));
        var overlay = new GeoRegionTerrainOverlay(
            tile.data.frozen,
            tile.data.frozen && terrainKind is GeoRegionTerrainKind.Ground or GeoRegionTerrainKind.Block
                ? GeoRegionPrimaryCategoryCode.Tundra
                : GeoRegionPrimaryCategoryCode.None,
            tile.data.frozen ? displayType.id : string.Empty,
            false);
        return new GeoRegionTerrainObservation(structure, surface, overlay);
    }

    /// <summary>
    /// 确认传入格子确实位于指定编号对应的坐标，避免把错位数据写入整图结果。
    /// </summary>
    private static void ValidateTile(
        WorldTile tile,
        int tileId,
        int width,
        GeoRegionRuleSnapshot rules)
    {
        if (tile == null) throw new InvalidOperationException($"GeoRegion 捕获遇到空 tile: index={tileId}");
        if (tile.data == null) throw new InvalidOperationException($"GeoRegion 捕获遇到无数据 tile: index={tileId}");
        if (rules == null) throw new ArgumentNullException(nameof(rules));

        int expectedX = tileId % width;
        int expectedY = tileId / width;
        if (tile.data.tile_id != tileId || tile.x != expectedX || tile.y != expectedY)
        {
            throw new InvalidOperationException(
                $"GeoRegion tile 布局不一致: index={tileId}, tileId={tile.data.tile_id}, " +
                $"position={tile.x},{tile.y}, expected={expectedX},{expectedY}");
        }
    }

    /// <summary>
    /// 根据地块标记、地块编号和生物群系编号判断该表面是否应按沙滩处理。
    /// </summary>
    private static bool IsBeachMaterial(TileTypeBase tileType, string biomeId)
    {
        return tileType.sand ||
               string.Equals(tileType.id, "sand", StringComparison.Ordinal) ||
               string.Equals(tileType.id, "snow_sand", StringComparison.Ordinal) ||
               string.Equals(biomeId, "biome_sand", StringComparison.Ordinal);
    }
}
