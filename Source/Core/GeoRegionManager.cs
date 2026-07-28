using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cultiway.Core.Libraries;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class GeoRegionManager : MetaSystemManager<GeoRegion, GeoRegionData>
{
    private GeoRegionMembershipIndex membership;
    private readonly ConcurrentQueue<UnitMembershipChange>
        pendingUnitChanges = new();
    private readonly Dictionary<GeoRegion, Dictionary<Actor, int>>
        unitPositions = new();
    private readonly HashSet<GeoRegion> changedUnitRegions = new();
    private bool unitPositionsValid;
    private int pendingUnitChangeCount;
    private long unitChangesEnqueued;
    private long unitChangesApplied;
    private long unitChangesDiscarded;
    private long unitMembershipAdds;
    private long unitMembershipRemoves;

    public GeoRegionManager()
    {
        type_id = WorldboxGame.HistoryMetaDatas.GeoRegion.id;
    }

    public override void clear()
    {
        ClearMembership();
        ClearPendingUnitChanges();
        unitPositions.Clear();
        changedUnitRegions.Clear();
        unitPositionsValid = false;
        GeoRegionShapeSpriteCache.Clear();
        base.clear();
    }

    internal bool IsMembershipReady =>
        membership != null &&
        World.world != null &&
        membership.Matches(World.world.tiles_list);

    internal void InstallMembership(GeoRegionMembershipIndex value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (World.world == null || !value.Matches(World.world.tiles_list))
        {
            throw new InvalidOperationException("不能安装不属于当前世界的 GeoRegion 索引");
        }

        membership = value;
    }

    internal void ClearMembership()
    {
        membership = null;
        ClearPendingUnitChanges();
        unitPositions.Clear();
        changedUnitRegions.Clear();
        unitPositionsValid = false;
    }

    internal GeoRegion GetRegionForTile(int tileId, GeoRegionLayer layer)
    {
        return IsMembershipReady ? membership.GetRegion(tileId, layer) : null;
    }

    internal IEnumerable<GeoRegion> EnumerateRegionsForTile(int tileId)
    {
        if (!IsMembershipReady) yield break;

        foreach (GeoRegion region in membership.EnumerateRegions(tileId))
        {
            yield return region;
        }
    }

    internal bool TileHasRegion(int tileId, GeoRegion region)
    {
        if (!IsMembershipReady || region == null) return false;

        GeoRegion current = membership.GetRegion(tileId, region.data.Layer);
        return ReferenceEquals(current, region);
    }

    public bool AssignTileToRegion(WorldTile tile, GeoRegionLayer layer, GeoRegion region)
    {
        if (!IsMembershipReady || tile == null || region == null) return false;
        return membership.AssignTile(tile.data.tile_id, layer, region);
    }

    public bool RemoveTileFromRegion(WorldTile tile, GeoRegionLayer layer)
    {
        if (!IsMembershipReady || tile == null) return false;
        return membership.RemoveTile(tile.data.tile_id, layer);
    }

    public int GetTileCount(GeoRegion region)
    {
        return IsMembershipReady ? membership.GetTileCount(region) : 0;
    }

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

    public override void finishDirtyUnits()
    {
        base.finishDirtyUnits();
        RebuildUnitPositions();
    }

    public void SetDirtyUnitsForTile(WorldTile tile)
    {
        if (!CanRefreshUnits() || tile == null) return;

        foreach (GeoRegion geoRegion in tile.GetExtend().GetGeoRegions())
        {
            if (geoRegion.isRekt()) continue;
            setDirtyUnits(geoRegion);
        }
    }

    /// <summary>
    /// 记录角色跨 GeoRegion 的成员变化。模拟阶段只写入并发队列，
    /// 下一 tick 的维护阶段再串行提交，时间边界与原版脏重建一致。
    /// </summary>
    public void NotifyUnitTileChange(
        Actor actor,
        WorldTile oldTile,
        WorldTile newTile)
    {
        if (actor == null || oldTile == newTile) return;
        if (!CanRefreshUnits()) return;
        if (HaveSameUnitMembership(oldTile, newTile)) return;

        pendingUnitChanges.Enqueue(
            new UnitMembershipChange(
                actor,
                oldTile,
                newTile));
        Interlocked.Increment(
            ref pendingUnitChangeCount);
        Interlocked.Increment(
            ref unitChangesEnqueued);
    }

    public void NotifyUnitRemoved(
        Actor actor,
        WorldTile tile)
    {
        NotifyUnitTileChange(
            actor,
            tile,
            null);
    }

    internal void ApplyPendingUnitChanges()
    {
        if (Volatile.Read(
                ref pendingUnitChangeCount) == 0)
        {
            return;
        }

        if (!CanRefreshUnits() ||
            isUnitsDirty())
        {
            long discarded =
                DrainPendingUnitChanges();
            Interlocked.Add(
                ref unitChangesDiscarded,
                discarded);
            unitPositionsValid = false;
            return;
        }

        EnsureUnitPositions();
        changedUnitRegions.Clear();
        long applied = 0L;
        long adds = 0L;
        long removes = 0L;
        while (pendingUnitChanges.TryDequeue(
                   out UnitMembershipChange change))
        {
            Interlocked.Decrement(
                ref pendingUnitChangeCount);
            ApplyUnitMembershipChange(
                in change,
                ref adds,
                ref removes);
            applied++;
        }

        foreach (GeoRegion region in changedUnitRegions)
        {
            region.unDirty();
        }

        changedUnitRegions.Clear();
        Interlocked.Add(
            ref unitChangesApplied,
            applied);
        Interlocked.Add(
            ref unitMembershipAdds,
            adds);
        Interlocked.Add(
            ref unitMembershipRemoves,
            removes);
    }

    internal string GetUnitMembershipDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "pending={0} changes={1}/{2}/{3}(queued/applied/discarded) " +
            "members={4}/{5}(add/remove) indexed_regions={6}",
            Volatile.Read(ref pendingUnitChangeCount),
            Interlocked.Read(ref unitChangesEnqueued),
            Interlocked.Read(ref unitChangesApplied),
            Interlocked.Read(ref unitChangesDiscarded),
            Interlocked.Read(ref unitMembershipAdds),
            Interlocked.Read(ref unitMembershipRemoves),
            unitPositions.Count);
    }

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

    public GeoRegion GetGeoRegionForTile(WorldTile tile, CustomMapModeAsset mapMode)
    {
        GeoRegion geoRegion = ResolveGeoRegion(tile, mapMode);
        if (geoRegion != null) return geoRegion;

        return GetPrimaryGeoRegionForTile(tile);
    }

    public GeoRegion GetPrimaryGeoRegionForTile(WorldTile tile)
    {
        if (tile == null) return null;
        if (!CanResolveTiles()) return null;

        return tile.GetExtend().GetGeoRegion();
    }

    public GeoRegionAsset ResolveCategory(in GeoRegionGeneratedEvent evt)
    {
        return ResolveCategory(
            evt.Layer,
            evt.BaseLayerType,
            evt.WaterKind,
            evt.TouchesEdge,
            evt.BiomeDominantCategoryId,
            evt.LandformDominantCategoryId);
    }

    public GeoRegionAsset ResolveCategory(
        GeoRegionLayer layer,
        TileLayerType baseLayerType,
        PrimaryWaterKind waterKind,
        bool touchesEdge,
        string biomeDominantCategoryId,
        string landformDominantCategoryId)
    {
        var lib = ModClass.L?.GeoRegionLibrary ?? throw new InvalidOperationException("GeoRegionLibrary 尚未初始化");

        switch (layer)
        {
            case GeoRegionLayer.Primary:
            {
                if (waterKind != PrimaryWaterKind.None)
                {
                    return lib.ResolvePrimaryWater(waterKind);
                }

                if (baseLayerType == TileLayerType.Ocean)
                {
                    return lib.ResolvePrimaryWater(touchesEdge ? PrimaryWaterKind.Sea : PrimaryWaterKind.Lake);
                }

                if (baseLayerType is TileLayerType.Lava or TileLayerType.Goo or TileLayerType.Block)
                {
                    return lib.ResolvePrimarySpecial(baseLayerType);
                }

                if (!string.IsNullOrEmpty(biomeDominantCategoryId))
                {
                    var cat = lib.get(biomeDominantCategoryId);
                    if (cat != null) return cat;
                    throw new InvalidOperationException($"无效的 Primary 分类 id: {biomeDominantCategoryId}");
                }

                return lib.PrimarySpecial;
            }
            case GeoRegionLayer.Landform:
            {
                if (!string.IsNullOrEmpty(landformDominantCategoryId))
                {
                    var cat = lib.get(landformDominantCategoryId);
                    if (cat != null) return cat;
                    throw new InvalidOperationException($"无效的 Landform 分类 id: {landformDominantCategoryId}");
                }

                return lib.LandformPlain;
            }
            case GeoRegionLayer.Landmass:
                return lib.ResolveLandmass(touchesEdge);
            case GeoRegionLayer.Peninsula:
                return lib.Peninsula;
            case GeoRegionLayer.Strait:
                return lib.Strait;
            case GeoRegionLayer.Archipelago:
                return lib.Archipelago;
            default:
                throw new InvalidOperationException($"未知 GeoRegionLayer: {layer}");
        }
    }

    public GeoRegionAsset InitializePrimaryRegionFromTile(GeoRegion region, WorldTile tile)
    {
        if (region == null) throw new InvalidOperationException("GeoRegion 为空");
        if (region.data == null) throw new InvalidOperationException($"GeoRegion 数据为空: id={region.getID()}");

        var category = ResolvePrimaryCategoryForTile(tile);
        region.data.Layer = GeoRegionLayer.Primary;
        if (string.IsNullOrEmpty(category?.id))
        {
            throw new InvalidOperationException($"无法从 tile 初始化 GeoRegion 分类: region={region.getID()}, tile={tile?.data.tile_id.ToString() ?? "null"}");
        }

        region.data.CategoryId = category.id;
        region.data.CenterX = tile.x;
        region.data.CenterY = tile.y;

        RefreshTileCount(region);
        region.data.name = category.GenerateName();
        region.data.custom_name = false;
        return category;
    }

    public void RefreshTileCount(GeoRegion region)
    {
        if (region?.data == null || region.E.IsNull) return;
        region.data.TileCount = GetTileCount(region);
    }

    public List<GeoRegion> GetOverlappingRegions(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        Dictionary<GeoRegion, int> counters = new();
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            foreach (GeoRegion other in tile.GetExtend().GetGeoRegions())
            {
                CountRelatedRegion(counters, region, other);
            }
        }

        return SortRegionCounters(counters, maxCount);
    }

    public List<GeoRegion> GetContainedRegions(GeoRegion region, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        Dictionary<GeoRegion, int> counters = new();
        GeoRegionLayer[] layers = GetContainedLayerCandidates(region.data.Layer);
        foreach (WorldTile tile in EnumerateRegionTiles(region))
        {
            for (int i = 0; i < layers.Length; i++)
            {
                CountRelatedRegion(counters, region, tile.GetExtend().GetGeoRegion(layers[i]));
            }
        }

        return SortRegionCounters(counters, maxCount);
    }

    public List<GeoRegion> GetAdjacentRegions(GeoRegion region, GeoRegionLayer? layer = null, int maxCount = 8)
    {
        if (!CanQueryRegionTiles(region)) return new List<GeoRegion>();

        Dictionary<GeoRegion, int> counters = new();
        GeoRegionLayer targetLayer = layer ?? region.data.Layer;
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

        return SortRegionCounters(counters, maxCount);
    }

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
            .Take(maxCount)
            .ToList();
    }

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

    private static void CountRelatedRegion(Dictionary<GeoRegion, int> counters, GeoRegion source, GeoRegion other)
    {
        if (other == null || other.isRekt() || ReferenceEquals(source, other)) return;

        counters.TryGetValue(other, out int count);
        counters[other] = count + 1;
    }

    private static List<GeoRegion> SortRegionCounters(Dictionary<GeoRegion, int> counters, int maxCount)
    {
        if (counters.Count == 0) return new List<GeoRegion>();

        return counters
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.data?.TileCount ?? 0)
            .ThenBy(pair => pair.Key.name)
            .Take(Math.Max(1, maxCount))
            .Select(pair => pair.Key)
            .ToList();
    }

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

    public bool CanResolveRegionTiles()
    {
        return CanResolveTiles();
    }

    public IEnumerable<WorldTile> EnumerateRegionTiles(GeoRegion region)
    {
        if (!CanQueryRegionTiles(region)) yield break;

        IReadOnlyList<int> tileIds = membership.GetTileIds(region);
        WorldTile[] tiles = World.world.tiles_list;
        for (int i = 0; i < tileIds.Count; i++)
        {
            int tileId = tileIds[i];
            if ((uint)tileId >= (uint)tiles.Length) continue;
            WorldTile tile = tiles[tileId];
            if (tile != null) yield return tile;
        }
    }

    private bool CanQueryRegionTiles(GeoRegion region)
    {
        if (region == null) throw new InvalidOperationException("GeoRegion 为空");
        if (region.data == null) throw new InvalidOperationException($"GeoRegion 数据为空: id={region.getID()}");
        if (region.E.IsNull) throw new InvalidOperationException($"GeoRegion Entity 为空: id={region.getID()}, name={region.name}");

        return CanResolveTiles();
    }

    public GeoRegionAsset ResolvePrimaryCategoryForTile(WorldTile tile)
    {
        var lib = ModClass.L?.GeoRegionLibrary ?? throw new InvalidOperationException("GeoRegionLibrary 尚未初始化");
        if (tile == null) throw new InvalidOperationException("无法从空 tile 解析 GeoRegion 分类");
        if (tile.Type == null) throw new InvalidOperationException($"无法从 Type 为空的 tile 解析 GeoRegion 分类: tile={tile.data.tile_id}");

        var tileType = tile.Type;
        var layerType = tileType.layer_type;
        var isLava = layerType == TileLayerType.Lava || tileType.lava;
        var isGoo = layerType == TileLayerType.Goo || tileType.grey_goo;
        var isWater = IsWaterTile(tileType);
        var isBlock = IsBlockTile(tileType);

        if (isLava || isGoo || isBlock)
        {
            return lib.ResolvePrimarySpecial(layerType, isLava, isGoo, isBlock);
        }

        if (isWater)
        {
            return lib.ResolvePrimaryWater(TouchesMapEdge(tile) ? PrimaryWaterKind.Sea : PrimaryWaterKind.Lake);
        }

        if (layerType != TileLayerType.Ground)
        {
            return lib.PrimarySpecial;
        }

        var context = BuildTileRuleContext(tile);
        return lib.ResolvePrimaryLandByContext(context) ??
               throw new InvalidOperationException($"无法解析 tile 的 Primary 分类: tile={tile.data.tile_id}, type={tileType.id}, biome={tile.getBiome()?.id ?? "null"}");
    }

    private static GeoRegionTileRuleContext BuildTileRuleContext(WorldTile tile)
    {
        var tileType = tile.Type;
        var layerType = tileType.layer_type;
        var biomeId = tile.getBiome()?.id;

        var neighborWaterCount = 0;
        var neighborWater8Count = 0;
        var neighborBlockCount = 0;
        var neighborPitCount = 0;

        CountNeighborStats(tile.neighbours, true, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);
        if (tile.neighboursAll != null)
        {
            CountNeighborStats(tile.neighboursAll, false, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);
        }
        else
        {
            neighborWater8Count = neighborWaterCount;
        }

        var leftBlock = IsBlockAt(tile.x - 1, tile.y);
        var rightBlock = IsBlockAt(tile.x + 1, tile.y);
        var downBlock = IsBlockAt(tile.x, tile.y - 1);
        var upBlock = IsBlockAt(tile.x, tile.y + 1);
        var hasOppositeBlockPair = (leftBlock && rightBlock) || (downBlock && upBlock);
        var distanceToWater = IsBeachMaterialTile(tileType, biomeId) && neighborWater8Count > 0 ? 0 : -1;

        return new GeoRegionTileRuleContext(
            tileType.id,
            layerType,
            biomeId,
            tileType.ocean,
            tileType.can_be_filled_with_ocean,
            layerType == TileLayerType.Lava || tileType.lava,
            layerType == TileLayerType.Goo || tileType.grey_goo,
            IsBlockTile(tileType),
            neighborWaterCount,
            neighborWater8Count,
            distanceToWater,
            neighborBlockCount,
            neighborPitCount,
            hasOppositeBlockPair);
    }

    private static void CountNeighborStats(
        WorldTile[] neighbors,
        bool cardinal,
        ref int neighborWaterCount,
        ref int neighborWater8Count,
        ref int neighborBlockCount,
        ref int neighborPitCount)
    {
        if (neighbors == null) return;

        for (var i = 0; i < neighbors.Length; i++)
        {
            var neighbor = neighbors[i];
            var tileType = neighbor?.Type;
            if (tileType == null) continue;

            if (IsWaterTile(tileType))
            {
                if (cardinal)
                {
                    neighborWaterCount++;
                }
                else
                {
                    neighborWater8Count++;
                }
            }

            if (!cardinal) continue;

            if (IsBlockTile(tileType))
            {
                neighborBlockCount++;
            }

            if (tileType.can_be_filled_with_ocean)
            {
                neighborPitCount++;
            }
        }
    }

    private static bool TouchesMapEdge(WorldTile tile)
    {
        return tile.x <= 0 || tile.y <= 0 || tile.x >= MapBox.width - 1 || tile.y >= MapBox.height - 1;
    }

    private static bool IsBlockAt(int x, int y)
    {
        var tile = World.world?.GetTileSimple(x, y);
        return IsBlockTile(tile?.Type);
    }

    private static bool IsWaterTile(TileTypeBase tileType)
    {
        if (tileType == null) return false;
        var layerType = tileType.layer_type;
        var isLava = layerType == TileLayerType.Lava || tileType.lava;
        var isGoo = layerType == TileLayerType.Goo || tileType.grey_goo;
        return (layerType == TileLayerType.Ocean || tileType.ocean) && !isLava && !isGoo;
    }

    private static bool IsBlockTile(TileTypeBase tileType)
    {
        if (tileType == null) return false;
        return tileType.layer_type == TileLayerType.Block || tileType.block || tileType.mountains || tileType.edge_mountains;
    }

    private static bool IsBeachMaterialTile(TileTypeBase tileType, string biomeId)
    {
        if (tileType == null) return false;
        if (tileType.sand) return true;

        var tileTypeId = tileType.id;
        if (string.Equals(tileTypeId, "sand", StringComparison.Ordinal) ||
            string.Equals(tileTypeId, "snow_sand", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(biomeId, "biome_sand", StringComparison.Ordinal);
    }

    private bool CanRefreshUnits()
    {
        return CanResolveTiles();
    }

    private bool CanResolveTiles()
    {
        return ModClass.I?.TileExtendManager != null &&
               ModClass.I.TileExtendManager.Ready() &&
               IsMembershipReady;
    }

    public override void addObject(GeoRegion pObject)
    {
        pObject.BaseSetup();
        base.addObject(pObject);
    }

    public GeoRegion BuildGeoRegion(Actor founder)
    {
        var geoRegion = newObject();
        geoRegion.Setup(founder);

        return geoRegion;
    }

    private bool HaveSameUnitMembership(
        WorldTile oldTile,
        WorldTile newTile)
    {
        int oldTileId =
            oldTile?.tile_id ?? -1;
        int newTileId =
            newTile?.tile_id ?? -1;
        for (int layer = 0;
             layer < GeoRegionMembershipIndex.LayerCount;
             layer++)
        {
            GeoRegion oldRegion = oldTileId >= 0
                ? membership.GetRegion(
                    oldTileId,
                    (GeoRegionLayer)layer)
                : null;
            GeoRegion newRegion = newTileId >= 0
                ? membership.GetRegion(
                    newTileId,
                    (GeoRegionLayer)layer)
                : null;
            if (!ReferenceEquals(
                    oldRegion,
                    newRegion))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyUnitMembershipChange(
        in UnitMembershipChange change,
        ref long adds,
        ref long removes)
    {
        int oldTileId =
            change.OldTile?.tile_id ?? -1;
        int newTileId =
            change.NewTile?.tile_id ?? -1;
        bool addToNewRegions =
            change.Actor.isAlive();
        for (int layer = 0;
             layer < GeoRegionMembershipIndex.LayerCount;
             layer++)
        {
            GeoRegion oldRegion = oldTileId >= 0
                ? membership.GetRegion(
                    oldTileId,
                    (GeoRegionLayer)layer)
                : null;
            GeoRegion newRegion = newTileId >= 0
                ? membership.GetRegion(
                    newTileId,
                    (GeoRegionLayer)layer)
                : null;
            if (ReferenceEquals(
                    oldRegion,
                    newRegion))
            {
                continue;
            }

            if (oldRegion != null &&
                !oldRegion.isRekt() &&
                RemoveUnit(
                    oldRegion,
                    change.Actor))
            {
                removes++;
                changedUnitRegions.Add(
                    oldRegion);
            }

            if (addToNewRegions &&
                newRegion != null &&
                !newRegion.isRekt() &&
                AddUnit(
                    newRegion,
                    change.Actor))
            {
                adds++;
                changedUnitRegions.Add(
                    newRegion);
            }
        }
    }

    private bool RemoveUnit(
        GeoRegion region,
        Actor actor)
    {
        Dictionary<Actor, int> positions =
            GetOrBuildUnitPositions(region);
        if (!positions.TryGetValue(
                actor,
                out int index))
        {
            return false;
        }

        List<Actor> units = region.units;
        int lastIndex = units.Count - 1;
        Actor movedActor = units[lastIndex];
        if (index != lastIndex)
        {
            units[index] = movedActor;
            positions[movedActor] = index;
        }

        units.RemoveAt(lastIndex);
        positions.Remove(actor);
        if (units.Count == 0)
        {
            region.clearListUnits();
        }

        return true;
    }

    private bool AddUnit(
        GeoRegion region,
        Actor actor)
    {
        Dictionary<Actor, int> positions =
            GetOrBuildUnitPositions(region);
        if (positions.ContainsKey(actor))
        {
            return false;
        }

        positions.Add(
            actor,
            region.units.Count);
        region.units.Add(actor);
        return true;
    }

    private Dictionary<Actor, int>
        GetOrBuildUnitPositions(
            GeoRegion region)
    {
        if (unitPositions.TryGetValue(
                region,
                out Dictionary<Actor, int> positions))
        {
            return positions;
        }

        positions = new Dictionary<Actor, int>(
            region.units.Count,
            ActorReferenceComparer.Instance);
        for (int i = 0;
             i < region.units.Count;
             i++)
        {
            positions[region.units[i]] = i;
        }

        unitPositions.Add(
            region,
            positions);
        return positions;
    }

    private void EnsureUnitPositions()
    {
        if (!unitPositionsValid)
        {
            RebuildUnitPositions();
        }
    }

    private void RebuildUnitPositions()
    {
        unitPositions.Clear();
        foreach (GeoRegion region in this)
        {
            GetOrBuildUnitPositions(region);
        }

        unitPositionsValid = true;
    }

    private long DrainPendingUnitChanges()
    {
        long discarded = 0L;
        while (pendingUnitChanges.TryDequeue(out _))
        {
            Interlocked.Decrement(
                ref pendingUnitChangeCount);
            discarded++;
        }

        return discarded;
    }

    private void ClearPendingUnitChanges()
    {
        while (pendingUnitChanges.TryDequeue(out _))
        {
        }

        Volatile.Write(
            ref pendingUnitChangeCount,
            0);
    }

    private readonly struct UnitMembershipChange
    {
        internal UnitMembershipChange(
            Actor actor,
            WorldTile oldTile,
            WorldTile newTile)
        {
            Actor = actor;
            OldTile = oldTile;
            NewTile = newTile;
        }

        internal Actor Actor { get; }
        internal WorldTile OldTile { get; }
        internal WorldTile NewTile { get; }
    }

    private sealed class ActorReferenceComparer :
        IEqualityComparer<Actor>
    {
        internal static ActorReferenceComparer Instance { get; } =
            new();

        public bool Equals(
            Actor left,
            Actor right)
        {
            return ReferenceEquals(
                left,
                right);
        }

        public int GetHashCode(Actor actor)
        {
            return RuntimeHelpers.GetHashCode(actor);
        }
    }
}
