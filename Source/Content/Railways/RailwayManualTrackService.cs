using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Content.Railways;

/// <summary>玩家单格绘轨、四向斜线补角及实际车站连接识别，不生成虚拟轨道。</summary>
internal static class RailwayManualTrackService
{
    /// <summary>本轮发生手绘变化的格子。</summary>
    private static readonly HashSet<int> pending = new();
    /// <summary>连续斜画时的上一个合法格。</summary>
    private static WorldTile previous;
    /// <summary>读档或车站变化后按已有铁轨检查一次。</summary>
    private static bool scanExisting;

    /// <summary>免费绘制合法陆地轨道，斜向笔画补一个合法直角。</summary>
    /// <param name="tile">原版连续笔刷本次选中的格子。</param>
    /// <param name="power">原版神力编号。</param>
    internal static bool Paint(WorldTile tile, string power)
    {
        if (!CanPaint(tile)) { previous = null; return false; }
        if (previous != null && previous.top_type == TopTileTypes.TrainTrack &&
            Math.Abs(previous.x - tile.x) == 1 && Math.Abs(previous.y - tile.y) == 1)
        {
            var bend = World.world.GetTile(previous.x, tile.y);
            if (!CanPaint(bend)) bend = World.world.GetTile(tile.x, previous.y);
            if (CanPaint(bend)) Place(bend);
        }
        Place(tile); previous = tile;
        return true;
    }

    /// <summary>只擦除铁轨并撤销经过这里的连接，保留地面、农田与建筑。</summary>
    /// <param name="tile">原版笔刷选中的格子。</param>
    /// <param name="power">原版神力编号。</param>
    internal static bool Erase(WorldTile tile, string power)
    {
        previous = null;
        if (tile == null || tile.building != null || tile.top_type != TopTileTypes.TrainTrack) return false;
        TrainTrackRepairSystem.RevokeDrawnThrough(tile);
        tile.setTopTileType(null);
        foreach (var neighbour in tile.neighbours) if (neighbour != null) pending.Add(neighbour.tile_id);
        return true;
    }

    /// <summary>绘制沿用可拆除物规则，保护重要建筑，不填水或开山。</summary>
    /// <param name="tile">候选格子。</param>
    private static bool CanPaint(WorldTile tile)
        => RailTrackRoutePlanner.IsConstructionGround(tile) &&
           (tile.building == null || RailTrackRoutePlanner.CanClearBuilding(tile.building)) &&
           RailTrackRoutePlanner.IsBuildableSurface(tile.top_type) &&
           RailTrackRoutePlanner.HasBuildingClearance(tile);

    /// <summary>已经存在的轨道不反复改地形，但仍允许重新识别连通。</summary>
    /// <param name="tile">合法绘制格。</param>
    private static void Place(WorldTile tile)
    {
        RailTrackRoutePlanner.ClearForTrack(tile);
        if (tile.top_type != TopTileTypes.TrainTrack)
        {
            tile.setTopTileType(TopTileTypes.TrainTrack);
        }
        pending.Add(tile.tile_id);
    }

    /// <summary>车站、领土或存档恢复后只请求一次检查。</summary>
    internal static void RequestScan() => scanExisting = true;

    /// <summary>先找受影响的实轨分量，再按最近未连接站点登记；旧线占用时等待下一轮。</summary>
    internal static void Tick()
    {
        if (scanExisting)
        {
            foreach (var tile in TopTileTypes.TrainTrack.hashset) pending.Add(tile.tile_id);
            scanExisting = false;
        }
        if (pending.Count == 0) return;
        var seeds = pending.ToArray(); pending.Clear();
        var visited = new HashSet<WorldTile>();
        foreach (int seed in seeds)
        {
            var tile = World.world.tiles_list[seed];
            if (!Physical(tile) || visited.Contains(tile)) continue;
            var queue = new Queue<WorldTile>();
            var stations = new HashSet<Building>();
            visited.Add(tile); queue.Enqueue(tile);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.building != null) stations.Add(current.building);
                foreach (var next in current.neighbours)
                    if (Edge(current, next) && visited.Add(next)) queue.Enqueue(next);
            }
            var candidates = new List<(Building a, Building b, List<WorldTile> path)>();
            foreach (var station in stations.OrderBy(s => s.id))
            {
                var path = NearestUnconnected(station);
                if (path != null) candidates.Add((station, path[path.Count - 1].building, path));
            }
            foreach (var candidate in candidates.OrderBy(c => c.path.Count).ThenBy(c => c.a.id).ThenBy(c => c.b.id))
                if (!TrainTrackRepairSystem.RegisterDrawnLink(candidate.a, candidate.b, candidate.path))
                    pending.Add(candidate.path[0].tile_id);
        }
    }

    /// <summary>沿真实轨道寻找最近且有许可的未连接站，进站高度必须上下居中。</summary>
    /// <param name="origin">原点车站。</param>
    private static List<WorldTile> NearestUnconnected(Building origin)
    {
        if (origin.city == null || !origin.city.isAlive()) return null;
        var anchor = RailwayStationService.TrackAnchor(origin);
        var previousTiles = new Dictionary<WorldTile, WorldTile> { [anchor] = null };
        var queue = new Queue<WorldTile>(); queue.Enqueue(anchor);
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            var destination = tile.building;
            if (destination != null && destination != origin && tile == RailwayStationService.TrackAnchor(destination) &&
                destination.city != null && destination.city.isAlive() &&
                RailTrackRoutePlanner.HasPermission(origin.kingdom, destination.kingdom) &&
                !TrainTrackRepairSystem.HasProjectOrConnection(origin.id, destination.id))
            {
                var path = new List<WorldTile>();
                for (var step = tile; step != null; step = previousTiles[step]) path.Add(step);
                path.Reverse(); return path;
            }
            foreach (var next in tile.neighbours)
                if (Edge(tile, next) && !previousTiles.ContainsKey(next) &&
                    (next.zone?.city?.kingdom is not { } owner || RailTrackRoutePlanner.HasPermission(origin.kingdom, owner)))
                { previousTiles[next] = tile; queue.Enqueue(next); }
        }
        return null;
    }

    /// <summary>实际铁轨与正常车站上下居中的站内轨道参与识别。</summary>
    /// <param name="tile">候选格子。</param>
    private static bool Physical(WorldTile tile)
        => tile != null && ActorComponents.Train.IsTrackTile(tile) &&
           (tile.building == null || RailwayStationService.IsTrackRow(tile, tile.building));

    /// <summary>车站从上下居中的左右入口进出，其他格子使用四向相邻。</summary>
    /// <param name="from">当前格。</param>
    /// <param name="to">相邻候选格。</param>
    private static bool Edge(WorldTile from, WorldTile to)
        => Physical(to) && RailwayStationService.ConnectsTrack(from, to);

    /// <summary>清世界时释放笔刷与尚未处理的格子。</summary>
    internal static void Clear() { pending.Clear(); previous = null; scanExisting = false; }
}
