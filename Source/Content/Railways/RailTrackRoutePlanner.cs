using System;
using System.Collections.Generic;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils;

namespace Cultiway.Content.Railways;

/// <summary>在现有导航地形上选择四向连续的合法陆地铁路。</summary>
internal static class RailTrackRoutePlanner
{
    /// <summary>两国均允许通行才可共同建设和运营。</summary>
    /// <param name="a">一方国家。</param>
    /// <param name="b">另一方国家。</param>
    internal static bool HasPermission(Kingdom a, Kingdom b)
        => a != null && b != null && (a == b ||
            (!a.isEnemy(b) && !b.isEnemy(a) && a.isOpinionTowardsKingdomGood(b) && b.isOpinionTowardsKingdomGood(a)));

    /// <summary>检查安全陆地；不把飞行、游泳或破墙能力用于工程。</summary>
    /// <param name="tile">候选地块。</param>
    internal static bool IsSafeGround(WorldTile tile)
        => tile?.Type != null && tile.main_type.ground && !tile.main_type.liquid &&
           !tile.main_type.block && !tile.main_type.lava && !tile.Type.block &&
           !tile.Type.liquid && !tile.Type.lava && !tile.Type.damage_units && !tile.isOnFire();

    /// <summary>普通草皮可以被工程覆盖；农田和其他专用表层仍保留。</summary>
    /// <param name="surface">当前地表。</param>
    internal static bool IsBuildableSurface(TopTileType surface)
        => surface == null || surface == TopTileTypes.TrainTrack || surface.road || surface.wall ||
           (surface.is_biome && !surface.farm_field);

    /// <summary>规划允许在安全陆地上拆墙；运行中的列车仍必须使用已经清好的轨道。</summary>
    /// <param name="tile">规划经过的格子。</param>
    internal static bool IsConstructionGround(WorldTile tile)
        => IsSafeGround(tile) || (tile?.top_type?.wall == true && tile.main_type.ground &&
            !tile.main_type.block && !tile.main_type.liquid && !tile.main_type.lava &&
            !tile.main_type.damage_units && !tile.isOnFire());

    /// <summary>允许拆除自然物、矿石、城墙和初级住宅，其他城市设施继续绕开。</summary>
    /// <param name="building">实际阻挡物。</param>
    internal static bool CanClearBuilding(Building building)
        => building.asset.flora || building.asset.type is "type_tree" or "type_mineral" or "type_wall" ||
           (building.asset.type == "type_house" && building.asset.upgrade_level <= 1);

    /// <summary>通过原版拆除流程立即释放占地，保留原版动画和城市设施更新。</summary>
    /// <param name="tile">已经通过规划与费用检查的施工格。</param>
    internal static void ClearForTrack(WorldTile tile)
    {
        if (tile.building != null && tile.building.asset != Buildings.TrainStation)
            tile.building.startDestroyBuilding();
    }

    /// <summary>轨道仍只铺一格，周围一格供建筑避让；转弯和端头也保留间隔。</summary>
    /// <param name="tile">建筑完整占地中的一格。</param>
    internal static bool IsReservedForTrack(WorldTile tile)
    {
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (World.world.GetTile(tile.x + x, tile.y + y)?.top_type == TopTileTypes.TrainTrack)
                return true;
        return false;
    }

    /// <summary>新轨道与其他建筑留一格间距，自然植被和正常车站的接轨口除外。</summary>
    /// <param name="tile">轨道中心格。</param>
    /// <param name="a">工程起站。</param>
    /// <param name="b">工程终站。</param>
    internal static bool HasBuildingClearance(WorldTile tile, Building a = null, Building b = null)
    {
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            if (x == 0 && y == 0) continue;
            var building = World.world.GetTile(tile.x + x, tile.y + y)?.building;
            if (building == null || building == a || building == b || building.asset.flora) continue;
            if (building == tile.building && CanClearBuilding(building)) continue;
            if (building.asset == Buildings.TrainStation && building.isNormal() &&
                tile.y == RailwayStationService.TrackAnchor(building).y) continue;
            return false;
        }
        return true;
    }

    /// <summary>规划与施工检查建筑间距；既有列车运行仍按实际轨道判断。</summary>
    /// <param name="tile">工程经过的地块。</param>
    /// <param name="a">起站。</param>
    /// <param name="b">终站。</param>
    internal static bool CanBuildWithClearance(WorldTile tile, Building a, Building b)
        => CanBuild(tile, a, b) && HasBuildingClearance(tile, a, b);

    /// <summary>检查可拆除物、车站轨道行和领土许可；规划本身不修改地形。</summary>
    /// <param name="tile">工程经过的地块。</param>
    /// <param name="a">起站。</param>
    /// <param name="b">终站。</param>
    internal static bool CanBuild(WorldTile tile, Building a, Building b)
    {
        if (!IsConstructionGround(tile)) return false;
        if (tile.building != null && tile.building != a && tile.building != b && !CanClearBuilding(tile.building)) return false;
        // 车站从左右侧接入，整段站内轨道保持在建筑上下居中的同一行。
        if ((tile.building == a && !RailwayStationService.IsTrackRow(tile, a)) ||
            (tile.building == b && !RailwayStationService.IsTrackRow(tile, b))) return false;
        if (!IsBuildableSurface(tile.top_type)) return false;
        var owner = tile.zone?.city?.kingdom;
        return owner == null || (HasPermission(a.kingdom, owner) && HasPermission(b.kingdom, owner));
    }

    /// <summary>先读取现有导航数据，再按工程代价找路，最后回溯完整路径。</summary>
    /// <param name="a">起站，路径包含其站内端点。</param>
    /// <param name="b">终站，路径包含其站内端点。</param>
    /// <returns>没有合法陆路时返回空列表。</returns>
    internal static List<WorldTile> BuildPath(Building a, Building b)
    {
        var result = new List<WorldTile>();
        if (a == null || b == null || !HasPermission(a.kingdom, b.kingdom)) return result;
        PathNavigationGridService.FlushDirty();
        var grid = PathNavigationGridService.Current;
        var start = RailwayStationService.TrackAnchor(a);
        var target = RailwayStationService.TrackAnchor(b);
        if (!CanBuildWithClearance(start, a, b) || !CanBuildWithClearance(target, a, b)) return result;

        var costs = new Dictionary<int, int> { [start.tile_id] = 0 };
        var previous = new Dictionary<int, int>();
        var comparer = Comparer<(WorldTile tile, int cost, int estimate)>.Create((x, y) =>
        {
            int order = x.estimate.CompareTo(y.estimate);
            return order != 0 ? order : x.tile.tile_id.CompareTo(y.tile.tile_id);
        });
        var open = new PriorityQueuePreview<(WorldTile tile, int cost, int estimate)>(256, comparer);
        open.Enqueue((start, 0, grid.ManhattanDistance(start.tile_id, target.tile_id)));
        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current.cost != costs[current.tile.tile_id]) continue;
            if (current.tile == target)
            {
                int id = target.tile_id;
                result.Add(target);
                while (id != start.tile_id)
                {
                    id = previous[id];
                    result.Add(World.world.tiles_list[id]);
                }
                result.Reverse();
                return result;
            }
            foreach (var next in current.tile.neighbours)
            {
                if (next == null || !CanBuildWithClearance(next, a, b)) continue;
                int step = next.building == a || next.building == b || next.top_type == TopTileTypes.TrainTrack
                    ? 1 : next.Type.road ? 3 : 4;
                if (next.top_type?.wall == true || next.building != null && next.building != a && next.building != b)
                    step += 8;
                int cost = current.cost + step;
                if (costs.TryGetValue(next.tile_id, out int old) && cost >= old) continue;
                costs[next.tile_id] = cost;
                previous[next.tile_id] = current.tile.tile_id;
                open.Enqueue((next, cost, cost + grid.ManhattanDistance(next.tile_id, target.tile_id)));
            }
        }
        return result;
    }
}
