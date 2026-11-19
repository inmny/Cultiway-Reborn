using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Utils;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>把有官道文化和已完工大厅的村庄接入本国首都的陆路网络。</summary>
public sealed class OfficialRoadSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>无工程时每两秒检查一次实际道路。</summary>
    private const float ScanInterval = 2f;
    /// <summary>无法通行的一对村庄等待三十秒后重新尝试。</summary>
    private const float FailedRouteInterval = 30f;
    /// <summary>每帧最多扫描的村庄地块数量。</summary>
    private const int ScanTilesPerFrame = 2048;
    /// <summary>每帧最多铺设的道路格数。</summary>
    private const int RoadTilesPerFrame = 16;
    /// <summary>当前世界的系统实例，供大厅完工通知使用。</summary>
    private static OfficialRoadSystem instance;
    /// <summary>当前正在检查、寻路或铺路的分帧工作。</summary>
    private IEnumerator work;
    /// <summary>工作所属世界，避免读档后使用旧地块。</summary>
    private MapBox world;
    /// <summary>下次开始检查的实际时间。</summary>
    private float nextScan;
    /// <summary>下一次重新尝试失败路线的实际时间。</summary>
    private float retryFailedAt;
    /// <summary>同一画面帧只推进一次。</summary>
    private int lastFrame = -1;
    /// <summary>暂时不可行的村庄连接，防止连续重复搜索。</summary>
    private readonly HashSet<(long, long)> failedPairs = new();

    /// <summary>记录当前实例。</summary>
    public OfficialRoadSystem() => instance = this;

    /// <summary>大厅完工后尽快检查，不打断已经开始的工作。</summary>
    internal static void RequestRefresh()
    {
        if (instance != null) instance.nextScan = 0f;
    }

    /// <summary>按实际帧推进少量工作，不随游戏加速在同一帧反复铺路。</summary>
    protected override void OnUpdateGroup()
    {
        if (!Config.game_loaded || World.world?.tiles_list == null || lastFrame == Time.frameCount) return;
        if (world != World.world)
        {
            ClearWorldState();
            world = World.world;
        }
        lastFrame = Time.frameCount;
        if (Time.realtimeSinceStartup >= retryFailedAt)
        {
            failedPairs.Clear();
            retryFailedAt = Time.realtimeSinceStartup + FailedRouteInterval;
        }
        if (work == null)
        {
            if (Time.realtimeSinceStartup < nextScan) return;
            work = MaintainNetwork();
        }
        if (work.MoveNext()) return;
        (work as IDisposable)?.Dispose();
        work = null;
    }

    /// <summary>切换世界时丢弃未完成工作；道路本身随原版地图保存。</summary>
    public void ClearWorldState()
    {
        (work as IDisposable)?.Dispose();
        work = null;
        world = null;
        nextScan = retryFailedAt = 0f;
        lastFrame = -1;
        failedPairs.Clear();
    }

    /// <summary>依次收集道路、从首都遍历、选择邻近村庄、分帧寻路并分批铺路。</summary>
    private IEnumerator MaintainNetwork()
    {
        nextScan = Time.realtimeSinceStartup + ScanInterval;
        List<VillageNode> nodes = CollectVillages();
        if (nodes.Count == 0) yield break;

        // 道路段之间可以通过同一村庄换接；只收集大厅所在陆地上的道路。
        var villageByCity = nodes.ToDictionary(node => node.City);
        int scanned = 0;
        foreach (VillageNode node in nodes)
        {
            foreach (TileZone zone in node.City.zones.ToArray())
            {
                if (zone.city != node.City) continue;
                foreach (WorldTile tile in zone.tiles)
                {
                    if (tile.Type.road && CanTraverse(tile, node.Country)
                        && tile.region?.island == node.Anchor.region?.island) node.Roads.Add(tile);
                }
                scanned += zone.tiles.Length;
                if (scanned < ScanTilesPerFrame) continue;
                scanned = 0;
                yield return null;
            }
        }

        var connected = new HashSet<VillageNode>();
        foreach (VillageNode capital in nodes)
        {
            if (capital.City != capital.Country.capital) continue;
            var queue = new Queue<VillageNode>();
            var roadQueue = new Queue<WorldTile>();
            var visitedRoads = new HashSet<WorldTile>();
            connected.Add(capital);
            queue.Enqueue(capital);
            while (queue.Count > 0)
            {
                VillageNode current = queue.Dequeue();
                foreach (WorldTile road in current.Roads)
                {
                    if (visitedRoads.Add(road)) roadQueue.Enqueue(road);
                    if (++scanned < ScanTilesPerFrame) continue;
                    scanned = 0;
                    yield return null;
                }
                // 原版没有维护 road_island；直接沿实际道路行走，避免反复修同一对村庄。
                while (roadQueue.Count > 0)
                {
                    WorldTile road = roadQueue.Dequeue();
                    City owner = road.zone.city;
                    if (owner != null && villageByCity.TryGetValue(owner, out VillageNode neighbour)
                        && neighbour.Country == capital.Country
                        && road.region?.island == neighbour.Anchor.region?.island && connected.Add(neighbour))
                        queue.Enqueue(neighbour);
                    foreach (WorldTile next in road.neighboursAll)
                    {
                        if (next.Type.road && CanTraverse(next, capital.Country)
                            && next.region?.island == capital.Anchor.region?.island && visitedRoads.Add(next))
                            roadQueue.Enqueue(next);
                    }
                    if (++scanned < ScanTilesPerFrame) continue;
                    scanned = 0;
                    yield return null;
                }
            }
        }

        // 只从已通首都的节点向外扩展，不让几个邻近村庄形成孤立的小圈。
        VillageNode from = null;
        VillageNode to = null;
        int bestDistance = int.MaxValue;
        foreach (VillageNode candidate in nodes)
        {
            if (connected.Contains(candidate) || !HasOfficialRoads(candidate.City)) continue;
            foreach (VillageNode target in connected)
            {
                if (candidate.Country != target.Country || candidate.Anchor.region?.island == null
                    || candidate.Anchor.region.island != target.Anchor.region?.island
                    || failedPairs.Contains(Pair(candidate.City, target.City))) continue;
                int distance = Toolbox.SquaredDistTile(candidate.Anchor, target.Anchor);
                if (distance >= bestDistance) continue;
                from = candidate;
                to = target;
                bestDistance = distance;
            }
            yield return null;
        }
        if (from == null) yield break;

        City originalCapital = from.Country.capital;
        var search = new RoadSearch(from.Anchor, to.Anchor, from.Country);
        while (!search.Finished)
        {
            if (!CanContinue(from, to, originalCapital)) yield break;
            search.Advance();
            yield return null;
        }
        if (!search.Found)
        {
            failedPairs.Add(Pair(from.City, to.City));
            yield break;
        }

        // 回溯也分帧；从已经接通的一端向新村庄铺路，不额外复制全图数据。
        var path = new List<WorldTile>();
        WorldTile step = to.Anchor;
        while (true)
        {
            path.Add(step);
            if (step == from.Anchor) break;
            step = world.tiles_list[search.Previous[step.tile_id]];
            if (path.Count % 256 == 0) yield return null;
        }
        for (int i = 0; i < path.Count;)
        {
            if (!CanContinue(from, to, originalCapital)) yield break;
            int end = Math.Min(i + RoadTilesPerFrame, path.Count);
            for (; i < end; i++)
            {
                WorldTile tile = path[i];
                if (!CanTraverse(tile, from.Country)) yield break;
                if (tile.Type.road || tile.building != null && !tile.building.asset.flora) continue;
                MapAction.createRoadTile(tile);
            }
            yield return null;
        }
        nextScan = 0f;
    }

    /// <summary>只为存在官道需求的国家收集已完工大厅和首都节点。</summary>
    /// <returns>本轮稳定的村庄列表。</returns>
    private List<VillageNode> CollectVillages()
    {
        var countries = new HashSet<Kingdom>();
        foreach (City city in world.cities.list)
        {
            if (HasOfficialRoads(city) && city.kingdom?.capital != null
                && city.getBuildingOfType("type_hall") != null) countries.Add(city.kingdom);
        }
        var result = new List<VillageNode>();
        foreach (City city in world.cities.list)
        {
            if (!city.isAlive() || !countries.Contains(city.kingdom)) continue;
            WorldTile anchor = Anchor(city);
            if (anchor != null && CanTraverse(anchor, city.kingdom))
                result.Add(new VillageNode(city, anchor));
        }
        return result;
    }

    /// <summary>大厅作为村庄节点；首都尚无完工大厅时使用原版城市中心。</summary>
    /// <param name="city">需要定位的村庄。</param>
    /// <returns>节点地块；普通村庄未建好大厅时返回空。</returns>
    private static WorldTile Anchor(City city)
    {
        return city.getBuildingOfType("type_hall")?.current_tile
               ?? (city.kingdom?.capital == city ? city.getTile() : null);
    }

    /// <summary>确认城市仍有官道文化。</summary>
    /// <param name="city">需要检查的村庄。</param>
    /// <returns>城市存活且文化选中官道时返回真。</returns>
    private static bool HasOfficialRoads(City city)
    {
        return city.isAlive() && city.hasCulture() && city.culture.hasTrait(CultureTraits.OfficialRoadsId);
    }

    /// <summary>施工途中重新检查国家、首都和大厅，避免沿用失效的端点。</summary>
    /// <param name="from">待接通的村庄。</param>
    /// <param name="to">已经连通的村庄。</param>
    /// <param name="capital">开始规划时的首都。</param>
    /// <returns>原连接仍然有效时返回真。</returns>
    private static bool CanContinue(VillageNode from, VillageNode to, City capital)
    {
        return HasOfficialRoads(from.City) && to.City.isAlive()
               && from.City.kingdom == from.Country && to.City.kingdom == from.Country
               && from.Country.capital == capital && Anchor(from.City) == from.Anchor && Anchor(to.City) == to.Anchor;
    }

    /// <summary>只使用安全陆地；保护山体、水域、城墙、农田和外国建筑。</summary>
    /// <param name="tile">待经过的地块。</param>
    /// <param name="country">修路的国家。</param>
    /// <returns>可以经过并铺路时返回真；本国建筑仅作为村庄内部的步行连接。</returns>
    private static bool CanTraverse(WorldTile tile, Kingdom country)
    {
        var ground = tile?.main_type;
        var surface = tile?.Type;
        return ground != null && surface != null && ground.ground && !ground.liquid && !ground.block
               && !ground.mountains && !ground.summit && !ground.lava && !surface.block && !surface.liquid
               && !surface.damage_units && tile.top_type?.farm_field != true
               && (tile.building == null || tile.building.asset.flora || tile.building.city?.kingdom == country);
    }

    /// <summary>统一失败连接的两端顺序。</summary>
    /// <param name="a">一端村庄。</param>
    /// <param name="b">另一端村庄。</param>
    /// <returns>不区分方向的村庄编号对。</returns>
    private static (long, long) Pair(City a, City b) => a.id < b.id ? (a.id, b.id) : (b.id, a.id);

    /// <summary>一次路网检查中固定的村庄节点。</summary>
    private sealed class VillageNode
    {
        /// <summary>村庄对象。</summary>
        internal readonly City City;
        /// <summary>收集节点时所属的国家。</summary>
        internal readonly Kingdom Country;
        /// <summary>大厅或首都中心。</summary>
        internal readonly WorldTile Anchor;
        /// <summary>该村庄所在陆地上的实际道路格子。</summary>
        internal readonly List<WorldTile> Roads = new();

        /// <summary>记录稳定的节点端点。</summary>
        /// <param name="city">村庄。</param>
        /// <param name="anchor">节点地块。</param>
        internal VillageNode(City city, WorldTile anchor)
        {
            City = city;
            Country = city.kingdom;
            Anchor = anchor;
        }
    }

    /// <summary>按小批量展开地块，使用现有队列寻找连续陆路。</summary>
    private sealed class RoadSearch
    {
        /// <summary>目标大厅。</summary>
        private readonly WorldTile target;
        /// <summary>修路国家。</summary>
        private readonly Kingdom country;
        /// <summary>已经找到的最小行进代价。</summary>
        private readonly Dictionary<int, int> costs = new();
        /// <summary>等待搜索的地块，优先继续较短路线。</summary>
        private readonly PriorityQueuePreview<(WorldTile tile, int cost, int estimate)> open;
        /// <summary>回溯整条道路所需的前一格。</summary>
        internal readonly Dictionary<int, int> Previous = new();
        /// <summary>是否已经完成搜索。</summary>
        internal bool Finished;
        /// <summary>是否找到目标。</summary>
        internal bool Found;

        /// <summary>从源大厅开始，优先复用已有道路。</summary>
        /// <param name="start">源大厅。</param>
        /// <param name="target">目标大厅。</param>
        /// <param name="country">修路国家。</param>
        internal RoadSearch(WorldTile start, WorldTile target, Kingdom country)
        {
            this.target = target;
            this.country = country;
            var comparer = Comparer<(WorldTile tile, int cost, int estimate)>.Create((a, b) =>
            {
                int order = a.estimate.CompareTo(b.estimate);
                return order != 0 ? order : a.tile.tile_id.CompareTo(b.tile.tile_id);
            });
            open = new PriorityQueuePreview<(WorldTile tile, int cost, int estimate)>(256, comparer);
            costs[start.tile_id] = 0;
            open.Enqueue((start, 0, Distance(start, target)));
        }

        /// <summary>每帧最多展开二百五十六格，同时把搜索耗时限制在约两毫秒内。</summary>
        internal void Advance()
        {
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            for (int expanded = 0; expanded < 256 && open.Count > 0; expanded++)
            {
                if (Stopwatch.GetTimestamp() >= deadline) return;
                var current = open.Dequeue();
                if (current.cost != costs[current.tile.tile_id]) continue;
                if (current.tile == target)
                {
                    Finished = Found = true;
                    return;
                }
                foreach (WorldTile next in current.tile.neighbours)
                {
                    if (!CanTraverse(next, country)) continue;
                    int cost = current.cost + (next.Type.road ? 1 : 4);
                    if (costs.TryGetValue(next.tile_id, out int previous) && cost >= previous) continue;
                    costs[next.tile_id] = cost;
                    Previous[next.tile_id] = current.tile.tile_id;
                    open.Enqueue((next, cost, cost + Distance(next, target)));
                }
            }
            if (open.Count == 0) Finished = true;
        }

        /// <summary>计算不会高估最短行进代价的四向距离。</summary>
        /// <param name="a">起点。</param>
        /// <param name="b">终点。</param>
        /// <returns>横向距离与纵向距离之和。</returns>
        private static int Distance(WorldTile a, WorldTile b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
}
