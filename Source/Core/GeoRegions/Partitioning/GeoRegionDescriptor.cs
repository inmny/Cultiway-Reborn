using System;
using System.Collections.Generic;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 一片地区的完整只读说明，包括所含格子、类别、中心位置和地表组成，不引用游戏中的地区或实体对象。
/// </summary>
internal sealed class GeoRegionDescriptor
{
    /// <summary>按固定顺序保存这片地区包含的全部格子编号；核心格子排在前面。</summary>
    private readonly int[] tileIds;
    /// <summary>地区中出现过的原始连接编号，按数值从小到大排列。</summary>
    private readonly int[] rawSignatures;
    /// <summary>每个原始连接编号在地区中占多少格，与 rawSignatures 一一对应。</summary>
    private readonly int[] rawSignatureTileCounts;
    /// <summary>地区中出现过的生物群系标识，按文字顺序排列。</summary>
    private readonly string[] biomeIds;
    /// <summary>每个生物群系在地区中占多少格，与 biomeIds 一一对应。</summary>
    private readonly int[] biomeTileCounts;
    /// <summary>地区内使用核心连接编号的格子总数，包括核心外后来并入的格子。</summary>
    private readonly int coreSignatureTileCount;

    /// <summary>创建地区说明，复制各项组成数据并检查数量和排序是否一致。</summary>
    internal GeoRegionDescriptor(
        IList<int> tileIds,
        GeoRegionLayer layer,
        GeoRegionCategoryCode categoryCode,
        GeoRegionTerrainLayer baseTerrainLayer,
        PrimaryWaterKind waterKind,
        bool touchesEdge,
        int coreTileCount,
        bool isMixed,
        bool topologyExempt,
        int coreSignature,
        IList<int> rawSignatures,
        IList<int> rawSignatureTileCounts,
        string coreBiomeId,
        string dominantBiomeId,
        IList<string> biomeIds,
        IList<int> biomeTileCounts,
        int centerX,
        int centerY,
        GeoRegionPrimaryCategoryCode dominantPrimaryCode,
        GeoRegionLandformCode dominantLandformCode)
    {
        if (tileIds == null) throw new ArgumentNullException(nameof(tileIds));
        if ((uint)layer >= GeoRegionPartitionCodec.LayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(layer));
        }

        if (categoryCode == GeoRegionCategoryCode.None)
        {
            throw new InvalidOperationException($"GeoRegion descriptor 缺少分类: layer={layer}");
        }

        this.tileIds = new int[tileIds.Count];
        for (int i = 0; i < tileIds.Count; i++)
        {
            this.tileIds[i] = tileIds[i];
        }

        Layer = layer;
        CategoryCode = categoryCode;
        BaseTerrainLayer = baseTerrainLayer;
        WaterKind = waterKind;
        TouchesEdge = touchesEdge;
        CoreTileCount = coreTileCount;
        IsMixed = isMixed;
        TopologyExempt = topologyExempt;
        if (coreTileCount <= 0 || coreTileCount > this.tileIds.Length)
        {
            throw new InvalidOperationException(
                $"GeoRegion descriptor 核心尺寸无效: core={coreTileCount}, tiles={this.tileIds.Length}");
        }
        CoreSignature = coreSignature;
        if ((rawSignatures == null) != (rawSignatureTileCounts == null))
        {
            throw new InvalidOperationException("GeoRegion raw composition 数组必须同时提供");
        }
        int compositionCount = rawSignatures?.Count ?? 0;
        if ((rawSignatureTileCounts?.Count ?? 0) != compositionCount)
        {
            throw new InvalidOperationException("GeoRegion raw composition 数组尺寸不一致");
        }
        this.rawSignatures = new int[compositionCount];
        this.rawSignatureTileCounts = new int[compositionCount];
        int compositionTotal = 0;
        int coreSignatureTiles = 0;
        for (int i = 0; i < compositionCount; i++)
        {
            int signature = rawSignatures[i];
            int signatureTiles = rawSignatureTileCounts[i];
            if (signatureTiles <= 0 || i > 0 && signature <= this.rawSignatures[i - 1])
            {
                throw new InvalidOperationException("GeoRegion raw composition 必须按 signature 严格递增且计数为正");
            }
            this.rawSignatures[i] = signature;
            this.rawSignatureTileCounts[i] = signatureTiles;
            compositionTotal = checked(compositionTotal + signatureTiles);
            if (signature == coreSignature) coreSignatureTiles = signatureTiles;
        }
        coreSignatureTileCount = coreSignatureTiles;
        bool regularizedLayer = layer is GeoRegionLayer.Primary or GeoRegionLayer.Landform;
        if (regularizedLayer &&
            (compositionCount == 0 || compositionTotal != this.tileIds.Length ||
             coreSignatureTiles < coreTileCount || isMixed != (compositionCount > 1)))
        {
            throw new InvalidOperationException(
                $"GeoRegion raw composition 无效: layer={layer}, entries={compositionCount}, " +
                $"total={compositionTotal}, tiles={this.tileIds.Length}, core={coreTileCount}, mixed={isMixed}");
        }
        if (!regularizedLayer && compositionCount != 0)
        {
            throw new InvalidOperationException($"GeoRegion 非正则化层不应携带 raw composition: layer={layer}");
        }

        if ((biomeIds == null) != (biomeTileCounts == null))
        {
            throw new InvalidOperationException("GeoRegion biome composition 数组必须同时提供");
        }
        int biomeCount = biomeIds?.Count ?? 0;
        if ((biomeTileCounts?.Count ?? 0) != biomeCount)
        {
            throw new InvalidOperationException("GeoRegion biome composition 数组尺寸不一致");
        }
        this.biomeIds = new string[biomeCount];
        this.biomeTileCounts = new int[biomeCount];
        int biomeTotal = 0;
        for (int i = 0; i < biomeCount; i++)
        {
            string biomeId = biomeIds[i];
            int biomeTiles = biomeTileCounts[i];
            if (string.IsNullOrWhiteSpace(biomeId) || biomeTiles <= 0 ||
                i > 0 && string.CompareOrdinal(biomeId, this.biomeIds[i - 1]) <= 0)
            {
                throw new InvalidOperationException(
                    "GeoRegion biome composition 必须按 biome id 严格递增且计数为正");
            }
            this.biomeIds[i] = biomeId;
            this.biomeTileCounts[i] = biomeTiles;
            biomeTotal = checked(biomeTotal + biomeTiles);
        }
        if (biomeTotal > this.tileIds.Length)
        {
            throw new InvalidOperationException(
                $"GeoRegion biome composition 超出区域面积: biome={biomeTotal}, tiles={this.tileIds.Length}");
        }
        CoreBiomeId = coreBiomeId ?? string.Empty;
        DominantBiomeId = dominantBiomeId ?? string.Empty;
        ValidateBiomeIdentity(CoreBiomeId, this.biomeIds, nameof(coreBiomeId));
        ValidateBiomeIdentity(DominantBiomeId, this.biomeIds, nameof(dominantBiomeId));
        CenterX = centerX;
        CenterY = centerY;
        DominantPrimaryCode = dominantPrimaryCode;
        DominantLandformCode = dominantLandformCode;
    }

    /// <summary>这片地区属于哪一层，例如主要地表、陆地外形或群岛。</summary>
    internal GeoRegionLayer Layer { get; }
    /// <summary>这片地区最终认定的具体类别。</summary>
    internal GeoRegionCategoryCode CategoryCode { get; }
    /// <summary>组成地区的基础地面层。</summary>
    internal GeoRegionTerrainLayer BaseTerrainLayer { get; }
    /// <summary>水域地区的种类；非水域使用对应的空值。</summary>
    internal PrimaryWaterKind WaterKind { get; }
    /// <summary>地区是否碰到地图边缘。</summary>
    internal bool TouchesEdge { get; }
    /// <summary>最初连接成这片地区的核心格子数。</summary>
    internal int CoreTileCount { get; }
    /// <summary>地区是否混合了多个原始连接编号。</summary>
    internal bool IsMixed { get; }
    /// <summary>是否因周围没有足够大的同类地区，只能保留为低于通常面积要求的小地区。</summary>
    internal bool TopologyExempt { get; }
    /// <summary>地区核心格子使用的连接编号。</summary>
    internal int CoreSignature { get; }
    /// <summary>整个地区中与核心连接编号相同的格子数。</summary>
    internal int CoreSignatureTileCount => coreSignatureTileCount;
    /// <summary>核心连接编号格子占地区总格子的比例；没有组成记录时视为全一致。</summary>
    internal float Purity => rawSignatures.Length > 0
        ? (float)coreSignatureTileCount / tileIds.Length
        : 1f;
    /// <summary>地区包含多少种原始连接编号。</summary>
    internal int RawCompositionCount => rawSignatures.Length;
    /// <summary>核心格子中数量最多的生物群系标识。</summary>
    internal string CoreBiomeId { get; }
    /// <summary>整片地区中数量最多的生物群系标识。</summary>
    internal string DominantBiomeId { get; }
    /// <summary>地区包含多少种有标识的生物群系。</summary>
    internal int BiomeCompositionCount => biomeIds.Length;
    /// <summary>地区代表中心格的横坐标。</summary>
    internal int CenterX { get; }
    /// <summary>地区代表中心格的纵坐标。</summary>
    internal int CenterY { get; }
    /// <summary>地区包含的全部格子数。</summary>
    internal int TileCount => tileIds.Length;
    /// <summary>地区内占主导的主要地表类别。</summary>
    internal GeoRegionPrimaryCategoryCode DominantPrimaryCode { get; }
    /// <summary>地区内占主导的陆地外形类别。</summary>
    internal GeoRegionLandformCode DominantLandformCode { get; }

    /// <summary>确认核心或主要生物群系确实出现在这片地区的群系组成中。</summary>
    private static void ValidateBiomeIdentity(string biomeId, string[] composition, string parameterName)
    {
        if (string.IsNullOrEmpty(biomeId)) return;
        if (Array.BinarySearch(composition, biomeId, StringComparer.Ordinal) < 0)
        {
            throw new InvalidOperationException(
                $"GeoRegion {parameterName} 不在 biome composition 中: biome={biomeId}");
        }
    }

    /// <summary>按排序位置读取一种原始连接编号。</summary>
    internal int GetRawSignature(int position)
    {
        if ((uint)position >= (uint)rawSignatures.Length) throw new ArgumentOutOfRangeException(nameof(position));
        return rawSignatures[position];
    }

    /// <summary>读取对应原始连接编号在地区中占用的格子数。</summary>
    internal int GetRawSignatureTileCount(int position)
    {
        if ((uint)position >= (uint)rawSignatureTileCounts.Length) throw new ArgumentOutOfRangeException(nameof(position));
        return rawSignatureTileCounts[position];
    }

    /// <summary>复制全部原始连接编号，避免外部修改内部数据。</summary>
    internal int[] CopyRawSignatures()
    {
        return (int[])rawSignatures.Clone();
    }

    /// <summary>复制各原始连接编号的格子数。</summary>
    internal int[] CopyRawSignatureTileCounts()
    {
        return (int[])rawSignatureTileCounts.Clone();
    }

    /// <summary>按排序位置读取一种生物群系标识。</summary>
    internal string GetBiomeId(int position)
    {
        if ((uint)position >= (uint)biomeIds.Length) throw new ArgumentOutOfRangeException(nameof(position));
        return biomeIds[position];
    }

    /// <summary>读取对应生物群系在地区中占用的格子数。</summary>
    internal int GetBiomeTileCount(int position)
    {
        if ((uint)position >= (uint)biomeTileCounts.Length) throw new ArgumentOutOfRangeException(nameof(position));
        return biomeTileCounts[position];
    }

    /// <summary>按地区内部位置读取格子编号。</summary>
    internal int GetTileId(int position)
    {
        if ((uint)position >= (uint)tileIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return tileIds[position];
    }

    /// <summary>复制地区包含的全部格子编号。</summary>
    internal List<int> CopyTileIds()
    {
        return new List<int>(tileIds);
    }
}

/// <summary>
/// 一次分区计算中各步骤花费的毫秒数，只用于日志和性能排查。
/// </summary>
internal readonly struct GeoRegionPartitionTiming
{
    /// <summary>创建一份各分区步骤的耗时记录。</summary>
    internal GeoRegionPartitionTiming(
        double baseArraysMilliseconds,
        double primaryMilliseconds,
        double landformMilliseconds,
        double landmassMilliseconds,
        double peninsulaMilliseconds,
        double straitMilliseconds,
        double archipelagoMilliseconds,
        double indexMilliseconds,
        double totalMilliseconds)
    {
        BaseArraysMilliseconds = baseArraysMilliseconds;
        PrimaryMilliseconds = primaryMilliseconds;
        LandformMilliseconds = landformMilliseconds;
        LandmassMilliseconds = landmassMilliseconds;
        PeninsulaMilliseconds = peninsulaMilliseconds;
        StraitMilliseconds = straitMilliseconds;
        ArchipelagoMilliseconds = archipelagoMilliseconds;
        IndexMilliseconds = indexMilliseconds;
        TotalMilliseconds = totalMilliseconds;
    }

    /// <summary>准备每格基础数据所用毫秒数。</summary>
    internal double BaseArraysMilliseconds { get; }
    /// <summary>划分主要地表地区所用毫秒数。</summary>
    internal double PrimaryMilliseconds { get; }
    /// <summary>划分陆地外形地区所用毫秒数。</summary>
    internal double LandformMilliseconds { get; }
    /// <summary>判断岛屿和大陆所用毫秒数。</summary>
    internal double LandmassMilliseconds { get; }
    /// <summary>寻找半岛所用毫秒数。</summary>
    internal double PeninsulaMilliseconds { get; }
    /// <summary>寻找海峡所用毫秒数。</summary>
    internal double StraitMilliseconds { get; }
    /// <summary>组合群岛所用毫秒数。</summary>
    internal double ArchipelagoMilliseconds { get; }
    /// <summary>建立格子与地区索引所用毫秒数。</summary>
    internal double IndexMilliseconds { get; }
    /// <summary>整次分区计算总毫秒数。</summary>
    internal double TotalMilliseconds { get; }
}
