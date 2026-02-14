using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// GeoRegion 命名规则。
/// </summary>
public class GeoRegionNamingRule
{
    /// <summary>
    /// 命名模板，支持占位符：{Dir} {Biome} {Landform} {Type}。
    /// </summary>
    public string Template;
    /// <summary>
    /// 可选命名模板池（为空时回退 Template）。
    /// </summary>
    public string[] Templates;
    /// <summary>
    /// 前缀词池（如“苍/玄/灵”）。
    /// </summary>
    public string[] PrefixPool;
    /// <summary>
    /// 核心词池（如“渚/岭/泽”）。
    /// </summary>
    public string[] CorePool;
    /// <summary>
    /// 后缀词池（如“境/域/地带”）。
    /// </summary>
    public string[] SuffixPool;
    /// <summary>
    /// 是否允许方位词参与命名。
    /// </summary>
    public bool AllowDirPrefix = true;
    /// <summary>
    /// 是否允许群系词参与命名。
    /// </summary>
    public bool AllowBiomeToken = true;
    /// <summary>
    /// 是否允许地貌词参与命名。
    /// </summary>
    public bool AllowLandformToken = true;
}

/// <summary>
/// 地块规则匹配上下文（基于 tile type / biome / 邻接统计）。
/// </summary>
public readonly struct GeoRegionTileRuleContext
{
    /// <summary>
    /// 当前地块 tile type id。
    /// </summary>
    public readonly string TileTypeId;
    /// <summary>
    /// 当前地块层类型。
    /// </summary>
    public readonly TileLayerType LayerType;
    /// <summary>
    /// 当前地块 biome id。
    /// </summary>
    public readonly string BiomeId;
    /// <summary>
    /// 当前地块是否 ocean 标记。
    /// </summary>
    public readonly bool IsOceanFlag;
    /// <summary>
    /// 当前地块是否可填海标记（坑地）。
    /// </summary>
    public readonly bool IsFillableWaterFlag;
    /// <summary>
    /// 当前地块是否 lava。
    /// </summary>
    public readonly bool IsLavaFlag;
    /// <summary>
    /// 当前地块是否 goo。
    /// </summary>
    public readonly bool IsGooFlag;
    /// <summary>
    /// 当前地块是否山体标记。
    /// </summary>
    public readonly bool IsMountainFlag;
    /// <summary>
    /// 4 邻接中的水体数量。
    /// </summary>
    public readonly int NeighborWaterCount;
    /// <summary>
    /// 8 邻接中的水体数量（用于识别对角线海岸）。
    /// </summary>
    public readonly int NeighborWater8Count;
    /// <summary>
    /// 到最近水体的距离（基于沙地连通传播）。
    /// </summary>
    public readonly int DistanceToWater;
    /// <summary>
    /// 4 邻接中的阻挡地块数量。
    /// </summary>
    public readonly int NeighborBlockCount;
    /// <summary>
    /// 4 邻接中的坑地数量。
    /// </summary>
    public readonly int NeighborPitCount;
    /// <summary>
    /// 是否存在左右或上下“对向山体”。
    /// </summary>
    public readonly bool HasOppositeBlockPair;

    public GeoRegionTileRuleContext(
        string tileTypeId,
        TileLayerType layerType,
        string biomeId,
        bool isOceanFlag,
        bool isFillableWaterFlag,
        bool isLavaFlag,
        bool isGooFlag,
        bool isMountainFlag,
        int neighborWaterCount,
        int neighborWater8Count,
        int distanceToWater,
        int neighborBlockCount,
        int neighborPitCount,
        bool hasOppositeBlockPair)
    {
        TileTypeId = tileTypeId;
        LayerType = layerType;
        BiomeId = biomeId;
        IsOceanFlag = isOceanFlag;
        IsFillableWaterFlag = isFillableWaterFlag;
        IsLavaFlag = isLavaFlag;
        IsGooFlag = isGooFlag;
        IsMountainFlag = isMountainFlag;
        NeighborWaterCount = neighborWaterCount;
        NeighborWater8Count = neighborWater8Count;
        DistanceToWater = distanceToWater;
        NeighborBlockCount = neighborBlockCount;
        NeighborPitCount = neighborPitCount;
        HasOppositeBlockPair = hasOppositeBlockPair;
    }
}

/// <summary>
/// GeoRegion 分类资产（规则、优先级、命名模板、形态参数）。
/// </summary>
public class GeoRegionAsset : Asset
{
    /// <summary>
    /// 资产所属大类层。
    /// </summary>
    public GeoRegionLayer Layer;
    /// <summary>
    /// 同层规则优先级，值越大越先匹配。
    /// </summary>
    public int Priority;
    /// <summary>
    /// 最小 tile 数，常用于过滤碎片区域。
    /// </summary>
    public int MinTiles;
    /// <summary>
    /// 最大 tile 数，常用于形态上限控制。
    /// </summary>
    public int MaxTiles;

    /// <summary>
    /// 分类显示名称。
    /// </summary>
    public string DisplayName;
    /// <summary>
    /// 命名模板规则。
    /// </summary>
    public GeoRegionNamingRule Naming = new();

    /// <summary>
    /// 允许的 biome id 列表（为空表示不限）。
    /// </summary>
    public string[] BiomeIds;
    /// <summary>
    /// 允许的 tile type id 列表（为空表示不限）。
    /// </summary>
    public string[] TileTypeIds;
    /// <summary>
    /// 允许的 tile layer 类型列表（为空表示不限）。
    /// </summary>
    public TileLayerType[] LayerTypes;

    /// <summary>
    /// 是否要求 tile.Type.ocean。
    /// </summary>
    public bool? RequireOceanFlag;
    /// <summary>
    /// 是否要求 tile.Type.can_be_filled_with_ocean。
    /// </summary>
    public bool? RequireFillableWaterFlag;
    /// <summary>
    /// 是否要求 lava 标记。
    /// </summary>
    public bool? RequireLavaFlag;
    /// <summary>
    /// 是否要求 goo 标记。
    /// </summary>
    public bool? RequireGooFlag;
    /// <summary>
    /// 是否要求山体标记。
    /// </summary>
    public bool? RequireMountainFlag;

    /// <summary>
    /// 4 邻接最小水体数量（可用于 Primary 海滩等贴水规则）。
    /// </summary>
    public int MinNeighborWater;
    /// <summary>
    /// 到最近水体的最大距离（用于控制海滩等贴海宽度，-1 表示不限制）。
    /// </summary>
    public int MaxDistanceToWater = -1;
    /// <summary>
    /// 4 邻接最小阻挡地块数量。
    /// </summary>
    public int MinNeighborBlock;
    /// <summary>
    /// 4 邻接最小坑地数量。
    /// </summary>
    public int MinNeighborPit;
    /// <summary>
    /// 是否要求存在左右或上下对向山体。
    /// </summary>
    public bool RequireOppositeBlockPair;

    /// <summary>
    /// 旧版高度规则：最小高度（已弃用，仅保留兼容）。
    /// </summary>
    public int MinHeight = -1;
    /// <summary>
    /// 旧版高度规则：最大高度（已弃用，仅保留兼容）。
    /// </summary>
    public int MaxHeight = -1;
    /// <summary>
    /// 旧版坡度规则：最小坡度（已弃用，仅保留兼容）。
    /// </summary>
    public int MinSlope = -1;
    /// <summary>
    /// 旧版坡度规则：最大坡度（已弃用，仅保留兼容）。
    /// </summary>
    public int MaxSlope = -1;
    /// <summary>
    /// 旧版高差规则：最小 delta（已弃用，仅保留兼容）。
    /// </summary>
    public int MinDelta = -1;
    /// <summary>
    /// 旧版高差规则：最大 delta（已弃用，仅保留兼容）。
    /// </summary>
    public int MaxDelta = -1;
    /// <summary>
    /// 旧版复合规则开关（已弃用，仅保留兼容）。
    /// </summary>
    public bool HeightOrSlope;

    /// <summary>
    /// 半岛最大厚度（tile）。
    /// </summary>
    public int MaxThickness;
    /// <summary>
    /// 半岛最小海岸占比。
    /// </summary>
    public float MinCoastRatio;
    /// <summary>
    /// 半岛最大颈部占比。
    /// </summary>
    public float MaxNeckRatio;

    /// <summary>
    /// 海峡半宽上限。
    /// </summary>
    public int MaxHalfWidth;
    /// <summary>
    /// 海峡最小出口数。
    /// </summary>
    public int MinExits;
    /// <summary>
    /// 海峡最小包围盒长宽比。
    /// </summary>
    public float MinAspectRatio;

    /// <summary>
    /// 群岛中单岛最大 tile 数。
    /// </summary>
    public int IslandMaxTiles;
    /// <summary>
    /// 群岛聚类最大间隔。
    /// </summary>
    public int MaxGap;
    /// <summary>
    /// 群岛最小岛屿数。
    /// </summary>
    public int MinIslands;
    /// <summary>
    /// 群岛最小总 tile 数。
    /// </summary>
    public int MinTotalTiles;
}
