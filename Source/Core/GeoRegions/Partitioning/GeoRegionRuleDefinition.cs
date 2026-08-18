using System;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 判断某格符合哪条地区规则时使用的现场数据，包括该格自身、周围格子和离水距离。
/// </summary>
internal readonly struct GeoRegionTerrainRuleContext
{
    /// <summary>创建某格的规则判断现场数据。</summary>
    internal GeoRegionTerrainRuleContext(
        in GeoRegionTerrainCell cell,
        int neighborWaterCount,
        int neighborWater8Count,
        int distanceToWater,
        int neighborBlockCount,
        int neighborPitCount,
        bool hasOppositeBlockPair)
    {
        Cell = cell;
        NeighborWaterCount = neighborWaterCount;
        NeighborWater8Count = neighborWater8Count;
        DistanceToWater = distanceToWater;
        NeighborBlockCount = neighborBlockCount;
        NeighborPitCount = neighborPitCount;
        HasOppositeBlockPair = hasOppositeBlockPair;
    }

    /// <summary>当前要判断的格子。</summary>
    internal GeoRegionTerrainCell Cell { get; }
    /// <summary>上下左右四格中水格的数量。</summary>
    internal int NeighborWaterCount { get; }
    /// <summary>连同斜角在内的周围八格中水格的数量。</summary>
    internal int NeighborWater8Count { get; }
    /// <summary>到最近水格的步数；找不到时为负数。</summary>
    internal int DistanceToWater { get; }
    /// <summary>上下左右四格中阻挡格的数量。</summary>
    internal int NeighborBlockCount { get; }
    /// <summary>上下左右四格中可填坑格的数量。</summary>
    internal int NeighborPitCount { get; }
    /// <summary>当前格两侧是否有一对相对的阻挡格。</summary>
    internal bool HasOppositeBlockPair { get; }
}

/// <summary>
/// 一条地区分类规则的固定副本，保存允许的地表以及面积、邻格和形状条件。
/// </summary>
internal sealed class GeoRegionCategoryRule
{
    /// <summary>这条规则允许的生物群系标识；空数组表示不限。</summary>
    private readonly string[] biomeIds;
    /// <summary>这条规则允许的地块材质标识；空数组表示不限。</summary>
    private readonly string[] tileTypeIds;
    /// <summary>这条规则允许的原始地面层；空数组表示不限。</summary>
    private readonly GeoRegionTerrainLayer[] layerTypes;

    /// <summary>创建一条只读分类规则，并复制传入的允许列表。</summary>
    internal GeoRegionCategoryRule(
        string id,
        GeoRegionLayer layer,
        GeoRegionCategoryCode categoryCode,
        GeoRegionPrimaryCategoryCode primaryCode,
        GeoRegionLandformCode landformCode,
        int priority,
        int minTiles,
        int maxTiles,
        string[] biomeIds,
        string[] tileTypeIds,
        GeoRegionTerrainLayer[] layerTypes,
        bool? requireOceanMaterial,
        bool? requireFillablePit,
        bool? requireLava,
        bool? requireGoo,
        bool? requireMountain,
        int minNeighborWater,
        int maxDistanceToWater,
        int minNeighborBlock,
        int minNeighborPit,
        bool requireOppositeBlockPair,
        int maxThickness,
        float minCoastRatio,
        float maxNeckRatio,
        int maxHalfWidth,
        int minExits,
        float minAspectRatio,
        int islandMaxTiles,
        int maxGap,
        int minIslands,
        int minTotalTiles)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("GeoRegion 分类规则缺少 id", nameof(id));
        if (categoryCode == GeoRegionCategoryCode.None) throw new ArgumentOutOfRangeException(nameof(categoryCode));
        Id = id;
        Layer = layer;
        CategoryCode = categoryCode;
        PrimaryCode = primaryCode;
        LandformCode = landformCode;
        Priority = priority;
        MinTiles = minTiles;
        MaxTiles = maxTiles;
        this.biomeIds = biomeIds == null ? Array.Empty<string>() : (string[])biomeIds.Clone();
        this.tileTypeIds = tileTypeIds == null ? Array.Empty<string>() : (string[])tileTypeIds.Clone();
        this.layerTypes = layerTypes == null ? Array.Empty<GeoRegionTerrainLayer>() : (GeoRegionTerrainLayer[])layerTypes.Clone();
        RequireOceanMaterial = requireOceanMaterial;
        RequireFillablePit = requireFillablePit;
        RequireLava = requireLava;
        RequireGoo = requireGoo;
        RequireMountain = requireMountain;
        MinNeighborWater = minNeighborWater;
        MaxDistanceToWater = maxDistanceToWater;
        MinNeighborBlock = minNeighborBlock;
        MinNeighborPit = minNeighborPit;
        RequireOppositeBlockPair = requireOppositeBlockPair;
        MaxThickness = maxThickness;
        MinCoastRatio = minCoastRatio;
        MaxNeckRatio = maxNeckRatio;
        MaxHalfWidth = maxHalfWidth;
        MinExits = minExits;
        MinAspectRatio = minAspectRatio;
        IslandMaxTiles = islandMaxTiles;
        MaxGap = maxGap;
        MinIslands = minIslands;
        MinTotalTiles = minTotalTiles;
    }

    /// <summary>规则的唯一标识。</summary>
    internal string Id { get; }
    /// <summary>规则生成的地区层。</summary>
    internal GeoRegionLayer Layer { get; }
    /// <summary>规则生成的最终地区类别。</summary>
    internal GeoRegionCategoryCode CategoryCode { get; }
    /// <summary>规则对应的主要地表类别；不适用时为 None。</summary>
    internal GeoRegionPrimaryCategoryCode PrimaryCode { get; }
    /// <summary>规则对应的陆地外形类别；不适用时为 None。</summary>
    internal GeoRegionLandformCode LandformCode { get; }
    /// <summary>多条规则同时符合时的优先级，数值越大越先判断。</summary>
    internal int Priority { get; }
    /// <summary>地区允许的最少格子数。</summary>
    internal int MinTiles { get; }
    /// <summary>地区允许的最多格子数。</summary>
    internal int MaxTiles { get; }
    /// <summary>是否必须是海洋材质；没有值表示不限。</summary>
    internal bool? RequireOceanMaterial { get; }
    /// <summary>是否必须是可填坑；没有值表示不限。</summary>
    internal bool? RequireFillablePit { get; }
    /// <summary>是否必须是熔岩；没有值表示不限。</summary>
    internal bool? RequireLava { get; }
    /// <summary>是否必须是黏液；没有值表示不限。</summary>
    internal bool? RequireGoo { get; }
    /// <summary>是否必须是山体；没有值表示不限。</summary>
    internal bool? RequireMountain { get; }
    /// <summary>上下左右至少需要多少个水格。</summary>
    internal int MinNeighborWater { get; }
    /// <summary>离最近水格最多允许多少步；负数表示不限。</summary>
    internal int MaxDistanceToWater { get; }
    /// <summary>上下左右至少需要多少个阻挡格。</summary>
    internal int MinNeighborBlock { get; }
    /// <summary>上下左右至少需要多少个可填坑格。</summary>
    internal int MinNeighborPit { get; }
    /// <summary>是否要求当前格两侧存在相对的阻挡格。</summary>
    internal bool RequireOppositeBlockPair { get; }
    /// <summary>狭长地区允许的最大厚度。</summary>
    internal int MaxThickness { get; }
    /// <summary>地区边缘接触海岸的最低比例。</summary>
    internal float MinCoastRatio { get; }
    /// <summary>半岛与陆地连接处相对面积的最高比例。</summary>
    internal float MaxNeckRatio { get; }
    /// <summary>狭窄地区从中线到边缘允许的最大宽度。</summary>
    internal int MaxHalfWidth { get; }
    /// <summary>水道至少需要连接的出口数。</summary>
    internal int MinExits { get; }
    /// <summary>地区长边与短边的最低比例。</summary>
    internal float MinAspectRatio { get; }
    /// <summary>群岛中单座岛允许的最多格子数。</summary>
    internal int IslandMaxTiles { get; }
    /// <summary>群岛内岛屿之间允许的最大间隔。</summary>
    internal int MaxGap { get; }
    /// <summary>形成群岛至少需要多少座岛。</summary>
    internal int MinIslands { get; }
    /// <summary>形成群岛的所有岛屿至少需要多少格。</summary>
    internal int MinTotalTiles { get; }

    /// <summary>判断给定生物群系是否在允许列表中；规则未限制时始终符合。</summary>
    internal bool MatchesBiome(string value)
    {
        return MatchesString(biomeIds, value);
    }

    /// <summary>判断给定地块材质是否在允许列表中；规则未限制时始终符合。</summary>
    internal bool MatchesTileType(string value)
    {
        return MatchesString(tileTypeIds, value);
    }

    /// <summary>复制允许的地块材质标识，避免外部改动规则内容。</summary>
    internal string[] CopyTileTypeIds()
    {
        return (string[])tileTypeIds.Clone();
    }

    /// <summary>复制允许的原始地面层，避免外部改动规则内容。</summary>
    internal GeoRegionTerrainLayer[] CopyLayerTypes()
    {
        return (GeoRegionTerrainLayer[])layerTypes.Clone();
    }

    /// <summary>规则是否限定了生物群系。</summary>
    internal bool HasBiomeRestriction => biomeIds.Length > 0;
    /// <summary>规则是否限定了地块材质。</summary>
    internal bool HasTileTypeRestriction => tileTypeIds.Length > 0;

    /// <summary>判断给定原始地面层是否在允许列表中；规则未限制时始终符合。</summary>
    internal bool MatchesLayer(GeoRegionTerrainLayer value)
    {
        if (layerTypes.Length == 0) return true;
        for (int i = 0; i < layerTypes.Length; i++)
        {
            if (layerTypes[i] == value) return true;
        }
        return false;
    }

    /// <summary>复制允许的生物群系标识，避免外部改动规则内容。</summary>
    internal string[] CopyBiomeIds()
    {
        return (string[])biomeIds.Clone();
    }

    /// <summary>在允许字符串列表中查找完全相同的值；空列表代表不限。</summary>
    private static bool MatchesString(string[] candidates, string value)
    {
        if (candidates.Length == 0) return true;
        if (string.IsNullOrEmpty(value)) return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (!string.IsNullOrEmpty(candidate) && string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 控制大片水域如何拆分的固定数值，随规则一起交给后台分区计算。
/// </summary>
internal sealed class GeoRegionPartitionParameters
{
    /// <summary>创建当前使用的大片水域拆分参数。</summary>
    internal GeoRegionPartitionParameters()
    {
        LargeWaterSqrtScale = 7.0;
        ClosedWaterSqrtScale = 8.0;
        LargeWaterSplitDivisor = 4;
        WaterSplitJitterRadius = 1;
        LargeWaterForcedSplitMultiplier = 12;
        ClosedWaterDirectFloor = 64;
        ClosedWaterDirectLakeMultiplier = 6;
    }

    /// <summary>估算开放大水域目标大小时使用的平方根倍数。</summary>
    internal double LargeWaterSqrtScale { get; }
    /// <summary>估算封闭大水域目标大小时使用的平方根倍数。</summary>
    internal double ClosedWaterSqrtScale { get; }
    /// <summary>开放大水域拆分数量的除数。</summary>
    internal int LargeWaterSplitDivisor { get; }
    /// <summary>水域拆分中心可随机偏移的格子半径。</summary>
    internal int WaterSplitJitterRadius { get; }
    /// <summary>水域达到普通上限多少倍后必须拆分。</summary>
    internal int LargeWaterForcedSplitMultiplier { get; }
    /// <summary>封闭水域可直接保留的最少上限。</summary>
    internal int ClosedWaterDirectFloor { get; }
    /// <summary>封闭水域相对湖泊上限的直接保留倍数。</summary>
    internal int ClosedWaterDirectLakeMultiplier { get; }
}
