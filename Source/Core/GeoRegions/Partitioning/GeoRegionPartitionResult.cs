using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 检查每个地图格是否都被主要地表地区覆盖，以及每个陆地格是否都被陆地外形地区覆盖。
/// </summary>
internal readonly struct GeoRegionCoverageDiagnostics
{
    /// <summary>创建一份地图格覆盖情况统计。</summary>
    internal GeoRegionCoverageDiagnostics(
        int tileCount,
        int primaryCoveredTileCount,
        int landTileCount,
        int landformCoveredTileCount,
        int unexpectedLandformTileCount,
        int unknownTileCount,
        int unknownPrimarySpecialTileCount)
    {
        TileCount = tileCount;
        PrimaryCoveredTileCount = primaryCoveredTileCount;
        LandTileCount = landTileCount;
        LandformCoveredTileCount = landformCoveredTileCount;
        UnexpectedLandformTileCount = unexpectedLandformTileCount;
        UnknownTileCount = unknownTileCount;
        UnknownPrimarySpecialTileCount = unknownPrimarySpecialTileCount;
    }

    /// <summary>地图格子总数。</summary>
    internal int TileCount { get; }
    /// <summary>已归入某个主要地表地区的格子数。</summary>
    internal int PrimaryCoveredTileCount { get; }
    /// <summary>需要划分陆地外形的陆地格子数。</summary>
    internal int LandTileCount { get; }
    /// <summary>已归入某个陆地外形地区的陆地格子数。</summary>
    internal int LandformCoveredTileCount { get; }
    /// <summary>不是陆地却被归入陆地外形地区的格子数。</summary>
    internal int UnexpectedLandformTileCount { get; }
    /// <summary>地面种类无法识别的格子数。</summary>
    internal int UnknownTileCount { get; }
    /// <summary>无法识别且最终归入特殊主要地表地区的格子数。</summary>
    internal int UnknownPrimarySpecialTileCount { get; }

    /// <summary>主要地表是否覆盖全部格子，且陆地外形恰好覆盖全部陆地格。</summary>
    internal bool IsComplete =>
        PrimaryCoveredTileCount == TileCount &&
        LandformCoveredTileCount == LandTileCount &&
        UnexpectedLandformTileCount == 0;

    /// <summary>生成便于写入日志的覆盖数量摘要。</summary>
    internal string GetSummary()
    {
        return
            "primary=" + PrimaryCoveredTileCount + "/" + TileCount +
            ", landform=" + LandformCoveredTileCount + "/" + LandTileCount +
            ", unexpectedLandform=" + UnexpectedLandformTileCount +
            ", unknownSpecial=" + UnknownPrimarySpecialTileCount + "/" + UnknownTileCount;
    }
}

/// <summary>
/// 一次完整分区计算的只读结果，保存所有地区和每格所属地区，并在创建时检查两边记录是否一致。
/// </summary>
internal sealed class GeoRegionPartitionResult
{
    /// <summary>本次计算得到的全部地区说明，位置编号就是地区槽位。</summary>
    private readonly ReadOnlyCollection<GeoRegionDescriptor> regions;
    /// <summary>按“格子编号和地区层”保存该格所属地区的槽位；未归属时为负数。</summary>
    private readonly int[] regionSlotByTileLayer;
    /// <summary>按“格子编号和地区层”保存该格在所属地区格子列表中的位置。</summary>
    private readonly int[] positionInRegionByTileLayer;

    /// <summary>创建完整分区结果，复制地区和索引数据，并检查所有对应关系。</summary>
    internal GeoRegionPartitionResult(
        int worldSeedId,
        int width,
        int height,
        int revision,
        ulong ruleFingerprint,
        IList<GeoRegionDescriptor> regions,
        int[] regionSlotByTileLayer,
        int[] positionInRegionByTileLayer,
        int membershipCount,
        long estimatedPersistentBytes,
        GeoRegionPartitionBaseArrays baseArrays,
        GeoRegionPartitionTiming timing)
    {
        if (regions == null) throw new ArgumentNullException(nameof(regions));
        if (regionSlotByTileLayer == null) throw new ArgumentNullException(nameof(regionSlotByTileLayer));
        if (positionInRegionByTileLayer == null) throw new ArgumentNullException(nameof(positionInRegionByTileLayer));

        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Revision = revision;
        RuleFingerprint = ruleFingerprint;
        MembershipCount = membershipCount;
        EstimatedPersistentBytes = estimatedPersistentBytes;
        BaseArrays = baseArrays ?? throw new ArgumentNullException(nameof(baseArrays));
        if (baseArrays.TileCount != checked(width * height))
        {
            throw new InvalidOperationException("GeoRegion result 与基础数组尺寸不一致");
        }
        Timing = timing;
        this.regions = new ReadOnlyCollection<GeoRegionDescriptor>(new List<GeoRegionDescriptor>(regions));
        this.regionSlotByTileLayer = (int[])regionSlotByTileLayer.Clone();
        this.positionInRegionByTileLayer = (int[])positionInRegionByTileLayer.Clone();
        ValidateStructure();
    }

    /// <summary>结果所属世界的种子标识。</summary>
    internal int WorldSeedId { get; }
    /// <summary>地图横向格子数。</summary>
    internal int Width { get; }
    /// <summary>地图纵向格子数。</summary>
    internal int Height { get; }
    /// <summary>结果使用的数据版本。</summary>
    internal int Revision { get; }
    /// <summary>结果使用的规则内容编号。</summary>
    internal ulong RuleFingerprint { get; }
    /// <summary>本次计算得到的地区总数。</summary>
    internal int RegionCount => regions.Count;
    /// <summary>所有层中的“格子属于地区”关系总数。</summary>
    internal int MembershipCount { get; }
    /// <summary>长期保存这份结果预计占用的字节数。</summary>
    internal long EstimatedPersistentBytes { get; }
    /// <summary>分区过程中得到、可供后续增量计算复用的每格基础数据。</summary>
    internal GeoRegionPartitionBaseArrays BaseArrays { get; }
    /// <summary>本次完整分区各步骤的耗时。</summary>
    internal GeoRegionPartitionTiming Timing { get; }

    /// <summary>按地区槽位读取一片地区的说明。</summary>
    internal GeoRegionDescriptor GetRegion(int index)
    {
        return regions[index];
    }

    /// <summary>查询指定格子在指定地区层中属于哪个地区；未归属时返回负数。</summary>
    internal int GetRegionSlot(int tileId, GeoRegionLayer layer)
    {
        if ((uint)tileId >= (uint)checked(Width * Height)) throw new ArgumentOutOfRangeException(nameof(tileId));
        int layerIndex = (int)layer;
        if ((uint)layerIndex >= GeoRegionPartitionCodec.LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
        return regionSlotByTileLayer[tileId * GeoRegionPartitionCodec.LayerCount + layerIndex];
    }

    /// <summary>复制每格每层对应的地区槽位索引。</summary>
    internal int[] CopyRegionSlotByTileLayer()
    {
        return (int[])regionSlotByTileLayer.Clone();
    }

    /// <summary>复制每格每层在所属地区中的位置索引。</summary>
    internal int[] CopyPositionInRegionByTileLayer()
    {
        return (int[])positionInRegionByTileLayer.Clone();
    }

    /// <summary>生成包含地区关系数量和各步骤毫秒数的日志摘要。</summary>
    internal string GetTimingSummary()
    {
        return
            $"memberships={MembershipCount}, total={Timing.TotalMilliseconds:0.0}ms " +
            $"[base={Timing.BaseArraysMilliseconds:0.0}, primary={Timing.PrimaryMilliseconds:0.0}, " +
            $"landform={Timing.LandformMilliseconds:0.0}, landmass={Timing.LandmassMilliseconds:0.0}, " +
            $"peninsula={Timing.PeninsulaMilliseconds:0.0}, strait={Timing.StraitMilliseconds:0.0}, " +
            $"archipelago={Timing.ArchipelagoMilliseconds:0.0}, index={Timing.IndexMilliseconds:0.0}]ms";
    }

    /// <summary>统计主要地表和陆地外形对地图格的覆盖情况。</summary>
    internal GeoRegionCoverageDiagnostics GetCoverageDiagnostics(GeoRegionTerrainSnapshot terrain)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (terrain.WorldSeedId != WorldSeedId ||
            terrain.Width != Width ||
            terrain.Height != Height ||
            terrain.Revision != Revision)
        {
            throw new InvalidOperationException("GeoRegion 覆盖诊断与地形快照身份不一致");
        }

        int tileCount = checked(Width * Height);
        int primaryCoveredTileCount = 0;
        int landTileCount = 0;
        int landformCoveredTileCount = 0;
        int unexpectedLandformTileCount = 0;
        int unknownTileCount = 0;
        int unknownPrimarySpecialTileCount = 0;

        for (int tileId = 0; tileId < tileCount; tileId++)
        {
            int baseIndex = tileId * GeoRegionPartitionCodec.LayerCount;
            int primarySlot = regionSlotByTileLayer[baseIndex + (int)GeoRegionLayer.Primary];
            if (primarySlot >= 0)
            {
                primaryCoveredTileCount++;
            }

            GeoRegionTerrainCell cell = terrain.GetCell(tileId);
            if (cell.TerrainKind == GeoRegionTerrainKind.Other)
            {
                unknownTileCount++;
                if (primarySlot >= 0 && regions[primarySlot].CategoryCode == GeoRegionCategoryCode.PrimarySpecial)
                {
                    unknownPrimarySpecialTileCount++;
                }
            }

            bool isLand = cell.TerrainKind is GeoRegionTerrainKind.Ground or GeoRegionTerrainKind.Block;
            int landformSlot = regionSlotByTileLayer[baseIndex + (int)GeoRegionLayer.Landform];
            if (isLand)
            {
                landTileCount++;
                if (landformSlot >= 0) landformCoveredTileCount++;
            }
            else if (landformSlot >= 0)
            {
                unexpectedLandformTileCount++;
            }
        }

        return new GeoRegionCoverageDiagnostics(
            tileCount,
            primaryCoveredTileCount,
            landTileCount,
            landformCoveredTileCount,
            unexpectedLandformTileCount,
            unknownTileCount,
            unknownPrimarySpecialTileCount);
    }

    /// <summary>检查地区是否完整覆盖应覆盖的格子；不完整时抛出错误。</summary>
    internal GeoRegionCoverageDiagnostics ValidateCoverage(GeoRegionTerrainSnapshot terrain)
    {
        GeoRegionCoverageDiagnostics diagnostics = GetCoverageDiagnostics(terrain);
        if (!diagnostics.IsComplete)
        {
            throw new InvalidOperationException("GeoRegion 完整覆盖校验失败: " + diagnostics.GetSummary());
        }

        return diagnostics;
    }

    /// <summary>逐项核对地区格子列表、正向索引、反向位置和关系总数是否一致。</summary>
    private void ValidateStructure()
    {
        if (Width <= 0 || Height <= 0 || Revision <= 0)
        {
            throw new InvalidOperationException("GeoRegion 分区结果身份无效");
        }

        int tileCount = checked(Width * Height);
        int expectedLength = checked(tileCount * GeoRegionPartitionCodec.LayerCount);
        if (regionSlotByTileLayer.Length != expectedLength ||
            positionInRegionByTileLayer.Length != expectedLength)
        {
            throw new InvalidOperationException(
                $"GeoRegion 分区索引尺寸不匹配: expected={expectedLength}, " +
                $"slots={regionSlotByTileLayer.Length}, positions={positionInRegionByTileLayer.Length}");
        }

        int countedMemberships = 0;
        for (int slot = 0; slot < regions.Count; slot++)
        {
            GeoRegionDescriptor descriptor = regions[slot] ??
                                             throw new InvalidOperationException($"GeoRegion descriptor 为空: slot={slot}");
            int layer = (int)descriptor.Layer;
            for (int position = 0; position < descriptor.TileCount; position++)
            {
                int tileId = descriptor.GetTileId(position);
                if ((uint)tileId >= (uint)tileCount)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion descriptor 包含越界 tile: slot={slot}, tile={tileId}, count={tileCount}");
                }

                int flatIndex = tileId * GeoRegionPartitionCodec.LayerCount + layer;
                if (regionSlotByTileLayer[flatIndex] != slot ||
                    positionInRegionByTileLayer[flatIndex] != position)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion descriptor 与索引不一致: slot={slot}, tile={tileId}, layer={descriptor.Layer}, position={position}");
                }

                countedMemberships++;
            }
        }

        if (countedMemberships != MembershipCount)
        {
            throw new InvalidOperationException(
                $"GeoRegion membership 计数不一致: descriptors={countedMemberships}, result={MembershipCount}");
        }

        for (int flatIndex = 0; flatIndex < expectedLength; flatIndex++)
        {
            int slot = regionSlotByTileLayer[flatIndex];
            int position = positionInRegionByTileLayer[flatIndex];
            if (slot < 0)
            {
                if (position >= 0)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion 空索引包含位置: index={flatIndex}, position={position}");
                }

                continue;
            }

            if ((uint)slot >= (uint)regions.Count)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 索引 slot 越界: index={flatIndex}, slot={slot}, regions={regions.Count}");
            }

            int tileId = flatIndex / GeoRegionPartitionCodec.LayerCount;
            int layer = flatIndex % GeoRegionPartitionCodec.LayerCount;
            GeoRegionDescriptor descriptor = regions[slot];
            if ((int)descriptor.Layer != layer ||
                (uint)position >= (uint)descriptor.TileCount ||
                descriptor.GetTileId(position) != tileId)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 正向索引不一致: tile={tileId}, layer={layer}, slot={slot}, position={position}");
            }
        }
    }
}
