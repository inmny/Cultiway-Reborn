using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;

namespace Cultiway.Content.Railways;

/// <summary>统一车站选址、收费与确定的站外上下客点。</summary>
internal static class RailwayStationService
{
    /// <summary>城市至少需要的人口。</summary>
    internal const int MinimumPopulation = 50;
    /// <summary>每个城市支付自己的车站费用。</summary>
    internal static readonly ConstructionCost StationCost = new(40, 20, 5, 20);
    /// <summary>当前30像素高、0.25倍缩放的车站，上下中点落在锚点上方第4格。</summary>
    private const int TrackHeightOffset = 4;

    /// <summary>城市本身具备建站人口、资源且没有车站。</summary>
    /// <param name="city">提出建设的城市。</param>
    internal static bool CanAffordStation(City city)
        => city != null && city.getPopulationPeople() >= MinimumPopulation &&
           !HasStation(city) && city.hasEnoughResourcesFor(StationCost);

    /// <summary>按人口、库存、谋划和实际选址依次返回失败原因，空值表示可以发起。</summary>
    /// <param name="actor">请求发起谋划的人物。</param>
    internal static string GetBuildBlockReason(Actor actor)
    {
        if (actor == null || !actor.hasCity()) return "NoCity";
        var city = actor.city;
        if (HasStation(city)) return "ExistingStation";
        if (city.getPopulationPeople() < MinimumPopulation) return "Population";
        if (!city.hasEnoughResourcesFor(StationCost)) return "Resources";
        if (World.world.plots.isPlotTypeAlreadyRunning(actor, Plots.BuildTrainStation)) return "PlotBusy";
        if (FindSite(city) == null) return "NoSite";
        if (FindPartner(city) == null) return "NoPartner";
        return null;
    }

    /// <summary>直接检查建筑占地，刚创建但尚未刷新城市列表的车站也算存在。</summary>
    /// <param name="city">待检查城市。</param>
    internal static bool HasStation(City city)
        => city.zones.Any(z => z.buildings_all.Any(b => b.asset == Buildings.TrainStation &&
            !b.isRemoved() && !b.isRuin()));

    /// <summary>只寻找一个最近的许可邻城，避免联合谋划免费铺开所有城市。</summary>
    /// <param name="city">发起城市。</param>
    internal static City FindPartner(City city)
        => city.neighbours_cities.Where(c => RailTrackRoutePlanner.HasPermission(city.kingdom, c.kingdom) &&
                (HasStation(c) || (CanAffordStation(c) && FindSite(c) != null)))
            .OrderBy(c => Toolbox.SquaredDistTile(city.getTile(), c.getTile())).ThenBy(c => c.id).FirstOrDefault();

    /// <summary>先检查完整占地与站外出入口，再创建；创建成功才收费。</summary>
    /// <param name="city">承担本站费用的城市。</param>
    internal static bool TryBuild(City city)
    {
        if (!CanAffordStation(city) || FindPartner(city) == null) return false;
        var tile = FindSite(city);
        if (tile == null) return false;
        // 原版占地流程会清除树木花草，选址阶段不提前破坏植被。
        var station = World.world.buildings.addBuilding(Buildings.TrainStation.id, tile);
        if (station == null) return false;
        city.spendResourcesForBuildingAsset(StationCost);
        TrainTrackRepairSystem.RequestRebuild();
        return true;
    }

    /// <summary>车站左右侧接入的共同轨道端点，高度对齐建筑上下的中点。</summary>
    /// <param name="station">实际车站。</param>
    internal static WorldTile TrackAnchor(Building station)
        => World.world.GetTile(station.current_tile.x, station.current_tile.y + TrackHeightOffset);

    /// <summary>站内只沿上下居中的同一横行通过，不能从正门、侧面其他高度或屋顶穿入。</summary>
    /// <param name="tile">候选轨道格。</param>
    /// <param name="station">所属车站。</param>
    internal static bool IsTrackRow(WorldTile tile, Building station)
        => tile.building == station && tile.y == station.current_tile.y + TrackHeightOffset;

    /// <summary>轨道显示与实际进站共用左右侧入口，不把其他高度画成连接方向。</summary>
    /// <param name="from">当前铁轨格。</param>
    /// <param name="to">相邻候选格。</param>
    internal static bool ConnectsTrack(WorldTile from, WorldTile to)
    {
        if (to == null) return false;
        var station = to.building;
        if (station?.asset == Buildings.TrainStation &&
            (!station.isNormal() || !IsTrackRow(to, station) || from.y != to.y)) return false;
        if (from.building?.asset == Buildings.TrainStation &&
            (!from.building.isNormal() || !IsTrackRow(from, from.building) || from.y != to.y)) return false;
        return to.top_type == TopTileTypes.TrainTrack;
    }

    /// <summary>补齐建筑遮住的站内横向轨道，不计为站外铺轨费用。</summary>
    /// <param name="station">已经完工的正常车站。</param>
    internal static void PrepareTrackEntrance(Building station)
    {
        foreach (var tile in station.tiles.Where(t => IsTrackRow(t, station)))
            if (tile.top_type != TopTileTypes.TrainTrack) tile.setTopTileType(TopTileTypes.TrainTrack);
    }

    /// <summary>保留原版分区规则，逐个检查完整占地，避免单个候选失败就放弃整片分区。</summary>
    /// <param name="city">本站所属城市。</param>
    internal static WorldTile FindSite(City city)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(Buildings.TrainStation, city, zones);
        var centerZone = city.getTile()?.zone;
        var footprint = new HashSet<WorldTile>();
        // 先排除原本就走不到的站口，再检查新车站占地是否会堵住通路。
        var townReachable = ReachTown(city, footprint);
        foreach (var zone in zones)
        {
            if (city.hasSpecialTownPlans() && !city.planAllowsToPlaceBuildingInZone(zone, centerZone)) continue;
            foreach (var tile in zone.tiles)
            {
                if (!CanPlaceFootprint(tile, city) || !CityBehBuild.isGoodTileForBuilding(tile, Buildings.TrainStation, city)) continue;
                footprint.Clear();
                for (int y = 0; y < 5; y++)
                for (int x = -5; x < 5; x++)
                    footprint.Add(World.world.GetTile(tile.x + x, tile.y + y));
                if (SelectPassengerTile(tile, footprint, city, townReachable) != null) return tile;
            }
        }
        return null;
    }

    /// <summary>快速检查完整占地和铁路入口，树木花草可被车站占用。</summary>
    /// <param name="tile">候选站内端点。</param>
    /// <param name="city">建设城市。</param>
    private static bool CanPlaceFootprint(WorldTile tile, City city)
    {
        for (int y = 0; y < 5; y++)
        for (int x = -5; x < 5; x++)
        {
            var part = World.world.GetTile(tile.x + x, tile.y + y);
            if (!RailTrackRoutePlanner.IsSafeGround(part) ||
                (part.building != null && !part.building.asset.flora) || part.zone?.city != city ||
                !RailTrackRoutePlanner.IsBuildableSurface(part.top_type)) return false;
        }
        return new[] { World.world.GetTile(tile.x - 6, tile.y + TrackHeightOffset),
                World.world.GetTile(tile.x + 5, tile.y + TrackHeightOffset) }
            .Any(entrance => RailTrackRoutePlanner.IsSafeGround(entrance) &&
                             (entrance.building == null || entrance.building.asset.flora) &&
                             RailTrackRoutePlanner.IsBuildableSurface(entrance.top_type) &&
                             RailTrackRoutePlanner.HasBuildingClearance(entrance));
    }

    /// <summary>寻路、候车与下车都使用同一个确定的站外安全格。</summary>
    /// <param name="station">已占地的车站。</param>
    internal static WorldTile GetPassengerTile(Building station)
        => SelectPassengerTile(station.current_tile, new HashSet<WorldTile>(station.tiles), station.city);

    /// <summary>优先沿现有道路选择站口，树木花草不阻挡步行连通。</summary>
    /// <param name="anchor">站内轨道端点。</param>
    /// <param name="footprint">建筑完整占地。</param>
    /// <param name="city">所属城市；独立测试站点可以没有城市。</param>
    /// <param name="townReachable">本次选址前从城内可步行到达的格子，用于排除无路可达的站口。</param>
    private static WorldTile SelectPassengerTile(WorldTile anchor, HashSet<WorldTile> footprint, City city,
        HashSet<WorldTile> townReachable = null)
    {
        var candidates = footprint.SelectMany(t => t.neighbours).Distinct()
            .Where(t => t != null && !footprint.Contains(t) && (t.building == null || t.building.asset.flora) &&
                        RailTrackRoutePlanner.IsSafeGround(t) && t.top_type != TopTileTypes.TrainTrack &&
                        t.y != anchor.y + TrackHeightOffset &&
                        (townReachable == null || townReachable.Contains(t))).ToArray();
        if (candidates.Length == 0) return null;
        var reachable = city == null ? null : ReachTown(city, footprint);
        return candidates
            .OrderByDescending(t => t.Type.road)
            .ThenBy(t => Math.Abs(t.x - anchor.x) + Math.Abs(t.y - anchor.y)).ThenBy(t => t.tile_id)
            .FirstOrDefault(t => reachable == null || reachable.Contains(t));
    }

    /// <summary>按居民的安全陆地通行规则检查城内连通，车站的新占地单独阻挡。</summary>
    /// <param name="city">车站所属城市。</param>
    /// <param name="footprint">新车站将要占用的格子。</param>
    private static HashSet<WorldTile> ReachTown(City city, HashSet<WorldTile> footprint)
    {
        var reached = new HashSet<WorldTile>();
        var queue = new Queue<WorldTile>();
        // 原版可能把车站或大型建筑选作城市中心，必须从既有建筑外围起步。
        var anchor = city.buildings.Where(b => b.asset != Buildings.TrainStation && b.isNormal() &&
                b.asset.city_building && !b.asset.docks)
            .OrderByDescending(b => b.asset.type == "type_bonfire")
            .ThenByDescending(b => b.asset.type == "type_hall").ThenBy(b => b.id).FirstOrDefault();
        var seeds = anchor == null ? city.getTile().neighbours : anchor.tiles.SelectMany(t => t.neighbours);
        foreach (var tile in seeds)
            if (CanWalk(tile) && reached.Add(tile)) queue.Enqueue(tile);
        while (queue.Count > 0)
            foreach (var tile in queue.Dequeue().neighbours)
                if (CanWalk(tile) && reached.Add(tile)) queue.Enqueue(tile);
        return reached;

        // 居民寻路不把普通建筑占地当墙；只拦地形障碍和本次车站占地，避免密集城区被误判成孤岛。
        bool CanWalk(WorldTile tile) => tile != null && tile.zone?.city == city && !footprint.Contains(tile) &&
                                        RailTrackRoutePlanner.IsSafeGround(tile);
    }
}
