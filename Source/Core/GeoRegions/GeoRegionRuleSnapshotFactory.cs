using System;
using System.Collections.Generic;
using Cultiway.Core.GeoRegions.Partitioning;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程读取地区分类配置，并复制成一份后台计算可以安全使用的纯数据。
/// 后台只读取这份副本，不会直接碰游戏资产或主线程状态。
/// </summary>
internal static class GeoRegionRuleSnapshotFactory
{
    /// <summary>
    /// 复制当前世界所需的全部地区分类规则、生物群系编号和划分参数。
    /// 返回结果带有世界、地图尺寸和数据版本，便于调用方发现混用了不同批次的数据。
    /// </summary>
    internal static GeoRegionRuleSnapshot Capture(
        GeoRegionLibrary library,
        int worldSeedId,
        int width,
        int height,
        int revision)
    {
        if (library == null) throw new ArgumentNullException(nameof(library));

        var rules = new List<GeoRegionCategoryRule>((int)GeoRegionCategoryCode.Archipelago);
        Add(rules, library.PrimarySea, GeoRegionCategoryCode.PrimarySea);
        Add(rules, library.PrimaryLake, GeoRegionCategoryCode.PrimaryLake);
        Add(rules, library.PrimaryRiver, GeoRegionCategoryCode.PrimaryRiver);
        Add(rules, library.PrimaryLava, GeoRegionCategoryCode.PrimaryLava);
        Add(rules, library.PrimaryGoo, GeoRegionCategoryCode.PrimaryGoo);
        Add(rules, library.PrimaryMountains, GeoRegionCategoryCode.PrimaryMountains, GeoRegionPrimaryCategoryCode.Mountains);
        Add(rules, library.PrimaryGrassland, GeoRegionCategoryCode.PrimaryGrassland, GeoRegionPrimaryCategoryCode.Grassland);
        Add(rules, library.PrimaryForest, GeoRegionCategoryCode.PrimaryForest, GeoRegionPrimaryCategoryCode.Forest);
        Add(rules, library.PrimaryJungle, GeoRegionCategoryCode.PrimaryJungle, GeoRegionPrimaryCategoryCode.Jungle);
        Add(rules, library.PrimarySwamp, GeoRegionCategoryCode.PrimarySwamp, GeoRegionPrimaryCategoryCode.Swamp);
        Add(rules, library.PrimaryDesert, GeoRegionCategoryCode.PrimaryDesert, GeoRegionPrimaryCategoryCode.Desert);
        Add(rules, library.PrimaryBeach, GeoRegionCategoryCode.PrimaryBeach, GeoRegionPrimaryCategoryCode.Beach);
        Add(rules, library.PrimaryTundra, GeoRegionCategoryCode.PrimaryTundra, GeoRegionPrimaryCategoryCode.Tundra);
        Add(rules, library.PrimaryHighlands, GeoRegionCategoryCode.PrimaryHighlands, GeoRegionPrimaryCategoryCode.Highlands);
        Add(rules, library.PrimaryWasteland, GeoRegionCategoryCode.PrimaryWasteland, GeoRegionPrimaryCategoryCode.Wasteland);
        Add(rules, library.PrimarySpecial, GeoRegionCategoryCode.PrimarySpecial, GeoRegionPrimaryCategoryCode.Special);
        Add(rules, library.LandformPlain, GeoRegionCategoryCode.LandformPlain, landformCode: GeoRegionLandformCode.Plain);
        Add(rules, library.LandformMountain, GeoRegionCategoryCode.LandformMountain, landformCode: GeoRegionLandformCode.Mountain);
        Add(rules, library.LandformCanyon, GeoRegionCategoryCode.LandformCanyon, landformCode: GeoRegionLandformCode.Canyon);
        Add(rules, library.LandformBasin, GeoRegionCategoryCode.LandformBasin, landformCode: GeoRegionLandformCode.Basin);
        Add(rules, library.LandmassIsland, GeoRegionCategoryCode.LandmassIsland);
        Add(rules, library.LandmassContinent, GeoRegionCategoryCode.LandmassContinent);
        Add(rules, library.LandmassMainland, GeoRegionCategoryCode.LandmassMainland);
        Add(rules, library.Peninsula, GeoRegionCategoryCode.Peninsula);
        Add(rules, library.Strait, GeoRegionCategoryCode.Strait);
        Add(rules, library.Archipelago, GeoRegionCategoryCode.Archipelago);

        return new GeoRegionRuleSnapshot(
            worldSeedId,
            width,
            height,
            revision,
            rules,
            CaptureBiomeIds(),
            new GeoRegionPartitionParameters());
    }

    /// <summary>
    /// 收集游戏当前注册的生物群系编号，去重并排序，使后台计算不依赖资产列表顺序。
    /// </summary>
    private static string[] CaptureBiomeIds()
    {
        BiomeLibrary biomeLibrary = AssetManager.biome_library ??
                                    throw new InvalidOperationException("GeoRegion 捕获规则时 biome library 尚未初始化");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < biomeLibrary.list.Count; i++)
        {
            string biomeId = biomeLibrary.list[i]?.id;
            if (!string.IsNullOrWhiteSpace(biomeId)) unique.Add(biomeId.Trim());
        }
        var result = new List<string>(unique);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    /// <summary>
    /// 把游戏使用的地形层枚举转换成地区划分使用的普通枚举值。
    /// </summary>
    internal static GeoRegionTerrainLayer EncodeLayer(TileLayerType layer)
    {
        return layer switch
        {
            TileLayerType.Ground => GeoRegionTerrainLayer.Ground,
            TileLayerType.Ocean => GeoRegionTerrainLayer.Ocean,
            TileLayerType.Lava => GeoRegionTerrainLayer.Lava,
            TileLayerType.Block => GeoRegionTerrainLayer.Block,
            TileLayerType.Goo => GeoRegionTerrainLayer.Goo,
            _ => GeoRegionTerrainLayer.None
        };
    }

    /// <summary>
    /// 校验并复制一个地区分类配置，把游戏资产中的可变数组和筛选条件写入规则列表。
    /// </summary>
    private static void Add(
        List<GeoRegionCategoryRule> target,
        GeoRegionAsset asset,
        GeoRegionCategoryCode categoryCode,
        GeoRegionPrimaryCategoryCode primaryCode = GeoRegionPrimaryCategoryCode.None,
        GeoRegionLandformCode landformCode = GeoRegionLandformCode.None)
    {
        if (asset == null)
        {
            throw new InvalidOperationException($"GeoRegionLibrary 缺少分区资产: code={categoryCode}");
        }

        ValidateLayer(asset, categoryCode);
        target.Add(new GeoRegionCategoryRule(
            asset.id,
            asset.Layer,
            categoryCode,
            primaryCode,
            landformCode,
            asset.Priority,
            asset.MinTiles,
            asset.MaxTiles,
            asset.BiomeIds,
            asset.TileTypeIds,
            EncodeLayers(asset.LayerTypes),
            asset.RequireOceanFlag,
            asset.RequireFillableWaterFlag,
            asset.RequireLavaFlag,
            asset.RequireGooFlag,
            asset.RequireMountainFlag,
            asset.MinNeighborWater,
            asset.MaxDistanceToWater,
            asset.MinNeighborBlock,
            asset.MinNeighborPit,
            asset.RequireOppositeBlockPair,
            asset.MaxThickness,
            asset.MinCoastRatio,
            asset.MaxNeckRatio,
            asset.MaxHalfWidth,
            asset.MinExits,
            asset.MinAspectRatio,
            asset.IslandMaxTiles,
            asset.MaxGap,
            asset.MinIslands,
            asset.MinTotalTiles));
    }

    /// <summary>
    /// 批量转换一个分类允许出现的地形层；没有限制时返回空数组。
    /// </summary>
    private static GeoRegionTerrainLayer[] EncodeLayers(TileLayerType[] layers)
    {
        if (layers == null || layers.Length == 0) return Array.Empty<GeoRegionTerrainLayer>();
        var result = new GeoRegionTerrainLayer[layers.Length];
        for (int i = 0; i < layers.Length; i++) result[i] = EncodeLayer(layers[i]);
        return result;
    }

    /// <summary>
    /// 确认分类编码所属层级与配置资产声明的层级一致，防止规则被放到错误的地区层。
    /// </summary>
    private static void ValidateLayer(GeoRegionAsset asset, GeoRegionCategoryCode code)
    {
        GeoRegionLayer expected = code switch
        {
            >= GeoRegionCategoryCode.PrimarySea and <= GeoRegionCategoryCode.PrimarySpecial => GeoRegionLayer.Primary,
            >= GeoRegionCategoryCode.LandformPlain and <= GeoRegionCategoryCode.LandformBasin => GeoRegionLayer.Landform,
            >= GeoRegionCategoryCode.LandmassIsland and <= GeoRegionCategoryCode.LandmassMainland => GeoRegionLayer.Landmass,
            GeoRegionCategoryCode.Peninsula => GeoRegionLayer.Peninsula,
            GeoRegionCategoryCode.Strait => GeoRegionLayer.Strait,
            GeoRegionCategoryCode.Archipelago => GeoRegionLayer.Archipelago,
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };

        if (asset.Layer != expected)
        {
            throw new InvalidOperationException(
                $"GeoRegion 分区资产层级不一致: id={asset.id}, actual={asset.Layer}, expected={expected}");
        }
    }
}
