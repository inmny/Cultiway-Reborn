using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Core.GeoRegions.Partitioning;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程把新的地区划分结果接回现有游戏对象，并让旧地区尽量继续使用。
/// 新旧地区在同一层共用格子越多，越优先沿用同一个旧对象；其余地区才新建或退役。
/// 同时负责保留玩家名称、修正当前选中地区并收集需要刷新的地图和关系。
/// </summary>
internal static class GeoRegionReconciler
{
    /// <summary>
    /// 比较新旧两版地区划分，让能够对应上的旧地区对象继续使用，并建立下一版格子归属。
    /// 整个过程在失败时会恢复已修改的旧对象并删除本次新建对象，避免留下半成品。
    /// </summary>
    internal static GeoRegionReconciliationResult Reconcile(
        GeoRegionManager manager,
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        GeoRegionLibrary library,
        WorldTile[] tiles,
        IReadOnlyList<int> affectedTileIds,
        IReadOnlyList<int> dirtyTileIds,
        int nextMembershipRevision)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (oldMembership == null) throw new ArgumentNullException(nameof(oldMembership));
        if (oldResult == null) throw new ArgumentNullException(nameof(oldResult));
        if (newResult == null) throw new ArgumentNullException(nameof(newResult));
        if (library == null) throw new ArgumentNullException(nameof(library));
        if (tiles == null) throw new ArgumentNullException(nameof(tiles));
        if (affectedTileIds == null) throw new ArgumentNullException(nameof(affectedTileIds));
        if (dirtyTileIds == null) throw new ArgumentNullException(nameof(dirtyTileIds));
        if (oldMembership.RegionCount != oldResult.RegionCount)
        {
            throw new InvalidOperationException(
                $"GeoRegion baseline slot 数量不一致: membership={oldMembership.RegionCount}, result={oldResult.RegionCount}");
        }
        if (oldResult.WorldSeedId != newResult.WorldSeedId ||
            oldResult.Width != newResult.Width ||
            oldResult.Height != newResult.Height)
        {
            throw new InvalidOperationException("GeoRegion reconciler 新旧纯结果不属于同一世界");
        }

        Dictionary<int, int>[] overlapsByOld = BuildOverlaps(oldResult, newResult);
        int[] bestNewForOld = SelectBestNewForOld(oldResult, newResult, overlapsByOld);
        int[] bestOldForNew = SelectBestOldForNew(oldResult, newResult, overlapsByOld);
        CustomNameTransfer[] customNameTransfers = BuildCustomNameTransfers(
            oldMembership,
            oldResult,
            newResult,
            overlapsByOld,
            bestNewForOld,
            out int customNameTransferCount,
            out int customNameConflictCount);
        GeoRegion[] regionByNewSlot = new GeoRegion[newResult.RegionCount];
        var survivorSet = new HashSet<GeoRegion>();
        var oldSlotByRegion = new Dictionary<GeoRegion, int>(oldResult.RegionCount);
        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            oldSlotByRegion.Add(oldMembership.GetRegionBySlot(oldSlot), oldSlot);
        }

        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            int newSlot = bestNewForOld[oldSlot];
            if (newSlot < 0 || bestOldForNew[newSlot] != oldSlot) continue;

            GeoRegion survivor = oldMembership.GetRegionBySlot(oldSlot);
            regionByNewSlot[newSlot] = survivor;
            survivorSet.Add(survivor);
        }

        var namingSession = new GeoRegionNamingSession();
        var materializer = new GeoRegionMaterializer(
            manager,
            library,
            namingSession,
            newResult.WorldSeedId,
            newResult.Width,
            newResult.Height);
        var categories = new GeoRegionAsset[newResult.RegionCount];
        for (int newSlot = 0; newSlot < newResult.RegionCount; newSlot++)
        {
            GeoRegionDescriptor descriptor = newResult.GetRegion(newSlot);
            GeoRegionAsset category = materializer.ResolveCategory(descriptor.CategoryCode);
            if (category.Layer != descriptor.Layer)
            {
                throw new InvalidOperationException(
                    $"GeoRegion reconciler 分类层级不一致: slot={newSlot}, category={category.id}/{category.Layer}, descriptor={descriptor.Layer}");
            }
            categories[newSlot] = category;

            CustomNameTransfer customNameTransfer = customNameTransfers[newSlot];
            if (customNameTransfer.IsValid)
            {
                namingSession.ReserveName(customNameTransfer.Name);
                continue;
            }

            GeoRegion survivor = regionByNewSlot[newSlot];
            if (survivor?.data == null) continue;
            string previousNamingBiomeId = survivor.data.Layer == GeoRegionLayer.Primary
                ? survivor.data.CoreBiomeId
                : survivor.data.DominantBiomeId;
            string nextNamingBiomeId = GeoRegionNameService.ResolveNamingBiomeId(descriptor);
            bool keepsName = survivor.data.custom_name ||
                             string.Equals(survivor.data.CategoryId, category.id, StringComparison.Ordinal) &&
                             string.Equals(previousNamingBiomeId, nextNamingBiomeId, StringComparison.Ordinal);
            if (keepsName) namingSession.ReserveName(survivor.data.name);
        }

        var createdRegions = new List<GeoRegion>();
        var survivorStates = new List<GeoRegionMutableState>(survivorSet.Count);
        var survivorStateByRegion = new Dictionary<GeoRegion, GeoRegionMutableState>(survivorSet.Count);
        foreach (GeoRegion survivor in survivorSet)
        {
            var state = new GeoRegionMutableState(survivor);
            survivorStates.Add(state);
            survivorStateByRegion.Add(survivor, state);
        }

        try
        {
            var entries = new List<GeoRegionMembershipEntry>(newResult.RegionCount);
            for (int newSlot = 0; newSlot < newResult.RegionCount; newSlot++)
            {
                GeoRegionDescriptor descriptor = newResult.GetRegion(newSlot);
                CustomNameTransfer customNameTransfer = customNameTransfers[newSlot];
                GeoRegion region = regionByNewSlot[newSlot];
                if (region == null)
                {
                    region = materializer.Materialize(
                        descriptor,
                        customNameTransfer.IsValid ? customNameTransfer.Name : null);
                    regionByNewSlot[newSlot] = region;
                    createdRegions.Add(region);
                }
                else
                {
                    materializer.UpdateExisting(region, descriptor, categories[newSlot]);
                    if (customNameTransfer.IsValid)
                    {
                        region.data.name = customNameTransfer.Name;
                        region.data.custom_name = true;
                    }
                }

                entries.Add(new GeoRegionMembershipEntry(region, descriptor.Layer, descriptor.CopyTileIds()));
            }

            int[] regionSlotByTileLayer = newResult.CopyRegionSlotByTileLayer();
            var changeSet = new GeoRegionRuntimeChangeSet();
            int[] assignmentStamps = BuildAssignmentStamps(
                oldMembership,
                newResult,
                regionByNewSlot,
                affectedTileIds,
                changeSet,
                out Dictionary<int, byte> changedLayerMasksByTile);
            var membership = new GeoRegionMembershipSnapshot(
                nextMembershipRevision,
                tiles,
                regionSlotByTileLayer,
                newResult.CopyPositionInRegionByTileLayer(),
                entries,
                assignmentStamps);
            List<GeoRegion> retiredRegions = CollectRetiredRegions(oldMembership, oldResult, survivorSet);
            PopulateRuntimeChangeSet(
                changeSet,
                oldMembership,
                membership,
                oldResult,
                newResult,
                oldSlotByRegion,
                regionByNewSlot,
                survivorStateByRegion,
                createdRegions,
                retiredRegions,
                changedLayerMasksByTile,
                dirtyTileIds,
                tiles);
            Dictionary<GeoRegion, GeoRegion> selectionRedirects = BuildSelectionRedirects(
                oldMembership,
                retiredRegions,
                bestNewForOld,
                regionByNewSlot);
            return new GeoRegionReconciliationResult(
                membership,
                retiredRegions,
                selectionRedirects,
                changeSet,
                createdRegions.Count,
                survivorSet.Count,
                customNameTransferCount,
                customNameConflictCount);
        }
        catch
        {
            for (int i = 0; i < survivorStates.Count; i++) survivorStates[i].Restore();
            for (int i = 0; i < createdRegions.Count; i++)
            {
                GeoRegion region = createdRegions[i];
                if (region != null && !region.isRekt()) manager.removeObject(region);
            }
            throw;
        }
    }

    /// <summary>
    /// 对受影响格子的每个地区层比较新旧对象。
    /// 归属改变时推进该位置的标记，并记录哪些地区对象和地图形状需要刷新。
    /// </summary>
    private static int[] BuildAssignmentStamps(
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionPartitionResult newResult,
        GeoRegion[] regionByNewSlot,
        IReadOnlyList<int> affectedTileIds,
        GeoRegionRuntimeChangeSet changeSet,
        out Dictionary<int, byte> changedLayerMasksByTile)
    {
        int[] stamps = oldMembership.CopyAssignmentStamps();
        changedLayerMasksByTile = new Dictionary<int, byte>();
        var processedTileIds = new HashSet<int>();
        int tileCount = checked(newResult.Width * newResult.Height);

        for (int i = 0; i < affectedTileIds.Count; i++)
        {
            int tileId = affectedTileIds[i];
            if ((uint)tileId >= (uint)tileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(affectedTileIds), tileId, "GeoRegion affected tile 越界");
            }
            if (!processedTileIds.Add(tileId)) continue;

            byte changedLayers = 0;
            for (int layerIndex = 0; layerIndex < GeoRegionMembershipSnapshot.LayerCount; layerIndex++)
            {
                var layer = (GeoRegionLayer)layerIndex;
                GeoRegion oldRegion = oldMembership.GetRegion(tileId, layer);
                int newSlot = newResult.GetRegionSlot(tileId, layer);
                GeoRegion newRegion = newSlot >= 0 ? regionByNewSlot[newSlot] : null;
                if (ReferenceEquals(oldRegion, newRegion)) continue;

                int flatIndex = tileId * GeoRegionMembershipSnapshot.LayerCount + layerIndex;
                stamps[flatIndex] = NextStamp(stamps[flatIndex]);
                changedLayers |= (byte)(1 << layerIndex);
                changeSet.CountChangedAssignment();
                changeSet.AddUnitDirtyRegion(oldRegion);
                changeSet.AddUnitDirtyRegion(newRegion);
                changeSet.AddShapeDirtyRegion(oldRegion);
                changeSet.AddShapeDirtyRegion(newRegion);
            }

            if (changedLayers != 0) changedLayerMasksByTile.Add(tileId, changedLayers);
        }

        return stamps;
    }

    /// <summary>
    /// 汇总本次换版对运行中游戏的影响，包括显示、形状、组成、相邻关系、跨层关系和地图格子。
    /// 调用方随后只刷新这些受影响内容，无需重建所有地区缓存。
    /// </summary>
    private static void PopulateRuntimeChangeSet(
        GeoRegionRuntimeChangeSet changeSet,
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionMembershipSnapshot newMembership,
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        Dictionary<GeoRegion, int> oldSlotByRegion,
        GeoRegion[] regionByNewSlot,
        Dictionary<GeoRegion, GeoRegionMutableState> survivorStateByRegion,
        List<GeoRegion> createdRegions,
        List<GeoRegion> retiredRegions,
        Dictionary<int, byte> changedLayerMasksByTile,
        IReadOnlyList<int> dirtyTileIds,
        WorldTile[] tiles)
    {
        var newSlotByRegion = new Dictionary<GeoRegion, int>(regionByNewSlot.Length);
        for (int newSlot = 0; newSlot < regionByNewSlot.Length; newSlot++)
        {
            newSlotByRegion.Add(regionByNewSlot[newSlot], newSlot);
        }

        var createdSet = new HashSet<GeoRegion>(createdRegions);
        var relationTargetChanges = new HashSet<GeoRegion>();
        for (int newSlot = 0; newSlot < regionByNewSlot.Length; newSlot++)
        {
            GeoRegion region = regionByNewSlot[newSlot];
            bool created = createdSet.Contains(region);
            bool membershipChanged = created || changeSet.IsUnitDirty(region);
            bool presentationChanged = created;
            bool geometryChanged = created || membershipChanged;
            bool relationTargetChanged = false;

            if (!created && survivorStateByRegion.TryGetValue(region, out GeoRegionMutableState state))
            {
                presentationChanged = state.HasPresentationChanged();
                geometryChanged |= state.HasGeometryChanged();
                relationTargetChanged = presentationChanged;
            }

            GeoRegionRuntimeChangeKind changes = GeoRegionRuntimeChangeKind.None;
            if (presentationChanged) changes |= GeoRegionRuntimeChangeKind.Presentation;
            if (geometryChanged) changes |= GeoRegionRuntimeChangeKind.Geometry;
            if (membershipChanged) changes |= GeoRegionRuntimeChangeKind.Composition;
            changeSet.AddRegionChange(region, changes);

            if (relationTargetChanged)
            {
                relationTargetChanges.Add(region);
            }
            if (presentationChanged || geometryChanged)
            {
                changeSet.AddShapeDirtyRegion(region);
            }
            if (presentationChanged)
            {
                AddRegionTilesToMapDirty(changeSet, newMembership, region);
            }
        }

        for (int i = 0; i < retiredRegions.Count; i++)
        {
            GeoRegion region = retiredRegions[i];
            changeSet.AddRegionChange(
                region,
                GeoRegionRuntimeChangeKind.Presentation |
                GeoRegionRuntimeChangeKind.Geometry |
                GeoRegionRuntimeChangeKind.Composition);
            changeSet.AddUnitDirtyRegion(region);
            changeSet.AddShapeDirtyRegion(region);
        }

        foreach (int tileId in changedLayerMasksByTile.Keys)
        {
            AddMapTileAndNeighbors(changeSet, tileId, tiles);
        }

        var processedDirtyTileIds = new HashSet<int>();
        for (int i = 0; i < dirtyTileIds.Count; i++)
        {
            int tileId = dirtyTileIds[i];
            if ((uint)tileId >= (uint)tiles.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(dirtyTileIds), tileId, "GeoRegion dirty tile 越界");
            }
            if (!processedDirtyTileIds.Add(tileId)) continue;

            AddMapTileAndNeighbors(changeSet, tileId, tiles);
            for (int layerIndex = 0; layerIndex < GeoRegionMembershipSnapshot.LayerCount; layerIndex++)
            {
                var layer = (GeoRegionLayer)layerIndex;
                changeSet.AddShapeDirtyRegion(oldMembership.GetRegion(tileId, layer));
                changeSet.AddShapeDirtyRegion(newMembership.GetRegion(tileId, layer));
            }
        }

        foreach (GeoRegion target in relationTargetChanges)
        {
            if (oldSlotByRegion.TryGetValue(target, out int oldSlot))
            {
                AddRelationDependents(
                    changeSet,
                    target,
                    oldResult.GetRegion(oldSlot),
                    oldMembership,
                    tiles);
            }
            if (newSlotByRegion.TryGetValue(target, out int newSlot))
            {
                AddRelationDependents(
                    changeSet,
                    target,
                    newResult.GetRegion(newSlot),
                    newMembership,
                    tiles);
            }
        }

        ResolveRelationDeltas(
            changeSet,
            changedLayerMasksByTile,
            oldMembership,
            newMembership,
            tiles);
    }

    /// <summary>
    /// 只检查归属发生变化的格子，计算同层相邻关系和同格跨层关系相比旧版增减了多少。
    /// </summary>
    private static void ResolveRelationDeltas(
        GeoRegionRuntimeChangeSet changeSet,
        IReadOnlyDictionary<int, byte> changedLayerMasksByTile,
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionMembershipSnapshot newMembership,
        WorldTile[] tiles)
    {
        var adjacencyDeltas = new Dictionary<RegionRelationPair, int>();
        var crossLayerDeltas = new Dictionary<RegionRelationPair, int>();
        var processedEdges = new HashSet<TileLayerEdge>();

        foreach (KeyValuePair<int, byte> pair in changedLayerMasksByTile)
        {
            int tileId = pair.Key;
            byte layerMask = pair.Value;
            for (int layerIndex = 0; layerIndex < GeoRegionMembershipSnapshot.LayerCount; layerIndex++)
            {
                if ((layerMask & (1 << layerIndex)) == 0) continue;
                AccumulateAdjacencyDeltasForTile(
                    adjacencyDeltas,
                    processedEdges,
                    tileId,
                    (GeoRegionLayer)layerIndex,
                    oldMembership,
                    newMembership,
                    tiles);
            }

            AccumulateCrossLayerDeltas(crossLayerDeltas, tileId, oldMembership, -1);
            AccumulateCrossLayerDeltas(crossLayerDeltas, tileId, newMembership, 1);
        }

        ApplyRelationDeltas(changeSet, adjacencyDeltas, GeoRegionRuntimeChangeKind.Adjacency);
        ApplyRelationDeltas(changeSet, crossLayerDeltas, GeoRegionRuntimeChangeKind.CrossLayer);
    }

    /// <summary>
    /// 收集一个格子与周围格子之间的同层相邻关系变化，并兼顾游戏中可能存在的反向邻接记录。
    /// </summary>
    private static void AccumulateAdjacencyDeltasForTile(
        Dictionary<RegionRelationPair, int> deltas,
        HashSet<TileLayerEdge> processedEdges,
        int tileId,
        GeoRegionLayer layer,
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionMembershipSnapshot newMembership,
        WorldTile[] tiles)
    {
        WorldTile[] neighbors = tiles[tileId]?.neighbours;
        if (neighbors == null) return;

        for (int i = 0; i < neighbors.Length; i++)
        {
            if (!TryGetTileId(neighbors[i], tiles.Length, out int neighborId)) continue;
            AccumulateDirectedAdjacencyDelta(
                deltas, processedEdges, tileId, neighborId, layer, oldMembership, newMembership);

            WorldTile[] reverseNeighbors = tiles[neighborId]?.neighbours;
            if (reverseNeighbors == null) continue;
            for (int reverseIndex = 0; reverseIndex < reverseNeighbors.Length; reverseIndex++)
            {
                if (!TryGetTileId(reverseNeighbors[reverseIndex], tiles.Length, out int reverseTargetId) ||
                    reverseTargetId != tileId) continue;
                AccumulateDirectedAdjacencyDelta(
                    deltas, processedEdges, neighborId, tileId, layer, oldMembership, newMembership);
                break;
            }
        }
    }

    /// <summary>
    /// 对一条有方向的格子边先减去旧地区关系、再加上新地区关系；同一条边只处理一次。
    /// </summary>
    private static void AccumulateDirectedAdjacencyDelta(
        Dictionary<RegionRelationPair, int> deltas,
        HashSet<TileLayerEdge> processedEdges,
        int sourceTileId,
        int targetTileId,
        GeoRegionLayer layer,
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionMembershipSnapshot newMembership)
    {
        if (!processedEdges.Add(new TileLayerEdge(sourceTileId, targetTileId, layer))) return;
        AddRelationDelta(
            deltas,
            oldMembership.GetRegion(sourceTileId, layer),
            oldMembership.GetRegion(targetTileId, layer),
            -1);
        AddRelationDelta(
            deltas,
            newMembership.GetRegion(sourceTileId, layer),
            newMembership.GetRegion(targetTileId, layer),
            1);
    }

    /// <summary>
    /// 累加一个格子上各地区层之间的关系；负数移除旧关系，正数加入新关系。
    /// </summary>
    private static void AccumulateCrossLayerDeltas(
        Dictionary<RegionRelationPair, int> deltas,
        int tileId,
        GeoRegionMembershipSnapshot membership,
        int amount)
    {
        for (int sourceLayerIndex = 0;
             sourceLayerIndex < GeoRegionMembershipSnapshot.LayerCount;
             sourceLayerIndex++)
        {
            GeoRegion source = membership.GetRegion(tileId, (GeoRegionLayer)sourceLayerIndex);
            for (int targetLayerIndex = 0;
                 targetLayerIndex < GeoRegionMembershipSnapshot.LayerCount;
                 targetLayerIndex++)
            {
                GeoRegion target = membership.GetRegion(tileId, (GeoRegionLayer)targetLayerIndex);
                AddRelationDelta(deltas, source, target, amount);
            }
        }
    }

    private static void AddRelationDelta(
        Dictionary<RegionRelationPair, int> deltas,
        GeoRegion source,
        GeoRegion target,
        int amount)
    {
        if (source == null || target == null || ReferenceEquals(source, target)) return;
        var pair = new RegionRelationPair(source, target);
        deltas.TryGetValue(pair, out int current);
        deltas[pair] = current + amount;
    }

    /// <summary>
    /// 把最终仍有净变化的地区关系写入刷新集合。
    /// </summary>
    private static void ApplyRelationDeltas(
        GeoRegionRuntimeChangeSet changeSet,
        Dictionary<RegionRelationPair, int> deltas,
        GeoRegionRuntimeChangeKind changeKind)
    {
        foreach (KeyValuePair<RegionRelationPair, int> pair in deltas)
        {
            if (pair.Value != 0) changeSet.AddRegionChange(pair.Key.Source, changeKind);
        }
    }

    /// <summary>
    /// 当某个地区的显示信息变化时，找出所有与它相邻或处在同一格其他层的地区，通知它们刷新关系显示。
    /// </summary>
    private static void AddRelationDependents(
        GeoRegionRuntimeChangeSet changeSet,
        GeoRegion target,
        GeoRegionDescriptor descriptor,
        GeoRegionMembershipSnapshot membership,
        WorldTile[] tiles)
    {
        for (int position = 0; position < descriptor.TileCount; position++)
        {
            int tileId = descriptor.GetTileId(position);
            WorldTile[] neighbors = tiles[tileId]?.neighbours;
            if (neighbors != null)
            {
                for (int i = 0; i < neighbors.Length; i++)
                {
                    if (!TryGetTileId(neighbors[i], tiles.Length, out int neighborId)) continue;
                    GeoRegion adjacent = membership.GetRegion(neighborId, descriptor.Layer);
                    if (!ReferenceEquals(adjacent, target))
                    {
                        changeSet.AddRegionChange(adjacent, GeoRegionRuntimeChangeKind.Adjacency);
                    }
                }
            }

            for (int layerIndex = 0; layerIndex < GeoRegionMembershipSnapshot.LayerCount; layerIndex++)
            {
                GeoRegion related = membership.GetRegion(tileId, (GeoRegionLayer)layerIndex);
                if (!ReferenceEquals(related, target))
                {
                    changeSet.AddRegionChange(related, GeoRegionRuntimeChangeKind.CrossLayer);
                }
            }
        }
    }

    /// <summary>
    /// 把某个地区覆盖的全部格子加入地图显示刷新范围。
    /// </summary>
    private static void AddRegionTilesToMapDirty(
        GeoRegionRuntimeChangeSet changeSet,
        GeoRegionMembershipSnapshot membership,
        GeoRegion region)
    {
        IReadOnlyList<int> tileIds = membership.GetTileIds(region);
        for (int i = 0; i < tileIds.Count; i++) changeSet.AddMapDirtyTile(tileIds[i]);
    }

    /// <summary>
    /// 把一个格子及其有效邻居加入地图显示刷新范围。
    /// </summary>
    private static void AddMapTileAndNeighbors(
        GeoRegionRuntimeChangeSet changeSet,
        int tileId,
        WorldTile[] tiles)
    {
        changeSet.AddMapDirtyTile(tileId);
        WorldTile[] neighbors = tiles[tileId]?.neighbours;
        if (neighbors == null) return;
        for (int i = 0; i < neighbors.Length; i++)
        {
            if (TryGetTileId(neighbors[i], tiles.Length, out int neighborId))
            {
                changeSet.AddMapDirtyTile(neighborId);
            }
        }
    }

    private static bool TryGetTileId(WorldTile tile, int tileCount, out int tileId)
    {
        tileId = tile?.data?.tile_id ?? -1;
        return (uint)tileId < (uint)tileCount;
    }

    private static int NextStamp(int stamp)
    {
        return stamp == int.MaxValue ? 1 : stamp + 1;
    }

    /// <summary>
    /// 统计每个旧地区与同层各新地区共用了多少格子，作为选择继续使用哪个旧对象的主要依据。
    /// </summary>
    private static Dictionary<int, int>[] BuildOverlaps(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult)
    {
        var result = new Dictionary<int, int>[oldResult.RegionCount];
        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            GeoRegionDescriptor oldDescriptor = oldResult.GetRegion(oldSlot);
            var overlaps = new Dictionary<int, int>();
            for (int position = 0; position < oldDescriptor.TileCount; position++)
            {
                int tileId = oldDescriptor.GetTileId(position);
                int newSlot = newResult.GetRegionSlot(tileId, oldDescriptor.Layer);
                if (newSlot < 0) continue;
                overlaps.TryGetValue(newSlot, out int count);
                overlaps[newSlot] = count + 1;
            }
            result[oldSlot] = overlaps;
        }
        return result;
    }

    /// <summary>
    /// 对每个旧地区找出最适合承接它的新地区，用于处理一个旧地区被拆开的情况。
    /// </summary>
    private static int[] SelectBestNewForOld(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        Dictionary<int, int>[] overlapsByOld)
    {
        int[] result = CreateEmptySlots(oldResult.RegionCount);
        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            int bestSlot = -1;
            int bestOverlap = -1;
            foreach (KeyValuePair<int, int> pair in overlapsByOld[oldSlot])
            {
                if (!IsBetterSplitCandidate(
                        oldResult.GetRegion(oldSlot),
                        newResult,
                        pair.Key,
                        pair.Value,
                        bestSlot,
                        bestOverlap)) continue;
                bestSlot = pair.Key;
                bestOverlap = pair.Value;
            }
            result[oldSlot] = bestSlot;
        }
        return result;
    }

    /// <summary>
    /// 对每个新地区找出最适合继续使用的旧地区，用于处理多个旧地区合并的情况。
    /// </summary>
    private static int[] SelectBestOldForNew(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        Dictionary<int, int>[] overlapsByOld)
    {
        int[] result = CreateEmptySlots(newResult.RegionCount);
        int[] bestOverlaps = new int[newResult.RegionCount];
        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            foreach (KeyValuePair<int, int> pair in overlapsByOld[oldSlot])
            {
                int newSlot = pair.Key;
                int bestOldSlot = result[newSlot];
                if (!IsBetterMergeCandidate(
                        oldResult,
                        newResult,
                        newSlot,
                        oldSlot,
                        pair.Value,
                        bestOldSlot,
                        bestOverlaps[newSlot])) continue;
                result[newSlot] = oldSlot;
                bestOverlaps[newSlot] = pair.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// 把玩家自定义名称转给承接原地区的新版地区；多个旧名称竞争时使用与对象复用相同的选择规则。
    /// </summary>
    private static CustomNameTransfer[] BuildCustomNameTransfers(
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        Dictionary<int, int>[] overlapsByOld,
        int[] bestNewForOld,
        out int transferCount,
        out int conflictCount)
    {
        var result = new CustomNameTransfer[newResult.RegionCount];
        var sourceOldSlots = CreateEmptySlots(newResult.RegionCount);
        var sourceOverlaps = new int[newResult.RegionCount];
        var candidateCounts = new int[newResult.RegionCount];

        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            GeoRegion oldRegion = oldMembership.GetRegionBySlot(oldSlot);
            if (oldRegion?.data?.custom_name != true || string.IsNullOrWhiteSpace(oldRegion.data.name)) continue;

            int newSlot = bestNewForOld[oldSlot];
            if (newSlot < 0 || !overlapsByOld[oldSlot].TryGetValue(newSlot, out int overlap)) continue;
            candidateCounts[newSlot]++;

            int currentOldSlot = sourceOldSlots[newSlot];
            if (currentOldSlot >= 0 &&
                !IsBetterMergeCandidate(
                    oldResult,
                    newResult,
                    newSlot,
                    oldSlot,
                    overlap,
                    currentOldSlot,
                    sourceOverlaps[newSlot]))
            {
                continue;
            }

            sourceOldSlots[newSlot] = oldSlot;
            sourceOverlaps[newSlot] = overlap;
            result[newSlot] = new CustomNameTransfer(oldRegion.data.name, oldSlot);
        }

        transferCount = 0;
        conflictCount = 0;
        for (int newSlot = 0; newSlot < result.Length; newSlot++)
        {
            if (!result[newSlot].IsValid) continue;
            transferCount++;
            if (candidateCounts[newSlot] > 1) conflictCount++;
        }
        return result;
    }

    /// <summary>
    /// 旧地区被拆分时比较两个新地区：依次看共用格子数、是否含旧中心、面积和稳定的格子编号顺序。
    /// </summary>
    private static bool IsBetterSplitCandidate(
        GeoRegionDescriptor oldDescriptor,
        GeoRegionPartitionResult newResult,
        int candidateSlot,
        int candidateOverlap,
        int currentSlot,
        int currentOverlap)
    {
        if (candidateOverlap != currentOverlap) return candidateOverlap > currentOverlap;
        if (currentSlot < 0) return true;

        bool candidateContainsCenter = ContainsOldCenter(oldDescriptor, newResult, candidateSlot);
        bool currentContainsCenter = ContainsOldCenter(oldDescriptor, newResult, currentSlot);
        if (candidateContainsCenter != currentContainsCenter) return candidateContainsCenter;

        GeoRegionDescriptor candidate = newResult.GetRegion(candidateSlot);
        GeoRegionDescriptor current = newResult.GetRegion(currentSlot);
        if (candidate.TileCount != current.TileCount) return candidate.TileCount > current.TileCount;
        int candidateMinTile = GetMinimumTileId(candidate);
        int currentMinTile = GetMinimumTileId(current);
        return candidateMinTile != currentMinTile ? candidateMinTile < currentMinTile : candidateSlot < currentSlot;
    }

    /// <summary>
    /// 多个旧地区合并时比较两个旧地区：依次看共用格子数、旧中心是否落入新区、面积和稳定编号顺序。
    /// </summary>
    private static bool IsBetterMergeCandidate(
        GeoRegionPartitionResult oldResult,
        GeoRegionPartitionResult newResult,
        int newSlot,
        int candidateOldSlot,
        int candidateOverlap,
        int currentOldSlot,
        int currentOverlap)
    {
        if (candidateOverlap != currentOverlap) return candidateOverlap > currentOverlap;
        if (currentOldSlot < 0) return true;

        GeoRegionDescriptor candidate = oldResult.GetRegion(candidateOldSlot);
        GeoRegionDescriptor current = oldResult.GetRegion(currentOldSlot);
        bool candidateContainsCenter = ContainsOldCenter(candidate, newResult, newSlot);
        bool currentContainsCenter = ContainsOldCenter(current, newResult, newSlot);
        if (candidateContainsCenter != currentContainsCenter) return candidateContainsCenter;
        if (candidate.TileCount != current.TileCount) return candidate.TileCount > current.TileCount;
        int candidateMinTile = GetMinimumTileId(candidate);
        int currentMinTile = GetMinimumTileId(current);
        return candidateMinTile != currentMinTile
            ? candidateMinTile < currentMinTile
            : candidateOldSlot < currentOldSlot;
    }

    private static bool ContainsOldCenter(
        GeoRegionDescriptor oldDescriptor,
        GeoRegionPartitionResult newResult,
        int newSlot)
    {
        if (newSlot < 0) return false;
        int centerTileId = oldDescriptor.CenterX + oldDescriptor.CenterY * newResult.Width;
        if ((uint)centerTileId >= (uint)checked(newResult.Width * newResult.Height)) return false;
        return newResult.GetRegionSlot(centerTileId, oldDescriptor.Layer) == newSlot;
    }

    private static int GetMinimumTileId(GeoRegionDescriptor descriptor)
    {
        int minimum = int.MaxValue;
        for (int i = 0; i < descriptor.TileCount; i++)
        {
            int tileId = descriptor.GetTileId(i);
            if (tileId < minimum) minimum = tileId;
        }
        return minimum;
    }

    private static int[] CreateEmptySlots(int count)
    {
        int[] result = new int[count];
        for (int i = 0; i < result.Length; i++) result[i] = -1;
        return result;
    }

    /// <summary>
    /// 收集没有被新版地区继续使用的旧对象，交给管理器延后退役。
    /// </summary>
    private static List<GeoRegion> CollectRetiredRegions(
        GeoRegionMembershipSnapshot oldMembership,
        GeoRegionPartitionResult oldResult,
        HashSet<GeoRegion> survivorSet)
    {
        var result = new List<GeoRegion>();
        for (int oldSlot = 0; oldSlot < oldResult.RegionCount; oldSlot++)
        {
            GeoRegion region = oldMembership.GetRegionBySlot(oldSlot);
            if (!survivorSet.Contains(region)) result.Add(region);
        }
        return result;
    }

    /// <summary>
    /// 为被退役但曾处于选中状态的旧地区，记录最接近的新地区作为选中目标。
    /// </summary>
    private static Dictionary<GeoRegion, GeoRegion> BuildSelectionRedirects(
        GeoRegionMembershipSnapshot oldMembership,
        List<GeoRegion> retiredRegions,
        int[] bestNewForOld,
        GeoRegion[] regionByNewSlot)
    {
        var redirects = new Dictionary<GeoRegion, GeoRegion>();
        var retiredSet = new HashSet<GeoRegion>(retiredRegions);
        for (int oldSlot = 0; oldSlot < bestNewForOld.Length; oldSlot++)
        {
            GeoRegion oldRegion = oldMembership.GetRegionBySlot(oldSlot);
            if (!retiredSet.Contains(oldRegion)) continue;
            int newSlot = bestNewForOld[oldSlot];
            if (newSlot >= 0) redirects[oldRegion] = regionByNewSlot[newSlot];
        }
        return redirects;
    }

    /// <summary>
    /// 一条有方向的地区关系，以对象引用而不是地区内容判断是否为同一对关系。
    /// </summary>
    private readonly struct RegionRelationPair : IEquatable<RegionRelationPair>
    {
        /// <summary>建立从来源地区指向目标地区的关系键。</summary>
        internal RegionRelationPair(GeoRegion source, GeoRegion target)
        {
            Source = source;
            Target = target;
        }

        /// <summary>需要在变化集合中标记的来源地区。</summary>
        internal GeoRegion Source { get; }

        /// <summary>用于区分关系的目标地区。</summary>
        private GeoRegion Target { get; }

        /// <summary>按来源和目标对象引用判断两条关系是否相同。</summary>
        public bool Equals(RegionRelationPair other)
        {
            return ReferenceEquals(Source, other.Source) && ReferenceEquals(Target, other.Target);
        }

        /// <summary>判断对象是否为引用完全相同的地区关系。</summary>
        public override bool Equals(object obj)
        {
            return obj is RegionRelationPair other && Equals(other);
        }

        /// <summary>根据两个地区对象的引用生成哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(Source) * 397) ^ RuntimeHelpers.GetHashCode(Target);
            }
        }
    }

    /// <summary>
    /// 一条有方向的“来源格子到目标格子”边，并包含所属地区层，用于防止相邻关系重复统计。
    /// </summary>
    private readonly struct TileLayerEdge : IEquatable<TileLayerEdge>
    {
        // 边的起点、终点和地区层共同组成唯一键。
        private readonly int sourceTileId;
        private readonly int targetTileId;
        private readonly GeoRegionLayer layer;

        /// <summary>建立一条指定地区层上的有向格子边。</summary>
        internal TileLayerEdge(int sourceTileId, int targetTileId, GeoRegionLayer layer)
        {
            this.sourceTileId = sourceTileId;
            this.targetTileId = targetTileId;
            this.layer = layer;
        }

        /// <summary>按起点、终点和地区层判断两条边是否相同。</summary>
        public bool Equals(TileLayerEdge other)
        {
            return sourceTileId == other.sourceTileId &&
                   targetTileId == other.targetTileId &&
                   layer == other.layer;
        }

        /// <summary>判断对象是否表示同一条有向格子边。</summary>
        public override bool Equals(object obj)
        {
            return obj is TileLayerEdge other && Equals(other);
        }

        /// <summary>根据起点、终点和地区层生成哈希值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = sourceTileId;
                hashCode = (hashCode * 397) ^ targetTileId;
                hashCode = (hashCode * 397) ^ (int)layer;
                return hashCode;
            }
        }
    }

    /// <summary>
    /// 一份从旧地区转给新地区的玩家自定义名称，以及该名称来自哪个旧地区位置。
    /// </summary>
    private readonly struct CustomNameTransfer
    {
        /// <summary>建立一份自定义名称转移记录。</summary>
        internal CustomNameTransfer(string name, int sourceOldSlot)
        {
            Name = name;
            SourceOldSlot = sourceOldSlot;
        }

        /// <summary>要保留的玩家自定义名称。</summary>
        internal string Name { get; }

        /// <summary>提供该名称的旧地区位置。</summary>
        internal int SourceOldSlot { get; }

        /// <summary>是否同时具有有效来源位置和非空名称。</summary>
        internal bool IsValid => SourceOldSlot >= 0 && !string.IsNullOrWhiteSpace(Name);
    }

    /// <summary>
    /// 旧地区对象更新前的重要可变字段副本，用于判断哪些方面发生变化，并在失败时恢复原状。
    /// </summary>
    private readonly struct GeoRegionMutableState
    {
        // 被更新的旧地区对象及其层级、分类、中心、面积和组成等计算字段。
        private readonly GeoRegion region;
        private readonly GeoRegionLayer layer;
        private readonly string categoryId;
        private readonly int centerX;
        private readonly int centerY;
        private readonly int tileCount;
        private readonly int coreTileCount;
        private readonly bool isMixed;
        private readonly bool topologyExempt;
        private readonly int dominantPrimaryCode;
        private readonly int dominantLandformCode;
        private readonly string coreBiomeId;
        private readonly string dominantBiomeId;
        private readonly int biomeCompositionCount;
        private readonly int rawCompositionCount;
        private readonly float purity;

        // 名称、颜色和旗帜等玩家可见字段。
        private readonly string name;
        private readonly bool customName;
        private readonly int colorId;
        private readonly int bannerBackgroundIndex;
        private readonly int bannerIconIndex;

        /// <summary>复制一个即将继续使用的旧地区对象的可变状态。</summary>
        internal GeoRegionMutableState(GeoRegion region)
        {
            this.region = region ?? throw new ArgumentNullException(nameof(region));
            GeoRegionData data = region.data ?? throw new InvalidOperationException("GeoRegion survivor 缺少 data");
            layer = data.Layer;
            categoryId = data.CategoryId;
            centerX = data.CenterX;
            centerY = data.CenterY;
            tileCount = data.TileCount;
            coreTileCount = data.CoreTileCount;
            isMixed = data.IsMixed;
            topologyExempt = data.TopologyExempt;
            dominantPrimaryCode = data.DominantPrimaryCode;
            dominantLandformCode = data.DominantLandformCode;
            coreBiomeId = data.CoreBiomeId;
            dominantBiomeId = data.DominantBiomeId;
            biomeCompositionCount = data.BiomeCompositionCount;
            rawCompositionCount = data.RawCompositionCount;
            purity = data.Purity;
            name = data.name;
            customName = data.custom_name;
            colorId = data.color_id;
            bannerBackgroundIndex = data.BannerBackgroundIndex;
            bannerIconIndex = data.BannerIconIndex;
        }

        /// <summary>
        /// 判断分类、生物群系、名称、颜色或旗帜等玩家可见内容是否改变。
        /// </summary>
        internal bool HasPresentationChanged()
        {
            GeoRegionData data = region.data;
            return !string.Equals(categoryId, data.CategoryId, StringComparison.Ordinal) ||
                   !string.Equals(coreBiomeId, data.CoreBiomeId, StringComparison.Ordinal) ||
                   !string.Equals(dominantBiomeId, data.DominantBiomeId, StringComparison.Ordinal) ||
                   !string.Equals(name, data.name, StringComparison.Ordinal) ||
                   customName != data.custom_name ||
                   colorId != data.color_id ||
                   bannerBackgroundIndex != data.BannerBackgroundIndex ||
                   bannerIconIndex != data.BannerIconIndex;
        }

        /// <summary>
        /// 判断层级、中心、面积或地区组成等影响地图形状和统计的内容是否改变。
        /// </summary>
        internal bool HasGeometryChanged()
        {
            GeoRegionData data = region.data;
            return layer != data.Layer ||
                   centerX != data.CenterX ||
                   centerY != data.CenterY ||
                   tileCount != data.TileCount ||
                   coreTileCount != data.CoreTileCount ||
                   isMixed != data.IsMixed ||
                   topologyExempt != data.TopologyExempt ||
                   dominantPrimaryCode != data.DominantPrimaryCode ||
                   dominantLandformCode != data.DominantLandformCode ||
                   biomeCompositionCount != data.BiomeCompositionCount ||
                   rawCompositionCount != data.RawCompositionCount ||
                   purity != data.Purity;
        }

        /// <summary>
        /// 把地区对象的所有已保存字段恢复到更新前状态。
        /// </summary>
        internal void Restore()
        {
            GeoRegionData data = region.data;
            data.Layer = layer;
            data.CategoryId = categoryId;
            data.CenterX = centerX;
            data.CenterY = centerY;
            data.TileCount = tileCount;
            data.CoreTileCount = coreTileCount;
            data.IsMixed = isMixed;
            data.TopologyExempt = topologyExempt;
            data.DominantPrimaryCode = dominantPrimaryCode;
            data.DominantLandformCode = dominantLandformCode;
            data.CoreBiomeId = coreBiomeId;
            data.DominantBiomeId = dominantBiomeId;
            data.BiomeCompositionCount = biomeCompositionCount;
            data.RawCompositionCount = rawCompositionCount;
            data.Purity = purity;
            data.name = name;
            data.custom_name = customName;
            data.setColorID(colorId);
            data.BannerBackgroundIndex = bannerBackgroundIndex;
            data.BannerIconIndex = bannerIconIndex;
        }
    }
}

/// <summary>
/// 新旧地区对应完成后的完整结果，包含下一版格子归属、退役对象、选中目标调整和刷新范围。
/// </summary>
internal sealed class GeoRegionReconciliationResult
{
    /// <summary>
    /// 汇总下一版地区归属和本次对象复用统计，供协调器一次性安装并刷新运行时状态。
    /// </summary>
    internal GeoRegionReconciliationResult(
        GeoRegionMembershipSnapshot membership,
        List<GeoRegion> retiredRegions,
        Dictionary<GeoRegion, GeoRegion> selectionRedirects,
        GeoRegionRuntimeChangeSet changeSet,
        int createdRegionCount,
        int survivorCount,
        int customNameTransferCount,
        int customNameConflictCount)
    {
        Membership = membership ?? throw new ArgumentNullException(nameof(membership));
        RetiredRegions = retiredRegions ?? throw new ArgumentNullException(nameof(retiredRegions));
        SelectionRedirects = selectionRedirects ?? throw new ArgumentNullException(nameof(selectionRedirects));
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
        CreatedRegionCount = createdRegionCount;
        SurvivorCount = survivorCount;
        CustomNameTransferCount = customNameTransferCount;
        CustomNameConflictCount = customNameConflictCount;
    }

    /// <summary>下一版完整的格子与地区对象对应关系。</summary>
    internal GeoRegionMembershipSnapshot Membership { get; }

    /// <summary>新版不再使用、需要延后移除的旧地区对象。</summary>
    internal List<GeoRegion> RetiredRegions { get; }

    /// <summary>旧选中地区退役后可改选的对应新地区。</summary>
    internal Dictionary<GeoRegion, GeoRegion> SelectionRedirects { get; }

    /// <summary>本次换版后需要刷新的地区、单位、形状、关系和地图格子。</summary>
    internal GeoRegionRuntimeChangeSet ChangeSet { get; }

    /// <summary>本次新建的地区对象数量。</summary>
    internal int CreatedRegionCount { get; }

    /// <summary>本次继续使用的旧地区对象数量。</summary>
    internal int SurvivorCount { get; }

    /// <summary>成功保留到新版地区的玩家自定义名称数量。</summary>
    internal int CustomNameTransferCount { get; }

    /// <summary>多个自定义名称竞争同一新版地区的次数。</summary>
    internal int CustomNameConflictCount { get; }
}
