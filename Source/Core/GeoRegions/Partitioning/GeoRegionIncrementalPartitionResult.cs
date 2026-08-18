using System;
using System.Collections.Generic;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>某一个地区层在更新前后的变化数量，用于查看哪些层受到地形更新影响。</summary>
internal readonly struct GeoRegionLayerChangeDiagnostics
{
    /// <summary>创建一个地区层的新旧数量和变化数量统计。</summary>
    internal GeoRegionLayerChangeDiagnostics(
        GeoRegionLayer layer,
        int oldRegionCount,
        int newRegionCount,
        int changedRegionCount,
        int changedTileCount)
    {
        Layer = layer;
        OldRegionCount = oldRegionCount;
        NewRegionCount = newRegionCount;
        ChangedRegionCount = changedRegionCount;
        ChangedTileCount = changedTileCount;
    }

    /// <summary>本条统计对应的地区层。</summary>
    internal GeoRegionLayer Layer { get; }
    /// <summary>更新前该层的地区数量。</summary>
    internal int OldRegionCount { get; }
    /// <summary>更新后该层的地区数量。</summary>
    internal int NewRegionCount { get; }
    /// <summary>新增、消失或内容改变的地区数量。</summary>
    internal int ChangedRegionCount { get; }
    /// <summary>所属地区发生变化的格子数量。</summary>
    internal int ChangedTileCount { get; }
}

/// <summary>一次增量分区的总体统计，说明输入变化、实际影响范围、计算轮数以及是否改做完整分区。</summary>
internal sealed class GeoRegionIncrementalDiagnostics
{
    /// <summary>按地区层编号保存各层的新旧变化统计。</summary>
    private readonly GeoRegionLayerChangeDiagnostics[] layerChanges;

    /// <summary>创建一次增量分区的诊断统计。</summary>
    internal GeoRegionIncrementalDiagnostics(
        int dirtyTileCount,
        int changedBaseTileCount,
        int topologyChangedTileCount,
        int affectedTileCount,
        int totalTileCount,
        int closureRounds,
        bool usedFullFallback,
        GeoRegionLayerChangeDiagnostics[] layerChanges)
    {
        DirtyTileCount = dirtyTileCount;
        ChangedBaseTileCount = changedBaseTileCount;
        TopologyChangedTileCount = topologyChangedTileCount;
        AffectedTileCount = affectedTileCount;
        TotalTileCount = totalTileCount;
        ClosureRounds = closureRounds;
        UsedFullFallback = usedFullFallback;
        this.layerChanges = layerChanges ?? throw new ArgumentNullException(nameof(layerChanges));
    }

    /// <summary>本次收到新观测的格子数量。</summary>
    internal int DirtyTileCount { get; }
    /// <summary>整理后基础分区数据真正改变的格子数量。</summary>
    internal int ChangedBaseTileCount { get; }
    /// <summary>会改变陆水或地区连接关系的格子数量。</summary>
    internal int TopologyChangedTileCount { get; }
    /// <summary>本次重新计算涉及的格子数量。</summary>
    internal int AffectedTileCount { get; }
    /// <summary>整张地图的格子总数。</summary>
    internal int TotalTileCount { get; }
    /// <summary>为了纳入相邻受影响地区而扩展计算范围的轮数。</summary>
    internal int ClosureRounds { get; }
    /// <summary>本次是否因影响范围过大而改做完整分区。</summary>
    internal bool UsedFullFallback { get; }
    /// <summary>受影响格子占整张地图的比例。</summary>
    internal double AffectedRatio => TotalTileCount > 0 ? AffectedTileCount / (double)TotalTileCount : 0d;

    /// <summary>读取指定地区层的变化统计。</summary>
    internal GeoRegionLayerChangeDiagnostics GetLayerChange(GeoRegionLayer layer)
    {
        int index = (int)layer;
        if ((uint)index >= (uint)layerChanges.Length) throw new ArgumentOutOfRangeException(nameof(layer));
        return layerChanges[index];
    }

    /// <summary>生成各地区层新旧地区数、变化地区数和变化格子数的日志摘要。</summary>
    internal string GetLayerChangeSummary()
    {
        var parts = new string[layerChanges.Length];
        for (int i = 0; i < layerChanges.Length; i++)
        {
            GeoRegionLayerChangeDiagnostics change = layerChanges[i];
            parts[i] = $"{change.Layer}:{change.OldRegionCount}->{change.NewRegionCount}/r{change.ChangedRegionCount}/t{change.ChangedTileCount}";
        }
        return string.Join(", ", parts);
    }
}

/// <summary>一次增量分区的完整输出，包括更新后的分区结果、变化统计和实际受影响格子。</summary>
internal sealed class GeoRegionIncrementalPartitionResult
{
    /// <summary>本次重新计算涉及的格子编号。</summary>
    private readonly int[] affectedTileIds;

    /// <summary>创建增量分区输出，并复制受影响格子编号。</summary>
    internal GeoRegionIncrementalPartitionResult(
        GeoRegionPartitionResult result,
        GeoRegionIncrementalDiagnostics diagnostics,
        int[] affectedTileIds)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.affectedTileIds = affectedTileIds != null
            ? (int[])affectedTileIds.Clone()
            : throw new ArgumentNullException(nameof(affectedTileIds));
    }

    /// <summary>应用本次地形变化后的完整分区结果。</summary>
    internal GeoRegionPartitionResult Result { get; }
    /// <summary>本次增量计算的范围和各层变化统计。</summary>
    internal GeoRegionIncrementalDiagnostics Diagnostics { get; }

    /// <summary>复制本次受影响的全部格子编号。</summary>
    internal int[] CopyAffectedTileIds()
    {
        return (int[])affectedTileIds.Clone();
    }
}

/// <summary>
/// 比较更新前后的完整分区结果，统计每个地区层中改变的地区和格子。
/// </summary>
internal static class GeoRegionPartitionComparer
{
    /// <summary>逐层比较两份结果，生成每层地区数量和变化范围统计。</summary>
    internal static GeoRegionLayerChangeDiagnostics[] BuildLayerChanges(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult)
    {
        var result = new GeoRegionLayerChangeDiagnostics[GeoRegionPartitionCodec.LayerCount];
        for (int layerIndex = 0; layerIndex < result.Length; layerIndex++)
        {
            var layer = (GeoRegionLayer)layerIndex;
            List<GeoRegionDescriptor> oldDescriptors = GetLayerDescriptors(oldResult, layer);
            List<GeoRegionDescriptor> newDescriptors = GetLayerDescriptors(newResult, layer);
            int changedRegionCount = CountChangedDescriptors(oldDescriptors, newDescriptors);
            int changedTileCount = CountChangedTiles(oldResult, newResult, layer);
            result[layerIndex] = new GeoRegionLayerChangeDiagnostics(
                layer,
                oldDescriptors.Count,
                newDescriptors.Count,
                changedRegionCount,
                changedTileCount);
        }
        return result;
    }

    /// <summary>判断两片地区的类别、组成、中心和全部格子是否完全相同。</summary>
    internal static bool AreEquivalent(GeoRegionDescriptor left, GeoRegionDescriptor right)
    {
        if (left == null || right == null) return ReferenceEquals(left, right);
        if (left.Layer != right.Layer ||
            left.CategoryCode != right.CategoryCode ||
            left.BaseTerrainLayer != right.BaseTerrainLayer ||
            left.WaterKind != right.WaterKind ||
            left.TouchesEdge != right.TouchesEdge ||
            left.CoreTileCount != right.CoreTileCount ||
            left.IsMixed != right.IsMixed ||
            left.TopologyExempt != right.TopologyExempt ||
            left.CoreSignature != right.CoreSignature ||
            left.RawCompositionCount != right.RawCompositionCount ||
            !string.Equals(left.CoreBiomeId, right.CoreBiomeId, StringComparison.Ordinal) ||
            !string.Equals(left.DominantBiomeId, right.DominantBiomeId, StringComparison.Ordinal) ||
            left.BiomeCompositionCount != right.BiomeCompositionCount ||
            left.CenterX != right.CenterX ||
            left.CenterY != right.CenterY ||
            left.DominantPrimaryCode != right.DominantPrimaryCode ||
            left.DominantLandformCode != right.DominantLandformCode ||
            left.TileCount != right.TileCount)
        {
            return false;
        }

        for (int i = 0; i < left.RawCompositionCount; i++)
        {
            if (left.GetRawSignature(i) != right.GetRawSignature(i) ||
                left.GetRawSignatureTileCount(i) != right.GetRawSignatureTileCount(i))
            {
                return false;
            }
        }
        for (int i = 0; i < left.BiomeCompositionCount; i++)
        {
            if (!string.Equals(left.GetBiomeId(i), right.GetBiomeId(i), StringComparison.Ordinal) ||
                left.GetBiomeTileCount(i) != right.GetBiomeTileCount(i))
            {
                return false;
            }
        }
        for (int i = 0; i < left.TileCount; i++)
        {
            if (left.GetTileId(i) != right.GetTileId(i)) return false;
        }
        return true;
    }

    /// <summary>按地区最小格子编号配对新旧地区，统计新增、消失或内容改变的地区数。</summary>
    private static int CountChangedDescriptors(
        List<GeoRegionDescriptor> oldDescriptors,
        List<GeoRegionDescriptor> newDescriptors)
    {
        int oldIndex = 0;
        int newIndex = 0;
        int changed = 0;
        while (oldIndex < oldDescriptors.Count || newIndex < newDescriptors.Count)
        {
            if (oldIndex >= oldDescriptors.Count)
            {
                changed += newDescriptors.Count - newIndex;
                break;
            }
            if (newIndex >= newDescriptors.Count)
            {
                changed += oldDescriptors.Count - oldIndex;
                break;
            }

            GeoRegionDescriptor oldDescriptor = oldDescriptors[oldIndex];
            GeoRegionDescriptor newDescriptor = newDescriptors[newIndex];
            int oldMin = oldDescriptor.GetTileId(0);
            int newMin = newDescriptor.GetTileId(0);
            if (oldMin == newMin)
            {
                if (!AreEquivalent(oldDescriptor, newDescriptor)) changed++;
                oldIndex++;
                newIndex++;
            }
            else if (oldMin < newMin)
            {
                changed++;
                oldIndex++;
            }
            else
            {
                changed++;
                newIndex++;
            }
        }
        return changed;
    }

    /// <summary>逐格比较指定层的新旧所属地区，统计所属地区内容发生变化的格子数。</summary>
    private static int CountChangedTiles(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        GeoRegionLayer layer)
    {
        int changed = 0;
        int tileCount = checked(oldResult.Width * oldResult.Height);
        var equivalenceCache = new Dictionary<long, bool>();
        for (int tileId = 0; tileId < tileCount; tileId++)
        {
            int oldSlot = oldResult.GetRegionSlot(tileId, layer);
            int newSlot = newResult.GetRegionSlot(tileId, layer);
            if (oldSlot < 0 || newSlot < 0)
            {
                if (oldSlot != newSlot) changed++;
                continue;
            }

            long key = ((long)oldSlot << 32) | (uint)newSlot;
            if (!equivalenceCache.TryGetValue(key, out bool equivalent))
            {
                equivalent = AreEquivalent(oldResult.GetRegion(oldSlot), newResult.GetRegion(newSlot));
                equivalenceCache.Add(key, equivalent);
            }
            if (!equivalent) changed++;
        }
        return changed;
    }

    /// <summary>取出指定层的全部地区，并按稳定顺序排列，供新旧结果配对。</summary>
    private static List<GeoRegionDescriptor> GetLayerDescriptors(
        GeoRegionPartitionResult result,
        GeoRegionLayer layer)
    {
        var descriptors = new List<GeoRegionDescriptor>();
        for (int i = 0; i < result.RegionCount; i++)
        {
            GeoRegionDescriptor descriptor = result.GetRegion(i);
            if (descriptor.Layer == layer) descriptors.Add(descriptor);
        }
        descriptors.Sort(CompareDescriptors);
        return descriptors;
    }

    /// <summary>依次按地区层、最小格子编号、类别和大小确定稳定排序。</summary>
    private static int CompareDescriptors(GeoRegionDescriptor left, GeoRegionDescriptor right)
    {
        int layerComparison = left.Layer.CompareTo(right.Layer);
        if (layerComparison != 0) return layerComparison;
        int leftMin = left.TileCount > 0 ? left.GetTileId(0) : int.MaxValue;
        int rightMin = right.TileCount > 0 ? right.GetTileId(0) : int.MaxValue;
        int tileComparison = leftMin.CompareTo(rightMin);
        if (tileComparison != 0) return tileComparison;
        int categoryComparison = left.CategoryCode.CompareTo(right.CategoryCode);
        if (categoryComparison != 0) return categoryComparison;
        return left.TileCount.CompareTo(right.TileCount);
    }

}
