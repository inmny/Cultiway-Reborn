using System;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 游戏地图中某格原始地面所在的层。分区计算只保存这些简单数值，不直接使用游戏对象。
/// </summary>
internal enum GeoRegionTerrainLayer : byte
{
    /// <summary>没有可识别的原始层。</summary>
    None = 0,
    /// <summary>普通陆地地面。</summary>
    Ground = 1,
    /// <summary>海洋或其他水面。</summary>
    Ocean = 2,
    /// <summary>熔岩地面。</summary>
    Lava = 3,
    /// <summary>山体、墙体等阻挡地面。</summary>
    Block = 4,
    /// <summary>黏液地面。</summary>
    Goo = 5
}

/// <summary>
/// 某格当前认定的地面情况。它综合原始层和材质标记，直接供分区规则判断。
/// </summary>
internal enum GeoRegionTerrainKind : byte
{
    /// <summary>无法归入下列种类的地面。</summary>
    Other = 0,
    /// <summary>可视为普通陆地。</summary>
    Ground = 1,
    /// <summary>可视为水域。</summary>
    Water = 2,
    /// <summary>可视为熔岩区域。</summary>
    Lava = 3,
    /// <summary>可视为山体或阻挡区域。</summary>
    Block = 4,
    /// <summary>可视为黏液区域。</summary>
    Goo = 5
}

/// <summary>
/// 某格或某片区域最主要的地表类别。固定数值用于统计一片区域里哪类格子最多。
/// </summary>
internal enum GeoRegionPrimaryCategoryCode : byte
{
    /// <summary>尚未确定主要地表。</summary>
    None = 0,
    /// <summary>草原。</summary>
    Grassland = 1,
    /// <summary>森林。</summary>
    Forest = 2,
    /// <summary>丛林。</summary>
    Jungle = 3,
    /// <summary>沼泽。</summary>
    Swamp = 4,
    /// <summary>沙漠。</summary>
    Desert = 5,
    /// <summary>苔原或冰雪地带。</summary>
    Tundra = 6,
    /// <summary>高地。</summary>
    Highlands = 7,
    /// <summary>荒地。</summary>
    Wasteland = 8,
    /// <summary>海滩。</summary>
    Beach = 9,
    /// <summary>未列入常规类别的特殊生物群系。</summary>
    Special = 10,
    /// <summary>山地区域。</summary>
    Mountains = 11
}

/// <summary>
/// 陆地外形类别，用来说明一片相连陆地整体更像平原、山地、峡谷还是盆地。
/// </summary>
internal enum GeoRegionLandformCode : byte
{
    /// <summary>尚未确定陆地外形。</summary>
    None = 0,
    /// <summary>平原。</summary>
    Plain = 1,
    /// <summary>山地。</summary>
    Mountain = 2,
    /// <summary>峡谷。</summary>
    Canyon = 3,
    /// <summary>盆地。</summary>
    Basin = 4
}

/// <summary>
/// 最终创建地区时使用的完整类别。它把主要地表、陆地外形和海陆位置放在同一套编号中。
/// </summary>
internal enum GeoRegionCategoryCode : byte
{
    /// <summary>没有有效类别。</summary>
    None = 0,
    /// <summary>主要地表为海洋。</summary>
    PrimarySea,
    /// <summary>主要地表为湖泊。</summary>
    PrimaryLake,
    /// <summary>主要地表为河流。</summary>
    PrimaryRiver,
    /// <summary>主要地表为熔岩。</summary>
    PrimaryLava,
    /// <summary>主要地表为黏液。</summary>
    PrimaryGoo,
    /// <summary>主要地表为山地。</summary>
    PrimaryMountains,
    /// <summary>主要地表为草原。</summary>
    PrimaryGrassland,
    /// <summary>主要地表为森林。</summary>
    PrimaryForest,
    /// <summary>主要地表为丛林。</summary>
    PrimaryJungle,
    /// <summary>主要地表为沼泽。</summary>
    PrimarySwamp,
    /// <summary>主要地表为沙漠。</summary>
    PrimaryDesert,
    /// <summary>主要地表为海滩。</summary>
    PrimaryBeach,
    /// <summary>主要地表为苔原。</summary>
    PrimaryTundra,
    /// <summary>主要地表为高地。</summary>
    PrimaryHighlands,
    /// <summary>主要地表为荒地。</summary>
    PrimaryWasteland,
    /// <summary>主要地表为其他特殊生物群系。</summary>
    PrimarySpecial,
    /// <summary>陆地外形为平原。</summary>
    LandformPlain,
    /// <summary>陆地外形为山地。</summary>
    LandformMountain,
    /// <summary>陆地外形为峡谷。</summary>
    LandformCanyon,
    /// <summary>陆地外形为盆地。</summary>
    LandformBasin,
    /// <summary>陆块大小属于岛屿。</summary>
    LandmassIsland,
    /// <summary>陆块大小属于大陆。</summary>
    LandmassContinent,
    /// <summary>陆块大小属于主大陆。</summary>
    LandmassMainland,
    /// <summary>三面临水的半岛。</summary>
    Peninsula,
    /// <summary>连接两片水域的狭窄水道。</summary>
    Strait,
    /// <summary>彼此接近的一组岛屿。</summary>
    Archipelago
}

/// <summary>
/// 集中保存分区层数和类别编号的换算方式，避免各处各自使用不同数字。
/// </summary>
internal static class GeoRegionPartitionCodec
{
    /// <summary>每个地图格需要记录的地区层数量。</summary>
    internal const int LayerCount = (int)GeoRegionLayer.Archipelago + 1;
    /// <summary>普通陆地主要类别在连接编号中的起始值。</summary>
    internal const int PrimaryGroundSignatureOffset = 10;
    /// <summary>特殊生物群系连接编号的起始值，使不同特殊生物群系不会被合并。</summary>
    internal const int PrimarySpecialBiomeSignatureOffset = 1000;
    /// <summary>主要地表类别的有效编号数量。</summary>
    internal const int PrimaryCodeCount = (int)GeoRegionPrimaryCategoryCode.Mountains + 1;
    /// <summary>陆地外形类别的有效编号数量。</summary>
    internal const int LandformCodeCount = (int)GeoRegionLandformCode.Basin + 1;

    /// <summary>把普通陆地类别换成连接分组使用的编号。</summary>
    internal static int EncodeGroundSignature(GeoRegionPrimaryCategoryCode code)
    {
        return PrimaryGroundSignatureOffset + (int)code;
    }

    /// <summary>从普通陆地连接编号还原主要地表类别；未知编号按特殊类别处理。</summary>
    internal static GeoRegionPrimaryCategoryCode DecodeGroundSignature(int signature)
    {
        int value = signature - PrimaryGroundSignatureOffset;
        return (uint)value < PrimaryCodeCount
            ? (GeoRegionPrimaryCategoryCode)value
            : GeoRegionPrimaryCategoryCode.Special;
    }
}
