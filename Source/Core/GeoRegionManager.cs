using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Core.GeoRegions;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

/// <summary>
/// 统一管理地理地区及地块归属关系，并提供地区查询、单位统计和关系查找。
/// 世界生成或局部重算完成后由它接收新版归属数据，游戏运行时的查询也从这里进入。
/// </summary>
public class GeoRegionManager : MetaSystemManager<GeoRegion, GeoRegionData>
{
    // 保护当前归属表及延迟删除队列，避免后台重算提交时与主线程读取互相干扰。
    private readonly object membershipGate = new();
    // 保存仍可能被旧读操作使用的归属表和待删除地区，确认无人读取后再释放。
    private readonly List<RetiredMembershipGeneration> retiredMemberships = new();
    // 保存因旧读操作仍在使用而暂缓删除的地区，供回收判断快速查询。
    private readonly HashSet<GeoRegion> leaseProtectedRetiredRegions = new();
    // 暂存跨层重叠、内部包含和相邻地区结果；地区相关变化编号改变时重新计算。
    private readonly Dictionary<long, RegionRelationCacheEntry> overlappingCache = new();
    private readonly Dictionary<long, RegionRelationCacheEntry> containedCache = new();
    private readonly Dictionary<AdjacencyCacheKey, RegionRelationCacheEntry> adjacencyCache = new();
    // 当前世界正在使用的完整地块归属表。
    private GeoRegionMembershipSnapshot membership;

    // 一次连续读操作固定使用的归属表，防止查询中途切换到新版数据。
    [ThreadStatic]
    private static GeoRegionMembershipSnapshot threadReadSnapshot;

    /// <summary>创建管理器并登记地区所使用的历史数据类型。</summary>
    public GeoRegionManager()
    {
        type_id = WorldboxGame.HistoryMetaDatas.GeoRegion.id;
    }

    /// <summary>清空世界时取消待处理重算，并释放归属数据、关系结果和轮廓图片。</summary>
    public override void clear()
    {
        GeoRegionRepartitionCoordinator.CancelPendingWork();
        ClearMembership();
        overlappingCache.Clear();
        containedCache.Clear();
        adjacencyCache.Clear();
        GeoRegionShapeSpriteCache.Clear();
        base.clear();
    }

    /// <summary>当前归属表属于正在运行的世界时为真，地区查询只有此时才可使用。</summary>
    internal bool IsMembershipReady
    {
        get
        {
            GeoRegionMembershipSnapshot current = GetQueryMembership();
            return current != null &&
                   World.world != null &&
                   current.Matches(World.world.tiles_list);
        }
    }

    /// <summary>当前归属表的变化编号；没有可用数据时返回 0。</summary>
    internal int MembershipRevision => Volatile.Read(ref membership)?.Revision ?? 0;

    /// <summary>取得指定版本的归属表供增量合并；数据已被替换时立即报错，避免混用新旧结果。</summary>
    internal GeoRegionMembershipSnapshot GetMembershipForReconciliation(int expectedRevision)
    {
        GeoRegionMembershipSnapshot current = Volatile.Read(ref membership);
        if (current == null || current.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                $"GeoRegion membership revision 已变化: expected={expectedRevision}, actual={current?.Revision ?? 0}");
        }

        return current;
    }

    /// <summary>首次建立世界地区时安装初始地块归属表，不允许覆盖已有数据。</summary>
    internal void InstallInitialMembership(GeoRegionMembershipSnapshot value)
    {
        ValidateMembershipWorld(value);
        lock (membershipGate)
        {
            if (membership != null)
            {
                throw new InvalidOperationException("GeoRegion 初始 membership 不能覆盖已有快照");
            }

            Volatile.Write(ref membership, value);
        }
    }

    /// <summary>
    /// 安装重算后的新版地块归属表，并暂时保留旧表涉及的已撤销地区，等旧读操作结束后再删除。
    /// </summary>
    internal void InstallReplacementMembership(
        GeoRegionMembershipSnapshot value,
        IReadOnlyList<GeoRegion> retiredRegions,
        int currentFrame)
    {
        ValidateMembershipWorld(value);
        lock (membershipGate)
        {
            GeoRegionMembershipSnapshot previous = membership ??
                throw new InvalidOperationException("GeoRegion 增量提交缺少上一版 membership");
            int expectedRevision = previous.Revision == int.MaxValue ? 1 : previous.Revision + 1;
            if (value.Revision != expectedRevision)
            {
                throw new InvalidOperationException(
                    $"GeoRegion membership 换代不连续: previous={previous.Revision}, next={value.Revision}");
            }

            List<GeoRegion> protectedRegions = new(retiredRegions?.Count ?? 0);
            if (retiredRegions != null)
            {
                for (int i = 0; i < retiredRegions.Count; i++)
                {
                    GeoRegion region = retiredRegions[i];
                    if (region == null || !leaseProtectedRetiredRegions.Add(region)) continue;
                    protectedRegions.Add(region);
                }
            }

            Volatile.Write(ref membership, value);
            if (protectedRegions.Count > 0)
            {
                retiredMemberships.Add(new RetiredMembershipGeneration(
                    previous,
                    protectedRegions,
                    currentFrame == int.MaxValue ? int.MaxValue : currentFrame + 1));
            }
        }
    }

    /// <summary>移除当前及待释放的所有地块归属数据，通常在世界清空时调用。</summary>
    internal void ClearMembership()
    {
        lock (membershipGate)
        {
            Volatile.Write(ref membership, null);
            retiredMemberships.Clear();
            leaseProtectedRetiredRegions.Clear();
            threadReadSnapshot = null;
        }
    }

    /// <summary>按地块编号和层级查找所属地区；归属数据尚未就绪时返回空。</summary>
    internal GeoRegion GetRegionForTile(int tileId, GeoRegionLayer layer)
    {
        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        return current?.GetRegion(tileId, layer);
    }

    /// <summary>取得某地块在指定层的分配变化编号，用于判断该格归属是否被重算过。</summary>
    internal int GetAssignmentStampForTile(int tileId, GeoRegionLayer layer)
    {
        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        return current?.GetAssignmentStamp(tileId, layer) ?? 0;
    }

    /// <summary>依次返回某个地块在各层所属的地区。</summary>
    internal IEnumerable<GeoRegion> EnumerateRegionsForTile(int tileId)
    {
        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        if (current == null) yield break;

        foreach (GeoRegion region in current.EnumerateRegions(tileId))
        {
            yield return region;
        }
    }

    /// <summary>检查指定地块在该地区所在层是否确实归属于它。</summary>
    internal bool TileHasRegion(int tileId, GeoRegion region)
    {
        if (region == null) return false;
        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        if (current == null) return false;

        GeoRegion assigned = current.GetRegion(tileId, region.data.Layer);
        return ReferenceEquals(assigned, region);
    }

    /// <summary>返回地区当前包含的地块数；归属数据不可用时返回 0。</summary>
    public int GetTileCount(GeoRegion region)
    {
        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        return current?.GetTileCount(region) ?? 0;
    }

    /// <summary>
    /// 开始一段需要一致数据的读取，并把当前归属表固定到本线程，使用后必须释放返回的对象。
    /// </summary>
    internal GeoRegionMembershipReadLease AcquireMembershipReadLease()
    {
        lock (membershipGate)
        {
            GeoRegionMembershipSnapshot current = membership;
            if (current == null || World.world == null || !current.Matches(World.world.tiles_list)) return null;

            GeoRegionMembershipSnapshot previous = threadReadSnapshot;
            var lease = new GeoRegionMembershipReadLease(this, current, previous);
            threadReadSnapshot = current;
            return lease;
        }
    }

    /// <summary>结束固定读取，恢复本线程先前使用的归属表，并登记少了一个读取者。</summary>
    internal void ReleaseReadLease(
        GeoRegionMembershipSnapshot snapshot,
        GeoRegionMembershipSnapshot previousSnapshot)
    {
        threadReadSnapshot = previousSnapshot;
        snapshot.RemoveReader();
    }

    /// <summary>每帧检查旧归属表；到达安全帧且无人读取后，删除其中已被新版撤销的地区。</summary>
    internal void ProcessRetiredMemberships(int currentFrame)
    {
        List<GeoRegion> readyToRetire = null;
        lock (membershipGate)
        {
            for (int i = retiredMemberships.Count - 1; i >= 0; i--)
            {
                RetiredMembershipGeneration generation = retiredMemberships[i];
                if (currentFrame < generation.RetireAfterFrame || generation.Snapshot.ReaderCount != 0) continue;

                readyToRetire ??= new List<GeoRegion>();
                for (int j = 0; j < generation.Regions.Count; j++)
                {
                    GeoRegion region = generation.Regions[j];
                    leaseProtectedRetiredRegions.Remove(region);
                    readyToRetire.Add(region);
                }
                retiredMemberships.RemoveAt(i);
            }
        }

        if (readyToRetire == null) return;
        for (int i = 0; i < readyToRetire.Count; i++)
        {
            GeoRegion region = readyToRetire[i];
            if (region == null || region.isRekt()) continue;
            removeObject(region);
        }
    }

    /// <summary>判断地区是否已不属于当前归属表，且没有旧读操作仍需访问它。</summary>
    internal bool CanRecycleRegion(GeoRegion region)
    {
        if (region == null) return false;
        lock (membershipGate)
        {
            if (leaseProtectedRetiredRegions.Contains(region)) return false;
            return membership == null || membership.GetTileCount(region) == 0;
        }
    }

    /// <summary>应用一次重算记录，按需刷新地区显示、单位统计、轮廓图片和关系查询结果。</summary>
    internal void ApplyRuntimeChangeSet(GeoRegionRuntimeChangeSet changeSet)
    {
        if (changeSet == null) throw new ArgumentNullException(nameof(changeSet));

        foreach (KeyValuePair<GeoRegion, GeoRegionRuntimeChangeKind> pair in changeSet.RegionChanges)
        {
            GeoRegion region = pair.Key;
            if (region == null || region.isRekt()) continue;
            region.ApplyRuntimeChanges(pair.Value, !changeSet.IsUnitDirty(region));
        }

        foreach (GeoRegion region in changeSet.UnitDirtyRegions)
        {
            if (region == null || region.isRekt()) continue;
            setDirtyUnits(region);
        }

        foreach (GeoRegion region in changeSet.ShapeDirtyRegions)
        {
            if (region == null) continue;
            GeoRegionShapeSpriteCache.Invalidate(region);
            if (GetTileCount(region) == 0)
            {
                overlappingCache.Remove(region.getID());
                containedCache.Remove(region.getID());
                RemoveAdjacencyCache(region.getID());
            }
        }
    }

    /// <summary>区域格子的城市归属等组成发生变化时，标记覆盖这些格子的地区需要更新统计。</summary>
    internal void NotifyZoneCompositionChanged(TileZone zone)
    {
        if (zone?.tiles == null || !IsMembershipReady) return;
        MarkCompositionChangedForTiles(zone.tiles);
    }

    /// <summary>城市组成发生变化时，找出城市所有格子涉及的地区并标记其统计需要更新。</summary>
    internal void NotifyCityCompositionChanged(City city)
    {
        if (city?.zones == null || !IsMembershipReady) return;

        var affectedRegions = new HashSet<GeoRegion>();
        for (int zoneIndex = 0; zoneIndex < city.zones.Count; zoneIndex++)
        {
            TileZone zone = city.zones[zoneIndex];
            if (zone?.tiles == null) continue;
            CollectRegionsForTiles(zone.tiles, affectedRegions);
        }
        MarkCompositionChanged(affectedRegions);
    }

    /// <summary>收集一组地块涉及的地区，并登记其组成内容已变化。</summary>
    private void MarkCompositionChangedForTiles(IEnumerable<WorldTile> tiles)
    {
        var affectedRegions = new HashSet<GeoRegion>();
        CollectRegionsForTiles(tiles, affectedRegions);
        MarkCompositionChanged(affectedRegions);
    }

    /// <summary>把一组地块在各层所属的有效地区加入结果集合，用集合自动去重。</summary>
    private void CollectRegionsForTiles(
        IEnumerable<WorldTile> tiles,
        HashSet<GeoRegion> affectedRegions)
    {
        foreach (WorldTile tile in tiles)
        {
            if (tile?.data == null) continue;
            foreach (GeoRegion region in EnumerateRegionsForTile(tile.data.tile_id))
            {
                if (region != null && !region.isRekt()) affectedRegions.Add(region);
            }
        }
    }

    /// <summary>逐个更新受影响地区的组成变化编号。</summary>
    private static void MarkCompositionChanged(HashSet<GeoRegion> affectedRegions)
    {
        foreach (GeoRegion region in affectedRegions)
        {
            region.ApplyRuntimeChanges(GeoRegionRuntimeChangeKind.Composition);
        }
    }

    /// <summary>
    /// 地区名称或颜色改变后，通知自身、相邻地区、跨层重叠地区和自定义地图模式刷新显示。
    /// </summary>
    internal void NotifyRegionPresentationChanged(GeoRegion region)
    {
        if (region == null || region.isRekt() || region.data == null) return;
        if (!CanQueryRegionTiles(region)) return;

        List<GeoRegion> adjacent = GetAdjacentRegions(region, region.data.Layer, int.MaxValue);
        List<GeoRegion> overlapping = GetOverlappingRegions(region, int.MaxValue);
        region.ApplyRuntimeChanges(GeoRegionRuntimeChangeKind.Presentation);

        for (int i = 0; i < adjacent.Count; i++)
        {
            GeoRegion source = adjacent[i];
            if (source != null && !source.isRekt())
            {
                source.ApplyRuntimeChanges(GeoRegionRuntimeChangeKind.Adjacency);
            }
        }
        for (int i = 0; i < overlapping.Count; i++)
        {
            GeoRegion source = overlapping[i];
            if (source != null && !source.isRekt())
            {
                source.ApplyRuntimeChanges(GeoRegionRuntimeChangeKind.CrossLayer);
            }
        }

        ModClass.I?.CustomMapModeManager?.OnGeoRegionPresentationChanged(region);
    }

    /// <summary>重新扫描存活单位，把单位登记到所有被标记为待更新的所属地区。</summary>
    public override void updateDirtyUnits()
    {
        if (!CanRefreshUnits()) return;

        List<Actor> units = World.world.units.units_only_alive;
        for (int i = 0; i < units.Count; i++)
        {
            Actor actor = units[i];
            WorldTile tile = actor.current_tile;
            if (tile == null) continue;

            foreach (GeoRegion geoRegion in tile.GetExtend().GetGeoRegions())
            {
                if (geoRegion.isRekt() || !geoRegion.isDirtyUnits()) continue;
                geoRegion.listUnit(actor);
            }
        }
    }

    /// <summary>某地块上的单位发生变化时，标记该格所在的全部地区重新统计单位。</summary>
    public void SetDirtyUnitsForTile(WorldTile tile)
    {
        if (!CanRefreshUnits() || tile == null) return;

        foreach (GeoRegion geoRegion in tile.GetExtend().GetGeoRegions())
        {
            if (geoRegion.isRekt()) continue;
            setDirtyUnits(geoRegion);
        }
    }

    /// <summary>单位移动时，只标记离开或进入的地区，未变化的共同地区不重复更新。</summary>
    public void SetDirtyUnitsForTileChange(WorldTile oldTile, WorldTile newTile)
    {
        if (oldTile == newTile) return;
        if (!CanRefreshUnits()) return;

        if (oldTile != null)
        {
            foreach (GeoRegion geoRegion in oldTile.GetExtend().GetGeoRegions())
            {
                if (geoRegion.isRekt() ||
                    (newTile != null && newTile.GetExtend().HasGeoRegion(geoRegion))) continue;
                setDirtyUnits(geoRegion);
            }
        }

        if (newTile != null)
        {
            foreach (GeoRegion geoRegion in newTile.GetExtend().GetGeoRegions())
            {
                if (geoRegion.isRekt() ||
                    (oldTile != null && oldTile.GetExtend().HasGeoRegion(geoRegion))) continue;
                setDirtyUnits(geoRegion);
            }
        }
    }

    /// <summary>按地图模式声明的层级顺序，返回地块上第一个可用地区。</summary>
    public GeoRegion ResolveGeoRegion(WorldTile tile, CustomMapModeAsset mapMode)
    {
        if (tile == null || mapMode == null) return null;
        if (!CanResolveTiles()) return null;

        GeoRegionLayer[] layers = mapMode.geo_region_layers;
        if (layers == null || layers.Length == 0) return null;

        TileExtend tileExtend = tile.GetExtend();
        for (int i = 0; i < layers.Length; i++)
        {
            GeoRegion geoRegion = tileExtend.GetGeoRegion(layers[i]);
            if (geoRegion != null && !geoRegion.isRekt()) return geoRegion;
        }

        return null;
    }

    /// <summary>取得地块在指定地图模式下应显示的地区。</summary>
    public GeoRegion GetGeoRegionForTile(WorldTile tile, CustomMapModeAsset mapMode)
    {
        return ResolveGeoRegion(tile, mapMode);
    }

    /// <summary>取得地块在默认主层所属的地区。</summary>
    public GeoRegion GetPrimaryGeoRegionForTile(WorldTile tile)
    {
        if (tile == null) return null;
        if (!CanResolveTiles()) return null;

        return tile.GetExtend().GetGeoRegion();
    }

    /// <summary>返回与目标地区共享地块的其他层地区，按共享地块数从多到少排列。</summary>
    public List<GeoRegion> GetOverlappingRegions(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        long regionId = region.getID();
        if (!overlappingCache.TryGetValue(regionId, out RegionRelationCacheEntry entry) ||
            entry.Revision != region.CrossLayerRevision)
        {
            entry = new RegionRelationCacheEntry(
                region.CrossLayerRevision,
                BuildOverlappingRegions(region));
            overlappingCache[regionId] = entry;
        }

        return CopyLimitedRegions(entry.Regions, maxCount);
    }

    /// <summary>遍历目标地区的地块，重新统计与它跨层重叠的地区及重叠次数。</summary>
    private List<GeoRegion> BuildOverlappingRegions(GeoRegion region)
    {
        Dictionary<GeoRegion, int> counters = new();
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            foreach (GeoRegion other in tile.GetExtend().GetGeoRegions())
            {
                CountRelatedRegion(counters, region, other);
            }
        }

        return SortRegionCounters(counters);
    }

    /// <summary>返回目标地区范围内较细层级的地区，按共同地块数从多到少排列。</summary>
    public List<GeoRegion> GetContainedRegions(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        long regionId = region.getID();
        if (!containedCache.TryGetValue(regionId, out RegionRelationCacheEntry entry) ||
            entry.Revision != region.CrossLayerRevision)
        {
            entry = new RegionRelationCacheEntry(
                region.CrossLayerRevision,
                BuildContainedRegions(region));
            containedCache[regionId] = entry;
        }

        return CopyLimitedRegions(entry.Regions, maxCount);
    }

    /// <summary>按照层级包含关系，重新统计目标地区范围内的其他地区。</summary>
    private List<GeoRegion> BuildContainedRegions(GeoRegion region)
    {
        Dictionary<GeoRegion, int> counters = new();
        GeoRegionLayer[] layers = GetContainedLayerCandidates(region.data.Layer);
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            for (int i = 0; i < layers.Length; i++)
            {
                CountRelatedRegion(counters, region, tile.GetExtend().GetGeoRegion(layers[i]));
            }
        }

        return SortRegionCounters(counters);
    }

    /// <summary>返回与目标地区边界相接的指定层地区，按接触次数从多到少排列。</summary>
    public List<GeoRegion> GetAdjacentRegions(GeoRegion region, GeoRegionLayer? layer = null, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        GeoRegionLayer targetLayer = layer ?? region.data.Layer;
        var key = new AdjacencyCacheKey(region.getID(), targetLayer);
        if (!adjacencyCache.TryGetValue(key, out RegionRelationCacheEntry entry) ||
            entry.Revision != region.AdjacencyRevision)
        {
            entry = new RegionRelationCacheEntry(
                region.AdjacencyRevision,
                BuildAdjacentRegions(region, targetLayer));
            adjacencyCache[key] = entry;
        }

        return CopyLimitedRegions(entry.Regions, maxCount);
    }

    /// <summary>检查目标地区每个地块的邻格，重新统计边界相接的地区。</summary>
    private List<GeoRegion> BuildAdjacentRegions(GeoRegion region, GeoRegionLayer targetLayer)
    {
        Dictionary<GeoRegion, int> counters = new();
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            WorldTile[] neighbors = tile.neighbours;
            if (neighbors == null) continue;

            for (int i = 0; i < neighbors.Length; i++)
            {
                WorldTile neighbor = neighbors[i];
                if (neighbor == null) continue;

                CountRelatedRegion(counters, region, neighbor.GetExtend().GetGeoRegion(targetLayer));
            }
        }

        return SortRegionCounters(counters);
    }

    /// <summary>返回地区内出现的城市，优先列出拥有区域格较多的城市。</summary>
    public List<City> GetCitiesInRegion(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<City>();

        HashSet<City> cities = new();
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            City city = tile?.zone?.city;
            if (city == null || city.isRekt()) continue;
            cities.Add(city);
        }

        return cities
            .OrderByDescending(city => city.zones.Count)
            .ThenBy(city => city.name)
            .ThenBy(city => city.getID())
            .Take(maxCount)
            .ToList();
    }

    /// <summary>返回地区内占地的非中立王国，优先列出覆盖地块较多的王国。</summary>
    public List<Kingdom> GetKingdomsInRegion(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<Kingdom>();

        Dictionary<Kingdom, int> counters = new();
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            Kingdom kingdom = tile?.zone?.city?.kingdom;
            if (kingdom == null || kingdom.isRekt() || kingdom.isNeutral()) continue;

            counters.TryGetValue(kingdom, out int count);
            counters[kingdom] = count + 1;
        }

        if (counters.Count == 0) return new List<Kingdom>();

        return counters
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.countCities())
            .ThenBy(pair => pair.Key.name)
            .Take(Math.Max(1, maxCount))
            .Select(pair => pair.Key)
            .ToList();
    }

    /// <summary>确认另一个地区有效且不是自身后，累计一次两者的空间关系。</summary>
    private static void CountRelatedRegion(Dictionary<GeoRegion, int> counters, GeoRegion source, GeoRegion other)
    {
        if (other == null || other.isRekt() || ReferenceEquals(source, other)) return;

        counters.TryGetValue(other, out int count);
        counters[other] = count + 1;
    }

    /// <summary>按共同地块或接触次数排序，并用名称、层级和编号保证结果顺序稳定。</summary>
    private static List<GeoRegion> SortRegionCounters(Dictionary<GeoRegion, int> counters)
    {
        if (counters.Count == 0) return new List<GeoRegion>();

        return counters
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.name)
            .ThenBy(pair => pair.Key.data?.Layer ?? GeoRegionLayer.Primary)
            .ThenBy(pair => pair.Key.getID())
            .Select(pair => pair.Key)
            .ToList();
    }

    /// <summary>从完整关系结果中复制调用方需要的前若干项，避免外部修改内部列表。</summary>
    private static List<GeoRegion> CopyLimitedRegions(IReadOnlyList<GeoRegion> regions, int maxCount)
    {
        int count = Math.Min(regions.Count, Math.Max(1, maxCount));
        var result = new List<GeoRegion>(count);
        for (int i = 0; i < count; i++) result.Add(regions[i]);
        return result;
    }

    /// <summary>给出某层查询“内部地区”时需要检查的其他层。</summary>
    private static GeoRegionLayer[] GetContainedLayerCandidates(GeoRegionLayer layer)
    {
        return layer switch
        {
            GeoRegionLayer.Landmass => new[]
            {
                GeoRegionLayer.Landform,
                GeoRegionLayer.Primary,
                GeoRegionLayer.Peninsula,
                GeoRegionLayer.Strait,
                GeoRegionLayer.Archipelago
            },
            GeoRegionLayer.Landform => new[]
            {
                GeoRegionLayer.Primary,
                GeoRegionLayer.Peninsula,
                GeoRegionLayer.Strait,
                GeoRegionLayer.Archipelago
            },
            GeoRegionLayer.Primary => new[]
            {
                GeoRegionLayer.Peninsula,
                GeoRegionLayer.Strait,
                GeoRegionLayer.Archipelago
            },
            GeoRegionLayer.Peninsula or GeoRegionLayer.Strait or GeoRegionLayer.Archipelago => new[]
            {
                GeoRegionLayer.Primary,
                GeoRegionLayer.Landform,
                GeoRegionLayer.Landmass
            },
            _ => throw new InvalidOperationException($"未知 GeoRegionLayer: {layer}")
        };
    }

    /// <summary>判断当前世界、地块扩展和归属数据是否已准备好，可否查询地区地块。</summary>
    public bool CanResolveRegionTiles()
    {
        return CanResolveTiles();
    }

    /// <summary>依次返回地区当前包含的有效地块。</summary>
    public IEnumerable<WorldTile> EnumerateRegionTiles(GeoRegion region)
    {
        if (!CanQueryRegionTiles(region)) yield break;

        GeoRegionMembershipSnapshot current = GetReadyQueryMembership();
        if (current == null) yield break;
        IReadOnlyList<int> tileIds = current.GetTileIds(region);
        WorldTile[] tiles = World.world.tiles_list;
        for (int i = 0; i < tileIds.Count; i++)
        {
            int tileId = tileIds[i];
            if ((uint)tileId >= (uint)tiles.Length) continue;
            WorldTile tile = tiles[tileId];
            if (tile != null) yield return tile;
        }
    }

    /// <summary>验证地区对象和绑定实体完整，并判断当前归属数据能否用于地块查询。</summary>
    private bool CanQueryRegionTiles(GeoRegion region)
    {
        if (region == null) throw new InvalidOperationException("GeoRegion 为空");
        if (region.data == null) throw new InvalidOperationException($"GeoRegion 数据为空: id={region.getID()}");
        if (region.E.IsNull) throw new InvalidOperationException($"GeoRegion Entity 为空: id={region.getID()}, name={region.name}");

        return CanResolveTiles();
    }

    /// <summary>判断地区归属和地块扩展是否已就绪，可否重新收集单位。</summary>
    private bool CanRefreshUnits()
    {
        return CanResolveTiles();
    }

    /// <summary>集中检查所有依赖项，避免世界加载或清空期间读取半成品数据。</summary>
    private bool CanResolveTiles()
    {
        return ModClass.I?.TileExtendManager != null &&
               ModClass.I.TileExtendManager.Ready() &&
               IsMembershipReady;
    }

    /// <summary>把地区加入管理器前先创建它对应的实体。</summary>
    public override void addObject(GeoRegion pObject)
    {
        pObject.BaseSetup();
        base.addObject(pObject);
    }

    /// <summary>创建、登记并完成一个新地区的通用初始化。</summary>
    public GeoRegion BuildGeoRegion()
    {
        var geoRegion = newObject();
        geoRegion.Setup();

        return geoRegion;
    }

    /// <summary>优先返回当前线程固定使用的归属表，否则读取全局最新版本。</summary>
    private GeoRegionMembershipSnapshot GetQueryMembership()
    {
        return threadReadSnapshot ?? Volatile.Read(ref membership);
    }

    /// <summary>仅当归属表确实属于当前世界时返回它，否则返回空。</summary>
    private GeoRegionMembershipSnapshot GetReadyQueryMembership()
    {
        GeoRegionMembershipSnapshot current = GetQueryMembership();
        return current != null && World.world != null && current.Matches(World.world.tiles_list)
            ? current
            : null;
    }

    /// <summary>安装归属表前确认它不为空，并且引用当前世界的地块数组。</summary>
    private static void ValidateMembershipWorld(GeoRegionMembershipSnapshot value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (World.world == null || !value.Matches(World.world.tiles_list))
        {
            throw new InvalidOperationException("不能安装不属于当前世界的 GeoRegion membership");
        }
    }

    /// <summary>删除某地区在所有目标层上的相邻关系暂存结果。</summary>
    private void RemoveAdjacencyCache(long regionId)
    {
        List<AdjacencyCacheKey> keysToRemove = null;
        foreach (AdjacencyCacheKey key in adjacencyCache.Keys)
        {
            if (key.RegionId != regionId) continue;
            keysToRemove ??= new List<AdjacencyCacheKey>();
            keysToRemove.Add(key);
        }

        if (keysToRemove == null) return;
        for (int i = 0; i < keysToRemove.Count; i++) adjacencyCache.Remove(keysToRemove[i]);
    }

    /// <summary>保存一次地区关系查询的完整排序结果，以及生成它时使用的变化编号。</summary>
    private sealed class RegionRelationCacheEntry
    {
        /// <summary>保存生成结果时的变化编号和已经排好序的地区列表。</summary>
        internal RegionRelationCacheEntry(int revision, List<GeoRegion> regions)
        {
            Revision = revision;
            Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        }

        /// <summary>生成这项结果时使用的地区变化编号。</summary>
        internal int Revision { get; }
        /// <summary>已经按关系强弱排好序的地区。</summary>
        internal List<GeoRegion> Regions { get; }
    }

    /// <summary>用地区编号和待查层级共同标识一项相邻关系结果。</summary>
    private readonly struct AdjacencyCacheKey : IEquatable<AdjacencyCacheKey>
    {
        /// <summary>用地区编号和待查层级建立唯一标识。</summary>
        internal AdjacencyCacheKey(long regionId, GeoRegionLayer layer)
        {
            RegionId = regionId;
            Layer = layer;
        }

        /// <summary>需要查询相邻关系的地区编号。</summary>
        internal long RegionId { get; }
        /// <summary>需要在其中查找相邻地区的层级。</summary>
        private GeoRegionLayer Layer { get; }

        /// <summary>比较地区编号和层级是否都相同。</summary>
        public bool Equals(AdjacencyCacheKey other)
        {
            return RegionId == other.RegionId && Layer == other.Layer;
        }

        /// <summary>检查另一个对象是否是相同的相邻关系标识。</summary>
        public override bool Equals(object obj)
        {
            return obj is AdjacencyCacheKey other && Equals(other);
        }

        /// <summary>根据地区编号和层级生成用于字典查找的数值。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (RegionId.GetHashCode() * 397) ^ (int)Layer;
            }
        }
    }

    /// <summary>保存已被新版替换但仍可能有人读取的归属表、待删地区和最早删除帧。</summary>
    private sealed class RetiredMembershipGeneration
    {
        /// <summary>记录一份旧归属表、受它保护的待删地区，以及最早可删除的帧。</summary>
        internal RetiredMembershipGeneration(
            GeoRegionMembershipSnapshot snapshot,
            List<GeoRegion> regions,
            int retireAfterFrame)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Regions = regions ?? throw new ArgumentNullException(nameof(regions));
            RetireAfterFrame = retireAfterFrame;
        }

        /// <summary>仍可能被旧读操作访问的地块归属表。</summary>
        internal GeoRegionMembershipSnapshot Snapshot { get; }
        /// <summary>归属表释放后可以删除的地区。</summary>
        internal List<GeoRegion> Regions { get; }
        /// <summary>即使无人读取，也必须等到这一帧后才能删除。</summary>
        internal int RetireAfterFrame { get; }
    }
}
