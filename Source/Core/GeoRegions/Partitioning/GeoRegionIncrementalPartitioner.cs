using System;
using System.Collections.Generic;
using System.Threading;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 根据新旧地形差异只重算受影响区域，并保留范围外旧结果的六层分区器。
/// 输入和产出均为纯数据，不读取游戏运行时对象。
/// </summary>
internal static class GeoRegionIncrementalPartitioner
{
    // 重算范围超过全图此百分比时，完整重算更简单且开销更可控。
    internal const int FullFallbackPercent = 25;

    /// <summary>
    /// 输入新旧地形、旧分区结果和脏格列表，计算实际受影响范围并重建相关区域。
    /// 返回新分区结果、各层变化统计和受影响格子列表；规则变化或范围过大时改为完整重算。
    /// </summary>
    internal static GeoRegionIncrementalPartitionResult BuildIncremental(
        GeoRegionTerrainSnapshot oldTerrain,
        GeoRegionTerrainSnapshot newTerrain,
        GeoRegionPartitionResult oldResult,
        IList<int> dirtyTileIds,
        GeoRegionRuleSnapshot rules,
        CancellationToken cancellationToken)
    {
        ValidateInputs(oldTerrain, newTerrain, oldResult, dirtyTileIds, rules);
        cancellationToken.ThrowIfCancellationRequested();

        int tileCount = newTerrain.CellCount;
        int dirtyTileCount = CountDistinctDirtyTiles(dirtyTileIds, tileCount);
        if (oldResult.RuleFingerprint != rules.RuleFingerprint)
        {
            return RebuildForRuleChange(
                newTerrain,
                oldResult,
                rules,
                dirtyTileCount,
                cancellationToken);
        }
        GeoRegionPartitionBaseArrays oldArrays = oldResult.BaseArrays;
        GeoRegionPartitionBaseArrays newArrays = GeoRegionPartitioner.CalculateBaseArraysIncremental(
            newTerrain,
            rules,
            oldArrays,
            dirtyTileIds,
            out int[] changedBaseTileIds,
            out int[] topologyChangedTileIds);
        var closure = new ClosureBuilder(oldTerrain, newTerrain, oldResult, oldArrays, newArrays, rules);

        int changedBaseTileCount = changedBaseTileIds.Length;
        int topologyChangedTileCount = topologyChangedTileIds.Length;
        for (int i = 0; i < changedBaseTileIds.Length; i++) closure.Add(changedBaseTileIds[i]);

        bool physicalTopologyMode = HasPhysicalTopologyChange(
            oldArrays,
            newArrays,
            topologyChangedTileIds);
        if (physicalTopologyMode)
        {
            int dependencyRadius = Math.Max(
                1,
                Math.Max(
                    Math.Max(0, rules.Peninsula.MaxThickness),
                    Math.Max(0, rules.Strait.MaxHalfWidth)));
            closure.AddDependencyNeighborhood(topologyChangedTileIds, dependencyRadius, cancellationToken);
        }
        GeoRegionGeneratedLayerMask generatedLayers = physicalTopologyMode
            ? GeoRegionGeneratedLayerMask.All
            : GeoRegionGeneratedLayerMask.Classification;
        bool fullFallback = closure.ExceedsFullFallbackThreshold;
        while (!fullFallback && closure.ExpandRound(generatedLayers, physicalTopologyMode, cancellationToken))
        {
            fullFallback = closure.ExceedsFullFallbackThreshold;
        }

        var input = new GeoRegionPartitionInput(newTerrain, rules);
        int[] closureTileIds = fullFallback ? Array.Empty<int>() : closure.CopyIncludedTileIds();
        GeoRegionPartitionResult result;
        if (fullFallback)
        {
            var allTiles = new bool[tileCount];
            for (int tileId = 0; tileId < tileCount; tileId++) allTiles[tileId] = true;
            result = GeoRegionPartitioner.BuildCore(
                input,
                allTiles,
                GeoRegionGeneratedLayerMask.All,
                Array.Empty<GeoRegionDescriptor>(),
                true,
                newArrays,
                cancellationToken);
        }
        else
        {
            List<GeoRegionDescriptor> retained = BuildRetainedDescriptors(
                oldResult,
                closure.IncludedMask,
                closureTileIds,
                changedBaseTileIds,
                generatedLayers,
                newTerrain,
                newArrays,
                rules);
            result = GeoRegionPartitioner.BuildCore(
                input,
                closure.IncludedMask,
                generatedLayers,
                retained,
                true,
                newArrays,
                cancellationToken);
        }

        GeoRegionLayerChangeDiagnostics[] layerChanges =
            GeoRegionPartitionComparer.BuildLayerChanges(oldResult, result);
        int[] affectedTileIds = fullFallback
            ? CreateAllTileIds(tileCount)
            : closureTileIds;
        var diagnostics = new GeoRegionIncrementalDiagnostics(
            dirtyTileCount,
            changedBaseTileCount,
            topologyChangedTileCount,
            closure.Count,
            tileCount,
            closure.Rounds,
            fullFallback,
            layerChanges);
        return new GeoRegionIncrementalPartitionResult(result, diagnostics, affectedTileIds);
    }

    /// <summary>
    /// 规则指纹变化时重建全图，因为旧区域和基础分类都不能可靠复用。
    /// </summary>
    private static GeoRegionIncrementalPartitionResult RebuildForRuleChange(
        GeoRegionTerrainSnapshot newTerrain,
        GeoRegionPartitionResult oldResult,
        GeoRegionRuleSnapshot rules,
        int dirtyTileCount,
        CancellationToken cancellationToken)
    {
        GeoRegionPartitionResult result = GeoRegionPartitioner.BuildFull(
            new GeoRegionPartitionInput(newTerrain, rules),
            cancellationToken);
        GeoRegionLayerChangeDiagnostics[] layerChanges =
            GeoRegionPartitionComparer.BuildLayerChanges(oldResult, result);
        int tileCount = newTerrain.CellCount;
        var diagnostics = new GeoRegionIncrementalDiagnostics(
            dirtyTileCount,
            tileCount,
            0,
            tileCount,
            tileCount,
            0,
            true,
            layerChanges);
        return new GeoRegionIncrementalPartitionResult(
            result,
            diagnostics,
            CreateAllTileIds(tileCount));
    }

    /// <summary>
    /// 检查新旧快照、旧结果和规则是否属于同一世界及相邻版本。
    /// </summary>
    private static void ValidateInputs(
        GeoRegionTerrainSnapshot oldTerrain,
        GeoRegionTerrainSnapshot newTerrain,
        GeoRegionPartitionResult oldResult,
        IList<int> dirtyTileIds,
        GeoRegionRuleSnapshot rules)
    {
        if (oldTerrain == null) throw new ArgumentNullException(nameof(oldTerrain));
        if (newTerrain == null) throw new ArgumentNullException(nameof(newTerrain));
        if (oldResult == null) throw new ArgumentNullException(nameof(oldResult));
        if (dirtyTileIds == null) throw new ArgumentNullException(nameof(dirtyTileIds));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (oldTerrain.WorldSeedId != newTerrain.WorldSeedId ||
            oldTerrain.Width != newTerrain.Width || oldTerrain.Height != newTerrain.Height ||
            oldResult.WorldSeedId != oldTerrain.WorldSeedId ||
            oldResult.Width != oldTerrain.Width || oldResult.Height != oldTerrain.Height ||
            oldResult.Revision != oldTerrain.Revision ||
            rules.WorldSeedId != newTerrain.WorldSeedId ||
            rules.Width != newTerrain.Width || rules.Height != newTerrain.Height ||
            rules.Revision != newTerrain.Revision)
        {
            throw new InvalidOperationException("GeoRegion incremental 输入身份不一致");
        }
    }

    private static bool HasPhysicalTopologyChange(
        GeoRegionPartitionBaseArrays oldArrays,
        GeoRegionPartitionBaseArrays newArrays,
        int[] topologyChangedTileIds)
    {
        for (int i = 0; i < topologyChangedTileIds.Length; i++)
        {
            int tileId = topologyChangedTileIds[i];
            if (oldArrays.IsLand[tileId] != newArrays.IsLand[tileId] ||
                oldArrays.IsWater[tileId] != newArrays.IsWater[tileId])
            {
                return true;
            }
        }
        return false;
    }

    private static int[] CreateAllTileIds(int count)
    {
        int[] allTileIds = new int[count];
        for (int tileId = 0; tileId < allTileIds.Length; tileId++) allTileIds[tileId] = tileId;
        return allTileIds;
    }

    private static int CountDistinctDirtyTiles(IList<int> dirtyTileIds, int tileCount)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < dirtyTileIds.Count; i++)
        {
            int tileId = dirtyTileIds[i];
            if ((uint)tileId >= (uint)tileCount) throw new ArgumentOutOfRangeException(nameof(dirtyTileIds));
            seen.Add(tileId);
        }
        return seen.Count;
    }

    /// <summary>
    /// 移除与重算范围相交的旧区域，保留其余描述；
    /// 未重建层若基础分类发生变化，则只刷新该区域的主要分类和生态统计。
    /// </summary>
    private static List<GeoRegionDescriptor> BuildRetainedDescriptors(
        GeoRegionPartitionResult oldResult,
        bool[] includedMask,
        int[] closureTileIds,
        int[] changedBaseTileIds,
        GeoRegionGeneratedLayerMask generatedLayers,
        GeoRegionTerrainSnapshot newTerrain,
        GeoRegionPartitionBaseArrays newArrays,
        GeoRegionRuleSnapshot rules)
    {
        var invalidated = new bool[oldResult.RegionCount];
        for (int i = 0; i < closureTileIds.Length; i++)
        {
            int tileId = closureTileIds[i];
            for (int layerIndex = 0; layerIndex < GeoRegionPartitionCodec.LayerCount; layerIndex++)
            {
                var layer = (GeoRegionLayer)layerIndex;
                if (!IsLayerGenerated(generatedLayers, layer)) continue;
                int slot = oldResult.GetRegionSlot(tileId, layer);
                if (slot >= 0) invalidated[slot] = true;
            }
        }

        var refreshMetadata = new bool[oldResult.RegionCount];
        GeoRegionLayer[] metadataLayers =
        {
            GeoRegionLayer.Landmass,
            GeoRegionLayer.Peninsula,
            GeoRegionLayer.Strait,
            GeoRegionLayer.Archipelago
        };
        for (int i = 0; i < changedBaseTileIds.Length; i++)
        {
            int tileId = changedBaseTileIds[i];
            for (int layerIndex = 0; layerIndex < metadataLayers.Length; layerIndex++)
            {
                GeoRegionLayer layer = metadataLayers[layerIndex];
                if (IsLayerGenerated(generatedLayers, layer)) continue;
                int slot = oldResult.GetRegionSlot(tileId, layer);
                if (slot >= 0) refreshMetadata[slot] = true;
            }
        }

        var retained = new List<GeoRegionDescriptor>(oldResult.RegionCount);
        for (int regionIndex = 0; regionIndex < oldResult.RegionCount; regionIndex++)
        {
            GeoRegionDescriptor descriptor = oldResult.GetRegion(regionIndex);
            if (invalidated[regionIndex])
            {
                for (int position = 0; position < descriptor.TileCount; position++)
                {
                    int tileId = descriptor.GetTileId(position);
                    if (!includedMask[tileId])
                    {
                        throw new InvalidOperationException(
                            $"GeoRegion 旧 descriptor 未完整纳入 closure: layer={descriptor.Layer}, tile={tileId}");
                    }
                }
                continue;
            }

            retained.Add(refreshMetadata[regionIndex]
                ? RefreshDominantMetadata(descriptor, newTerrain, newArrays, rules)
                : descriptor);
        }
        return retained;
    }

    /// <summary>
    /// 用新基础数组重新统计一个保留区域的主要分类、主要地貌和生态组成，
    /// 格子归属不变时避免重建整个区域层。
    /// </summary>
    private static GeoRegionDescriptor RefreshDominantMetadata(
        GeoRegionDescriptor descriptor,
        GeoRegionTerrainSnapshot terrain,
        GeoRegionPartitionBaseArrays arrays,
        GeoRegionRuleSnapshot rules)
    {
        if (descriptor.Layer is not (
                GeoRegionLayer.Landmass or
                GeoRegionLayer.Peninsula or
                GeoRegionLayer.Strait or
                GeoRegionLayer.Archipelago))
        {
            return descriptor;
        }

        var primaryCounts = new int[GeoRegionPartitionCodec.PrimaryCodeCount];
        var landformCounts = new int[GeoRegionPartitionCodec.LandformCodeCount];
        for (int position = 0; position < descriptor.TileCount; position++)
        {
            int tileId = descriptor.GetTileId(position);
            byte primaryCode = arrays.PrimaryCategoryCode[tileId];
            byte landformCode = arrays.LandformCode[tileId];
            if (primaryCode > 0 && primaryCode < primaryCounts.Length) primaryCounts[primaryCode]++;
            if (landformCode > 0 && landformCode < landformCounts.Length) landformCounts[landformCode]++;
        }

        int dominantPrimaryIndex = ArgMax(primaryCounts);
        int dominantLandformIndex = ArgMax(landformCounts);
        GeoRegionPrimaryCategoryCode dominantPrimary = dominantPrimaryIndex == 0
            ? GeoRegionPrimaryCategoryCode.None
            : rules.GetPrimaryRule((GeoRegionPrimaryCategoryCode)dominantPrimaryIndex).PrimaryCode;
        GeoRegionLandformCode dominantLandform = dominantLandformIndex == 0
            ? GeoRegionLandformCode.None
            : rules.GetLandformRule((GeoRegionLandformCode)dominantLandformIndex).LandformCode;
        List<int> tileIds = descriptor.CopyTileIds();
        GeoRegionBiomeCompositionData biomeComposition = GeoRegionBiomeComposition.Build(
            terrain,
            tileIds,
            descriptor.CoreTileCount);
        if (dominantPrimary == descriptor.DominantPrimaryCode &&
            dominantLandform == descriptor.DominantLandformCode &&
            BiomeCompositionEquals(descriptor, biomeComposition))
        {
            return descriptor;
        }

        return new GeoRegionDescriptor(
            tileIds,
            descriptor.Layer,
            descriptor.CategoryCode,
            descriptor.BaseTerrainLayer,
            descriptor.WaterKind,
            descriptor.TouchesEdge,
            descriptor.CoreTileCount,
            descriptor.IsMixed,
            descriptor.TopologyExempt,
            descriptor.CoreSignature,
            descriptor.CopyRawSignatures(),
            descriptor.CopyRawSignatureTileCounts(),
            biomeComposition.CoreBiomeId,
            biomeComposition.DominantBiomeId,
            biomeComposition.BiomeIds,
            biomeComposition.BiomeTileCounts,
            descriptor.CenterX,
            descriptor.CenterY,
            dominantPrimary,
            dominantLandform);
    }

    private static bool BiomeCompositionEquals(
        GeoRegionDescriptor descriptor,
        GeoRegionBiomeCompositionData composition)
    {
        if (!string.Equals(descriptor.CoreBiomeId, composition.CoreBiomeId, StringComparison.Ordinal) ||
            !string.Equals(descriptor.DominantBiomeId, composition.DominantBiomeId, StringComparison.Ordinal) ||
            descriptor.BiomeCompositionCount != composition.BiomeIds.Length)
        {
            return false;
        }
        for (int i = 0; i < composition.BiomeIds.Length; i++)
        {
            if (!string.Equals(descriptor.GetBiomeId(i), composition.BiomeIds[i], StringComparison.Ordinal) ||
                descriptor.GetBiomeTileCount(i) != composition.BiomeTileCounts[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int ArgMax(int[] counts)
    {
        int bestIndex = 0;
        int bestCount = -1;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= bestCount) continue;
            bestIndex = i;
            bestCount = counts[i];
        }
        return bestIndex;
    }

    private static bool IsLayerGenerated(GeoRegionGeneratedLayerMask mask, GeoRegionLayer layer)
    {
        return (mask & (GeoRegionGeneratedLayerMask)(1 << (int)layer)) != 0;
    }

    /// <summary>
    /// 从变化格开始，逐轮收集所有可能影响同一次分区判断的格子，
    /// 产出一个可独立重算且不会截断旧区域或新连续地形的范围。
    /// </summary>
    private sealed class ClosureBuilder
    {
        // 新旧快照和基础数组用于同时检查变化前后的连续地形。
        private readonly GeoRegionTerrainSnapshot oldTerrain;
        private readonly GeoRegionTerrainSnapshot newTerrain;
        private readonly GeoRegionPartitionResult oldResult;
        private readonly GeoRegionPartitionBaseArrays oldArrays;
        private readonly GeoRegionPartitionBaseArrays newArrays;
        private readonly GeoRegionRuleSnapshot rules;
        // 记录已纳入重算范围的格子，以及尚需继续向外检查的位置。
        private readonly bool[] includedMask;
        private readonly List<int> includedTileIds = new();
        private readonly bool[] oldDescriptorAdded;
        // 搜索队列和访问标记用于沿相邻格找完整陆地、水体或其他同类地形。
        private readonly int[] queue;
        private readonly bool[] oldTopologyExpanded;
        private readonly bool[] newTopologyExpanded;
        // 两个分类索引用于找出碎片可能并入或影响的相邻区域。
        private readonly RegularizationComponentIndex primaryRegularization;
        private readonly RegularizationComponentIndex landformRegularization;
        private readonly HashSet<int> expandedPrimaryComponents = new();
        private readonly HashSet<int> expandedLandformComponents = new();
        private readonly List<int> componentFrontier = new();
        // 各处理位置只向前推进，避免每轮重复扫描已经展开过的格子。
        private int descriptorFrontierPosition;
        private int regularizationFrontierPosition;
        private int oldTopologyFrontierPosition;
        private int newTopologyFrontierPosition;
        private int count;

        /// <summary>
        /// 使用新旧地形、旧结果和新旧基础分类初始化范围收集器；初始重算范围为空。
        /// </summary>
        internal ClosureBuilder(
            GeoRegionTerrainSnapshot oldTerrain,
            GeoRegionTerrainSnapshot newTerrain,
            GeoRegionPartitionResult oldResult,
            GeoRegionPartitionBaseArrays oldArrays,
            GeoRegionPartitionBaseArrays newArrays,
            GeoRegionRuleSnapshot rules)
        {
            this.oldTerrain = oldTerrain;
            this.newTerrain = newTerrain;
            this.oldResult = oldResult;
            this.oldArrays = oldArrays;
            this.newArrays = newArrays;
            this.rules = rules;
            includedMask = new bool[newTerrain.CellCount];
            oldDescriptorAdded = new bool[oldResult.RegionCount];
            queue = new int[newTerrain.CellCount];
            oldTopologyExpanded = new bool[newTerrain.CellCount];
            newTopologyExpanded = new bool[newTerrain.CellCount];
            primaryRegularization = new RegularizationComponentIndex(
                newTerrain.Width,
                newTerrain.Height,
                newArrays.PrimarySignature,
                newArrays.IsWater,
                rules);
            landformRegularization = new RegularizationComponentIndex(
                newTerrain.Width,
                newTerrain.Height,
                newArrays.LandformCode,
                newArrays.IsLand,
                rules);
        }

        /// <summary>用格子编号标出哪些位置需要重新计算。</summary>
        internal bool[] IncludedMask => includedMask;
        /// <summary>当前需要重新计算的格子数量。</summary>
        internal int Count => count;
        /// <summary>为了找全受影响地区已经向外检查了多少轮。</summary>
        internal int Rounds { get; private set; }
        /// <summary>重算范围过大时为真，此时直接重新计算整张地图更合适。</summary>
        internal bool ExceedsFullFallbackThreshold => (long)count * 100 > (long)includedMask.Length * FullFallbackPercent;

        /// <summary>
        /// 返回按格子编号排序的重算范围，供结果和诊断信息稳定输出。
        /// </summary>
        internal int[] CopyIncludedTileIds()
        {
            int[] result = includedTileIds.ToArray();
            Array.Sort(result);
            return result;
        }

        /// <summary>
        /// 把一个格子加入重算范围；若之前已加入则不重复记录。
        /// </summary>
        internal bool Add(int tileId)
        {
            if (includedMask[tileId]) return false;
            includedMask[tileId] = true;
            includedTileIds.Add(tileId);
            count++;
            return true;
        }

        /// <summary>
        /// 以陆水变化格为中心加入指定半径的邻域，覆盖半岛厚度和狭窄水道判断会读取的周边格子，
        /// 防止局部重算漏掉间接受影响区域。
        /// </summary>
        internal void AddDependencyNeighborhood(
            IList<int> seedTileIds,
            int radius,
            CancellationToken cancellationToken)
        {
            if (seedTileIds == null) throw new ArgumentNullException(nameof(seedTileIds));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            int width = newTerrain.Width;
            int height = newTerrain.Height;
            for (int seedIndex = 0; seedIndex < seedTileIds.Count; seedIndex++)
            {
                if ((seedIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                int seedTileId = seedTileIds[seedIndex];
                int centerX = seedTileId % width;
                int centerY = seedTileId / width;
                int minX = Math.Max(0, centerX - radius);
                int maxX = Math.Min(width - 1, centerX + radius);
                int minY = Math.Max(0, centerY - radius);
                int maxY = Math.Min(height - 1, centerY + radius);
                for (int y = minY; y <= maxY; y++)
                {
                    int rowOffset = y * width;
                    for (int x = minX; x <= maxX; x++)
                    {
                        Add(rowOffset + x);
                    }
                }
            }
        }

        /// <summary>
        /// 执行一轮范围扩张：纳入相交旧区域、变化前后完整连续地形及可能参与碎片归并的区域。
        /// 返回本轮是否加入新格子，供调用方重复执行直到范围稳定。
        /// </summary>
        internal bool ExpandRound(
            GeoRegionGeneratedLayerMask generatedLayers,
            bool topologyMode,
            CancellationToken cancellationToken)
        {
            int before = count;
            Rounds++;
            AddIntersectingOldDescriptors(generatedLayers, cancellationToken);
            if (ExceedsFullFallbackThreshold) return count != before;
            if (topologyMode)
            {
                ExpandTopologyComponents(
                    oldTerrain,
                    oldArrays,
                    oldTopologyExpanded,
                    ref oldTopologyFrontierPosition,
                    cancellationToken);
                ExpandTopologyComponents(
                    newTerrain,
                    newArrays,
                    newTopologyExpanded,
                    ref newTopologyFrontierPosition,
                    cancellationToken);
                if (ExceedsFullFallbackThreshold) return count != before;
                ExpandArchipelagoGraph(cancellationToken);
                if (ExceedsFullFallbackThreshold) return count != before;
            }
            ExpandRegularizationDomains(cancellationToken);
            return count != before;
        }

        /// <summary>
        /// 只要重算范围碰到某个将重新生成的旧区域，就把该区域全部格子纳入，
        /// 避免只重建半个旧区域而造成归属重叠或缺口。
        /// </summary>
        private void AddIntersectingOldDescriptors(
            GeoRegionGeneratedLayerMask generatedLayers,
            CancellationToken cancellationToken)
        {
            while (descriptorFrontierPosition < includedTileIds.Count)
            {
                int tilePosition = descriptorFrontierPosition++;
                if ((tilePosition & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                int tileId = includedTileIds[tilePosition];
                for (int layerIndex = 0; layerIndex < GeoRegionPartitionCodec.LayerCount; layerIndex++)
                {
                    GeoRegionLayer layer = (GeoRegionLayer)layerIndex;
                    if (!IsLayerGenerated(generatedLayers, layer)) continue;
                    int regionIndex = oldResult.GetRegionSlot(tileId, layer);
                    if (regionIndex < 0 || oldDescriptorAdded[regionIndex]) continue;

                    oldDescriptorAdded[regionIndex] = true;
                    GeoRegionDescriptor descriptor = oldResult.GetRegion(regionIndex);
                    for (int position = 0; position < descriptor.TileCount; position++)
                    {
                        Add(descriptor.GetTileId(position));
                    }
                }
            }
        }

        /// <summary>
        /// 检查重算范围中的每个格子，把主分类和地貌分类中可能参与碎片归并的相邻区域一并纳入。
        /// 这样局部重算得到的归并目标与完整重算一致。
        /// </summary>
        private void ExpandRegularizationDomains(CancellationToken cancellationToken)
        {
            while (regularizationFrontierPosition < includedTileIds.Count)
            {
                int position = regularizationFrontierPosition++;
                if ((position & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                int tileId = includedTileIds[position];
                ExpandRegularizationGraph(primaryRegularization, expandedPrimaryComponents, tileId);
                ExpandRegularizationGraph(landformRegularization, expandedLandformComponents, tileId);
            }
        }

        /// <summary>
        /// 从指定分类区域向相邻小区域继续展开，遇到达到面积门槛的大区域后停止向外，
        /// 产出完成碎片归并判断所需的最小范围。
        /// </summary>
        private void ExpandRegularizationGraph(
            RegularizationComponentIndex index,
            HashSet<int> expandedComponents,
            int seedTileId)
        {
            int seedComponentId = index.GetComponentId(seedTileId);
            if (seedComponentId < 0) return;

            componentFrontier.Clear();
            IncludeRegularizationComponent(index, seedComponentId);
            if (!expandedComponents.Add(seedComponentId)) return;
            componentFrontier.Add(seedComponentId);
            for (int cursor = 0; cursor < componentFrontier.Count; cursor++)
            {
                int componentId = componentFrontier[cursor];
                RegularizationComponent component = index.GetComponent(componentId);
                if (component.IsFormalCore) continue;

                IReadOnlyList<int> adjacent = index.GetAdjacentComponentIds(componentId);
                for (int i = 0; i < adjacent.Count; i++)
                {
                    int adjacentId = adjacent[i];
                    IncludeRegularizationComponent(index, adjacentId);
                    if (!expandedComponents.Add(adjacentId)) continue;
                    componentFrontier.Add(adjacentId);
                }
            }
        }

        private void IncludeRegularizationComponent(RegularizationComponentIndex index, int componentId)
        {
            IReadOnlyList<int> tileIds = index.GetComponent(componentId).TileIds;
            for (int i = 0; i < tileIds.Count; i++) Add(tileIds[i]);
        }

        /// <summary>
        /// 对重算范围内的新旧格子沿上下左右寻找完整的陆地、水体或同类特殊地形，
        /// 防止陆水变化后只重建连续地形的一部分。
        /// </summary>
        private void ExpandTopologyComponents(
            GeoRegionTerrainSnapshot terrain,
            GeoRegionPartitionBaseArrays arrays,
            bool[] expanded,
            ref int frontierPosition,
            CancellationToken cancellationToken)
        {
            while (frontierPosition < includedTileIds.Count)
            {
                int position = frontierPosition++;
                if ((position & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                int tileId = includedTileIds[position];
                if (expanded[tileId]) continue;

                if (arrays.IsLand[tileId])
                {
                    ExpandBooleanComponent(tileId, arrays.IsLand, expanded);
                }
                else if (arrays.IsWater[tileId])
                {
                    ExpandBooleanComponent(tileId, arrays.IsWater, expanded);
                }
                else
                {
                    ExpandTerrainKindComponent(tileId, terrain, expanded, terrain.GetCell(tileId).TerrainKind);
                }
            }
        }

        private void ExpandBooleanComponent(int start, GeoRegionPagedArray<bool> values, bool[] expanded)
        {
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            expanded[start] = true;
            Add(start);
            while (head < tail)
            {
                int tileId = queue[head++];
                int x = tileId % newTerrain.Width;
                int y = tileId / newTerrain.Width;
                VisitBooleanNeighbor(tileId, x, y, -1, 0, values, expanded, ref tail);
                VisitBooleanNeighbor(tileId, x, y, 1, 0, values, expanded, ref tail);
                VisitBooleanNeighbor(tileId, x, y, 0, -1, values, expanded, ref tail);
                VisitBooleanNeighbor(tileId, x, y, 0, 1, values, expanded, ref tail);
            }
        }

        private void VisitBooleanNeighbor(
            int tileId,
            int x,
            int y,
            int dx,
            int dy,
            GeoRegionPagedArray<bool> values,
            bool[] expanded,
            ref int tail)
        {
            int nx = x + dx;
            int ny = y + dy;
            if ((uint)nx >= (uint)newTerrain.Width || (uint)ny >= (uint)newTerrain.Height) return;
            int neighbor = tileId + dx + dy * newTerrain.Width;
            if (expanded[neighbor] || !values[neighbor]) return;
            expanded[neighbor] = true;
            Add(neighbor);
            queue[tail++] = neighbor;
        }

        private void ExpandTerrainKindComponent(
            int start,
            GeoRegionTerrainSnapshot terrain,
            bool[] expanded,
            GeoRegionTerrainKind kind)
        {
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            expanded[start] = true;
            Add(start);
            while (head < tail)
            {
                int tileId = queue[head++];
                int x = tileId % newTerrain.Width;
                int y = tileId / newTerrain.Width;
                VisitTerrainKindNeighbor(tileId, x, y, -1, 0, terrain, expanded, kind, ref tail);
                VisitTerrainKindNeighbor(tileId, x, y, 1, 0, terrain, expanded, kind, ref tail);
                VisitTerrainKindNeighbor(tileId, x, y, 0, -1, terrain, expanded, kind, ref tail);
                VisitTerrainKindNeighbor(tileId, x, y, 0, 1, terrain, expanded, kind, ref tail);
            }
        }

        private void VisitTerrainKindNeighbor(
            int tileId,
            int x,
            int y,
            int dx,
            int dy,
            GeoRegionTerrainSnapshot terrain,
            bool[] expanded,
            GeoRegionTerrainKind kind,
            ref int tail)
        {
            int nx = x + dx;
            int ny = y + dy;
            if ((uint)nx >= (uint)newTerrain.Width || (uint)ny >= (uint)newTerrain.Height) return;
            int neighbor = tileId + dx + dy * newTerrain.Width;
            if (expanded[neighbor] || terrain.GetCell(neighbor).TerrainKind != kind) return;
            expanded[neighbor] = true;
            Add(neighbor);
            queue[tail++] = neighbor;
        }

        /// <summary>
        /// 找出重算范围内的小岛，并把距离足以影响群岛归组的旧小岛一并纳入，
        /// 防止局部更新遗漏跨范围的群岛关系。
        /// </summary>
        private void ExpandArchipelagoGraph(CancellationToken cancellationToken)
        {
            int islandMaxTiles = Math.Max(0, rules.Archipelago.IslandMaxTiles);
            int islandMinTiles = Math.Max(1, rules.LandmassIsland.MinTiles);
            int maxGap = Math.Max(0, rules.Archipelago.MaxGap);
            if (islandMaxTiles <= 0) return;

            List<IslandBounds> includedIslands = CollectIncludedIslandBounds(islandMinTiles, islandMaxTiles);
            if (includedIslands.Count == 0) return;

            for (int regionIndex = 0; regionIndex < oldResult.RegionCount; regionIndex++)
            {
                if ((regionIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                GeoRegionDescriptor descriptor = oldResult.GetRegion(regionIndex);
                if (descriptor.Layer != GeoRegionLayer.Landmass ||
                    descriptor.TouchesEdge ||
                    descriptor.TileCount < islandMinTiles ||
                    descriptor.TileCount > islandMaxTiles)
                {
                    continue;
                }

                IslandBounds oldIsland = GetBounds(descriptor);
                bool adjacent = false;
                for (int i = 0; i < includedIslands.Count; i++)
                {
                    if (!IsWithinGap(includedIslands[i], oldIsland, maxGap)) continue;
                    adjacent = true;
                    break;
                }
                if (!adjacent) continue;

                for (int position = 0; position < descriptor.TileCount; position++)
                {
                    Add(descriptor.GetTileId(position));
                }
            }
        }

        /// <summary>
        /// 从当前重算范围收集符合群岛候选面积的完整小岛外接矩形。
        /// </summary>
        private List<IslandBounds> CollectIncludedIslandBounds(int minTiles, int maxTiles)
        {
            var islands = new List<IslandBounds>();
            var visited = new bool[includedMask.Length];
            for (int position = 0; position < includedTileIds.Count; position++)
            {
                int start = includedTileIds[position];
                if (!newArrays.IsLand[start] || visited[start]) continue;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                int minX = start % newTerrain.Width;
                int maxX = minX;
                int minY = start / newTerrain.Width;
                int maxY = minY;
                bool touchesEdge = false;
                while (head < tail)
                {
                    int tileId = queue[head++];
                    int x = tileId % newTerrain.Width;
                    int y = tileId / newTerrain.Width;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                    touchesEdge |= x == 0 || y == 0 || x == newTerrain.Width - 1 || y == newTerrain.Height - 1;
                    VisitIncludedLand(tileId, x, y, -1, 0, visited, ref tail);
                    VisitIncludedLand(tileId, x, y, 1, 0, visited, ref tail);
                    VisitIncludedLand(tileId, x, y, 0, -1, visited, ref tail);
                    VisitIncludedLand(tileId, x, y, 0, 1, visited, ref tail);
                }

                if (!touchesEdge && tail >= minTiles && tail <= maxTiles)
                {
                    islands.Add(new IslandBounds(minX, minY, maxX, maxY));
                }
            }
            return islands;
        }

        private void VisitIncludedLand(
            int tileId,
            int x,
            int y,
            int dx,
            int dy,
            bool[] visited,
            ref int tail)
        {
            int nx = x + dx;
            int ny = y + dy;
            if ((uint)nx >= (uint)newTerrain.Width || (uint)ny >= (uint)newTerrain.Height) return;
            int neighbor = tileId + dx + dy * newTerrain.Width;
            if (!newArrays.IsLand[neighbor] || visited[neighbor]) return;
            if (!includedMask[neighbor])
            {
                throw new InvalidOperationException(
                    $"GeoRegion topology closure 未完整包含新陆地分量: tile={neighbor}");
            }
            visited[neighbor] = true;
            queue[tail++] = neighbor;
        }

        private IslandBounds GetBounds(GeoRegionDescriptor descriptor)
        {
            int first = descriptor.GetTileId(0);
            int minX = first % newTerrain.Width;
            int maxX = minX;
            int minY = first / newTerrain.Width;
            int maxY = minY;
            for (int position = 1; position < descriptor.TileCount; position++)
            {
                int tileId = descriptor.GetTileId(position);
                int x = tileId % newTerrain.Width;
                int y = tileId / newTerrain.Width;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
            return new IslandBounds(minX, minY, maxX, maxY);
        }

        private static bool IsWithinGap(IslandBounds left, IslandBounds right, int maxGap)
        {
            int dx = 0;
            if (left.MaxX < right.MinX) dx = right.MinX - left.MaxX - 1;
            else if (right.MaxX < left.MinX) dx = left.MinX - right.MaxX - 1;

            int dy = 0;
            if (left.MaxY < right.MinY) dy = right.MinY - left.MaxY - 1;
            else if (right.MaxY < left.MinY) dy = left.MinY - right.MaxY - 1;
            return Math.Max(dx, dy) <= maxGap;
        }
    }

    /// <summary>
    /// 按需建立“分类相同且上下左右连续”的区域索引，并记录区域面积及相邻关系，
    /// 用于判断某个小碎片可能并入哪些大区域。
    /// </summary>
    private sealed class RegularizationComponentIndex
    {
        // 网格尺寸用于在一维格子编号和二维邻接位置之间换算。
        private readonly int width;
        private readonly int height;
        // 主分类和地貌分类二选一使用；可参与标记限定地貌阶段只检查陆地格。
        private readonly GeoRegionPagedArray<int> primarySignatures;
        private readonly GeoRegionPagedArray<byte> landformSignatures;
        private readonly GeoRegionPagedArray<bool> eligible;
        // 主分类还需区分陆地与水体，规则用于判断区域是否达到独立面积门槛。
        private readonly GeoRegionPagedArray<bool> isWater;
        private readonly GeoRegionRuleSnapshot rules;
        private readonly bool primary;
        private readonly int[] componentMarkers;
        // 标记、队列和区域列表支持按需搜索并缓存已经发现的区域。
        private readonly int[] queue;
        private readonly List<RegularizationComponent> components = new();

        /// <summary>
        /// 为主分类创建索引，输入每格分类编码和水体标记。
        /// </summary>
        internal RegularizationComponentIndex(
            int width,
            int height,
            GeoRegionPagedArray<int> signatures,
            GeoRegionPagedArray<bool> isWater,
            GeoRegionRuleSnapshot rules)
        {
            this.width = width;
            this.height = height;
            primarySignatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            this.isWater = isWater ?? throw new ArgumentNullException(nameof(isWater));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            primary = true;
            int tileCount = checked(width * height);
            if (signatures.Length != tileCount || isWater.Length != tileCount)
            {
                throw new InvalidOperationException("GeoRegion Primary regularization component index 尺寸不一致");
            }
            componentMarkers = new int[tileCount];
            queue = new int[tileCount];
        }

        /// <summary>
        /// 为地貌分类创建索引，输入每格地貌编码和可参与判断的陆地标记。
        /// </summary>
        internal RegularizationComponentIndex(
            int width,
            int height,
            GeoRegionPagedArray<byte> signatures,
            GeoRegionPagedArray<bool> eligible,
            GeoRegionRuleSnapshot rules)
        {
            this.width = width;
            this.height = height;
            landformSignatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            this.eligible = eligible ?? throw new ArgumentNullException(nameof(eligible));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            primary = false;
            int tileCount = checked(width * height);
            if (signatures.Length != tileCount || eligible.Length != tileCount)
            {
                throw new InvalidOperationException("GeoRegion Landform regularization component index 尺寸不一致");
            }
            componentMarkers = new int[tileCount];
            queue = new int[tileCount];
        }

        /// <summary>
        /// 返回指定格子所属的分类区域编号；首次访问时从相邻格向外寻找并缓存整个区域。
        /// 不参与该层判断的格子返回 -1。
        /// </summary>
        internal int GetComponentId(int tileId)
        {
            if ((uint)tileId >= (uint)componentMarkers.Length) throw new ArgumentOutOfRangeException(nameof(tileId));
            if (eligible != null && !eligible[tileId]) return -1;
            int marker = componentMarkers[tileId];
            if (marker > 0) return marker - 1;

            int componentId = components.Count;
            int signature = GetSignature(tileId);
            int physicalDomain = ResolvePhysicalDomain(tileId);
            int head = 0;
            int tail = 0;
            var tileIds = new List<int>();
            componentMarkers[tileId] = componentId + 1;
            queue[tail++] = tileId;
            while (head < tail)
            {
                int current = queue[head++];
                tileIds.Add(current);
                int x = current % width;
                int y = current / width;
                VisitRawNeighbor(current, x, y, -1, 0, componentId, signature, physicalDomain, ref tail);
                VisitRawNeighbor(current, x, y, 1, 0, componentId, signature, physicalDomain, ref tail);
                VisitRawNeighbor(current, x, y, 0, -1, componentId, signature, physicalDomain, ref tail);
                VisitRawNeighbor(current, x, y, 0, 1, componentId, signature, physicalDomain, ref tail);
            }
            tileIds.Sort();
            int minTiles = primary
                ? GeoRegionPartitioner.ResolvePrimaryMinTilesBySignature(rules, signature)
                : GeoRegionPartitioner.ResolveLandformMinTilesBySignature(rules, signature);
            components.Add(new RegularizationComponent(
                tileIds,
                signature,
                physicalDomain,
                tileIds.Count >= Math.Max(1, minTiles)));
            return componentId;
        }

        /// <summary>按编号取得一块分类相同且彼此相连的格子。</summary>
        internal RegularizationComponent GetComponent(int componentId)
        {
            if ((uint)componentId >= (uint)components.Count) throw new ArgumentOutOfRangeException(nameof(componentId));
            return components[componentId];
        }

        /// <summary>
        /// 返回同一陆水范围内与指定分类区域上下左右相接的其他区域编号。
        /// </summary>
        internal IReadOnlyList<int> GetAdjacentComponentIds(int componentId)
        {
            RegularizationComponent component = GetComponent(componentId);
            if (component.AdjacentComponentIds != null) return component.AdjacentComponentIds;

            var adjacent = new HashSet<int>();
            for (int i = 0; i < component.TileIds.Count; i++)
            {
                int tileId = component.TileIds[i];
                int x = tileId % width;
                int y = tileId / width;
                CollectAdjacent(tileId, x, y, -1, 0, componentId, component.PhysicalDomain, adjacent);
                CollectAdjacent(tileId, x, y, 1, 0, componentId, component.PhysicalDomain, adjacent);
                CollectAdjacent(tileId, x, y, 0, -1, componentId, component.PhysicalDomain, adjacent);
                CollectAdjacent(tileId, x, y, 0, 1, componentId, component.PhysicalDomain, adjacent);
            }
            var result = new List<int>(adjacent);
            result.Sort();
            component.AdjacentComponentIds = result;
            return result;
        }

        private void VisitRawNeighbor(
            int tileId,
            int x,
            int y,
            int dx,
            int dy,
            int componentId,
            int signature,
            int physicalDomain,
            ref int tail)
        {
            int nx = x + dx;
            int ny = y + dy;
            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) return;
            int neighbor = tileId + dx + dy * width;
            if (componentMarkers[neighbor] != 0 || eligible != null && !eligible[neighbor] ||
                GetSignature(neighbor) != signature || ResolvePhysicalDomain(neighbor) != physicalDomain)
            {
                return;
            }
            componentMarkers[neighbor] = componentId + 1;
            queue[tail++] = neighbor;
        }

        private void CollectAdjacent(
            int tileId,
            int x,
            int y,
            int dx,
            int dy,
            int componentId,
            int physicalDomain,
            HashSet<int> adjacent)
        {
            int nx = x + dx;
            int ny = y + dy;
            if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) return;
            int neighbor = tileId + dx + dy * width;
            if (eligible != null && !eligible[neighbor] || ResolvePhysicalDomain(neighbor) != physicalDomain) return;
            int adjacentId = GetComponentId(neighbor);
            if (adjacentId >= 0 && adjacentId != componentId) adjacent.Add(adjacentId);
        }

        private int GetSignature(int tileId)
        {
            return primary ? primarySignatures[tileId] : landformSignatures[tileId];
        }

        private int ResolvePhysicalDomain(int tileId)
        {
            return primary && isWater[tileId] ? 1 : 2;
        }
    }

    /// <summary>
    /// 保存一块分类相同的连续格子、所属陆水范围及是否达到独立面积门槛，
    /// 相邻区域列表在首次需要时补充。
    /// </summary>
    private sealed class RegularizationComponent
    {
        internal RegularizationComponent(
            List<int> tileIds,
            int signature,
            int physicalDomain,
            bool isFormalCore)
        {
            TileIds = tileIds;
            Signature = signature;
            PhysicalDomain = physicalDomain;
            IsFormalCore = isFormalCore;
        }

        /// <summary>这块连续区域包含的所有格子编号。</summary>
        internal List<int> TileIds { get; }
        /// <summary>用于区分地表类别的稳定编号。</summary>
        internal int Signature { get; }
        /// <summary>表示这块区域属于陆地还是水域。</summary>
        internal int PhysicalDomain { get; }
        /// <summary>格子数是否已经达到单独成为地区的面积要求。</summary>
        internal bool IsFormalCore { get; }
        /// <summary>与它上下左右相接的其他连续区域编号，首次需要时才计算。</summary>
        internal List<int> AdjacentComponentIds { get; set; }
    }

    /// <summary>
    /// 小岛在网格中的外接矩形，用于快速判断岛屿间距。
    /// </summary>
    private readonly struct IslandBounds
    {
        internal IslandBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        internal int MinX { get; }
        internal int MinY { get; }
        internal int MaxX { get; }
        internal int MaxY { get; }
    }
}
