using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Railways;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Performance;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content;

/// <summary>统一管理铁路施工、付费、维修与对外发布的运营连接。</summary>
public sealed class TrainTrackRepairSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>工程与维修每两秒执行一次。</summary>
    private const float WorkInterval = 2f;
    /// <summary>首次重试延迟。</summary>
    private const float RetryDelay = 5f;
    /// <summary>失败后的重试间隔。</summary>
    private const float RetryInterval = 10f;
    /// <summary>每轮最多推进三条工程。</summary>
    private const int ProjectsPerTick = 3;
    /// <summary>每条工程每轮最多检查并铺设十六格。</summary>
    private const int TilesPerBatch = 16;
    /// <summary>连续三次失败后等实际条件变化再重试。</summary>
    private const int MaxFailures = 3;
    /// <summary>每对车站仅保存一份工程。</summary>
    private static readonly Dictionary<(long a, long b), LinkInfo> links = new();
    /// <summary>由地块找到受影响的工程，包含暂停中的线路。</summary>
    private static readonly Dictionary<int, HashSet<LinkInfo>> tileMap = new();
    /// <summary>下一次工程处理时间。</summary>
    private static double nextWork;
    /// <summary>站点或连接变化后统一重建网络。</summary>
    private static bool dirtyConnectivity;
    /// <summary>轮转起点，避免前几条工程一直占用每轮名额。</summary>
    private static int workCursor;

    /// <summary>一条线路的运行数据；存档只写编号和数值。</summary>
    private sealed class LinkInfo
    {
        /// <summary>有序路径的起站。</summary>
        internal long A;
        /// <summary>有序路径的终站。</summary>
        internal long B;
        /// <summary>承担轨道费用的城市。</summary>
        internal long Payer;
        /// <summary>包含站内端点的完整路径。</summary>
        internal List<WorldTile> Tiles;
        /// <summary>当前施工或运营状态。</summary>
        internal RailwayLinkState State;
        /// <summary>暂停前的状态。</summary>
        internal RailwayLinkState ResumeState;
        /// <summary>暂停或受损原因。</summary>
        internal string Reason = "";
        /// <summary>下一批施工起点。</summary>
        internal int Cursor;
        /// <summary>本次工程已经新铺的格数。</summary>
        internal int Laid;
        /// <summary>本次工程已支付的八格费用份数。</summary>
        internal int Paid;
        /// <summary>当前预计总费用份数。</summary>
        internal int Budget;
        /// <summary>下一次维修时间。</summary>
        internal double NextRepair;
        /// <summary>连续维修失败次数。</summary>
        internal int Failures;
        /// <summary>玩家绘制的线路受损后不自动补回，等待玩家修改或城市重新申请。</summary>
        internal bool PlayerDrawn;
    }

    /// <summary>统一两端顺序，避免双向重复立项。</summary>
    /// <param name="a">一端车站编号。</param>
    /// <param name="b">另一端车站编号。</param>
    private static (long, long) Key(long a, long b) => a <= b ? (a, b) : (b, a);

    /// <summary>新线先检查路线和预算；中断的旧线允许重新申请，沿用补轨或绕行流程。</summary>
    /// <param name="a">提议建设的起站。</param>
    /// <param name="b">目标车站。</param>
    /// <param name="payer">仅此城市承担轨道费用。</param>
    internal static bool StartConstruction(Building a, Building b, City payer)
    {
        if (!IsStationValid(a) || !IsStationValid(b) || payer == null || payer != a.city || a == b ||
            !RailTrackRoutePlanner.HasPermission(a.kingdom, b.kingdom) || HasProjectOrConnection(a.id, b.id)) return false;
        if (links.TryGetValue(Key(a.id, b.id), out var previous) && previous.State != RailwayLinkState.Revoked)
        {
            previous.Payer = payer.id;
            previous.PlayerDrawn = false;
            Damage(previous);
            previous.NextRepair = SimulationTime.Now;
            return true;
        }
        var path = RailTrackRoutePlanner.BuildPath(a, b);
        if (path.Count < 2) return false;
        int budget = Units(CountMissing(path));
        if (!payer.hasEnoughResourcesFor(Cost(budget))) return false;
        var link = new LinkInfo
        {
            A = a.id, B = b.id, Payer = payer.id, Tiles = path,
            State = RailwayLinkState.Construction, Budget = budget
        };
        ReplaceLink(link);
        return true;
    }

    /// <summary>正在施工的工程和实际可运营通路阻止重复立项，受损或暂停的线路允许重试。</summary>
    /// <param name="a">始发车站编号。</param>
    /// <param name="b">目标车站编号。</param>
    internal static bool HasProjectOrConnection(long a, long b)
    {
        if (links.TryGetValue(Key(a, b), out var project) &&
            project.State is RailwayLinkState.Construction or RailwayLinkState.Repairing) return true;
        var visited = new HashSet<long> { a };
        var queue = new Queue<long>();
        queue.Enqueue(a);
        while (queue.Count > 0)
        {
            long current = queue.Dequeue();
            foreach (var link in links.Values)
            {
                if (link.State != RailwayLinkState.Normal || !IsOperational(link)) continue;
                long next = link.A == current ? link.B : link.B == current ? link.A : 0;
                if (next == b) return true;
                if (next != 0 && visited.Add(next)) queue.Enqueue(next);
            }
        }
        return false;
    }

    /// <summary>撤销工程只停止后续施工，已经付费铺出的轨道保留。</summary>
    /// <param name="a">一端车站编号。</param>
    /// <param name="b">另一端车站编号。</param>
    internal static void CancelConstruction(long a, long b)
    {
        if (!links.TryGetValue(Key(a, b), out var link)) return;
        link.State = RailwayLinkState.Revoked;
        link.Reason = "工程撤销";
        dirtyConnectivity = true;
    }

    /// <summary>仍被线路、工程或列车使用的铁轨保留维护，废弃轨道交给原版道路自然衰退。</summary>
    /// <param name="tile">原版道路衰退抽中的铁轨格。</param>
    internal static bool IsMaintainedTrack(WorldTile tile)
        => (tile.building?.asset == Buildings.TrainStation && tile.building.isNormal()) ||
           RailwayTrackOccupancy.IsOccupied(tile) ||
           (tileMap.TryGetValue(tile.tile_id, out var affected) && affected.Any(l => l.State != RailwayLinkState.Revoked));

    /// <summary>登记已经完成的真实线路，供完工和运行时场景使用。</summary>
    /// <param name="a">路径起站。</param>
    /// <param name="b">路径终站。</param>
    /// <param name="tiles">包含两端且完整连续的轨道。</param>
    internal static void RegisterLink(Building a, Building b, IReadOnlyList<WorldTile> tiles)
        => ReplaceLink(new LinkInfo
        {
            A = a.id, B = b.id, Payer = a.city?.id ?? 0,
            Tiles = new List<WorldTile>(tiles), State = RailwayLinkState.Normal
        });

    /// <summary>识别免费手绘连接，旧区间仍有车时暂不替换路径。</summary>
    /// <param name="a">一端车站。</param>
    /// <param name="b">另一端车站。</param>
    /// <param name="tiles">完整实轨路径。</param>
    internal static bool RegisterDrawnLink(Building a, Building b, List<WorldTile> tiles)
    {
        if (HasProjectOrConnection(a.id, b.id)) return true;
        if (links.TryGetValue(Key(a.id, b.id), out var previous) && RailwayTrackOccupancy.IsOccupied(previous.Tiles)) return false;
        var link = new LinkInfo { A = a.id, B = b.id, Payer = a.city.id, Tiles = tiles, State = RailwayLinkState.Normal, PlayerDrawn = true };
        if (IsOperational(link)) ReplaceLink(link);
        return true;
    }

    /// <summary>玩家拆轨是明确撤销，不让自动维修立即恢复被擦掉的部分。</summary>
    /// <param name="tile">被擦除的格子。</param>
    internal static void RevokeDrawnThrough(WorldTile tile)
    {
        if (!tileMap.TryGetValue(tile.tile_id, out var affected)) return;
        foreach (var link in affected) { link.State = RailwayLinkState.Revoked; link.Reason = "玩家拆除"; }
        dirtyConnectivity = true;
        if (Config.game_loaded) PublishConnectivity();
    }

    /// <summary>仍在已拆区间内的车辆应尝试沿旧轨撤回。</summary>
    /// <param name="from">原始发站。</param>
    /// <param name="to">原目标站。</param>
    internal static bool IsRevokedInterval(long from, long to)
        => links.TryGetValue(Key(from, to), out var link) && link.State == RailwayLinkState.Revoked;

    /// <summary>重连也包含已经不再接壤的旧端点，优先修复中断而非另建近线。</summary>
    /// <param name="station">提出请求的车站。</param>
    internal static IEnumerable<long> InterruptedNeighbours(long station)
        => links.Values.Where(l => (l.A == station || l.B == station) &&
            l.State != RailwayLinkState.Construction && l.State != RailwayLinkState.Repairing &&
            (l.State != RailwayLinkState.Normal || !IsOperational(l))).Select(l => l.A == station ? l.B : l.A);

    /// <summary>替换工程时同步地块索引，不改变任何地形。</summary>
    /// <param name="link">新工程记录。</param>
    private static void ReplaceLink(LinkInfo link)
    {
        var key = Key(link.A, link.B);
        if (links.TryGetValue(key, out var old)) RemoveTileMap(old);
        links[key] = link;
        AddTileMap(link);
        dirtyConnectivity = true;
    }

    /// <summary>地形事件只处理确实受影响的线路，立即撤下失效运营连接。</summary>
    /// <param name="tile">刚发生变化的地块。</param>
    internal static void MarkTileDamaged(WorldTile tile)
    {
        if (tile == null) return;
        if (tile.neighbours?.Any(n => n?.building?.asset == Buildings.TrainStation) == true) dirtyConnectivity = true;
        if (!tileMap.TryGetValue(tile.tile_id, out var affected)) return;
        bool withdraw = false;
        foreach (var link in affected)
        {
            if (link.State == RailwayLinkState.Revoked) continue;
            if (link.State == RailwayLinkState.Normal && !ActorComponents.Train.IsTrackTile(tile))
            {
                Damage(link);
                withdraw = true;
            }
            else if (link.State == RailwayLinkState.Paused && link.Reason == "地形受阻")
            {
                link.State = RailwayLinkState.Damaged;
                link.Failures = 0;
                link.NextRepair = World.world.getCurWorldTime() + RetryDelay;
            }
        }
        if (withdraw && Config.game_loaded) PublishConnectivity();
    }

    /// <summary>车站毁坏时保留已铺轨道，撤销有关工程和运营连接。</summary>
    /// <param name="station">失效车站。</param>
    internal static void MarkStationDisabled(Building station)
    {
        RailwayFleetService.RemoveDepot(station.id);
        foreach (var link in links.Values.Where(l => l.A == station.id || l.B == station.id))
        {
            link.State = RailwayLinkState.Revoked;
            link.Reason = "车站失效";
        }
        dirtyConnectivity = true;
        if (Config.game_loaded) PublishConnectivity();
    }

    /// <summary>记录首次受损时间；反复通知不会延后维修。</summary>
    /// <param name="link">受损线路。</param>
    private static void Damage(LinkInfo link)
    {
        if (link.State != RailwayLinkState.Damaged) RailwayLog.Record("Damaged", station: link.A);
        link.State = RailwayLinkState.Damaged;
        link.Reason = "轨道受损";
        link.Failures = 0;
        link.NextRepair = World.world.getCurWorldTime() + RetryDelay;
        dirtyConnectivity = true;
    }

    /// <summary>获取完整真实区间；不删除缺口，不补造虚拟端点。</summary>
    /// <param name="from">起站编号。</param>
    /// <param name="to">终站编号。</param>
    /// <param name="path">符合实际行驶方向的路径。</param>
    internal static bool TryGetPath(long from, long to, out List<WorldTile> path)
    {
        path = null;
        if (!links.TryGetValue(Key(from, to), out var link) || link.State != RailwayLinkState.Normal ||
            !IsOperational(link)) return false;
        path = new List<WorldTile>(link.Tiles);
        if (from != link.A) path.Reverse();
        return true;
    }

    /// <summary>供城市造车判断使用真实区间，避免无车和无请求互相等待。</summary>
    /// <param name="station">车站编号。</param>
    internal static IEnumerable<long> GetPhysicalNeighbours(long station)
        => links.Values.Where(l => l.State == RailwayLinkState.Normal && IsOperational(l) &&
            (l.A == station || l.B == station)).Select(l => l.A == station ? l.B : l.A);

    /// <summary>派车和国家路线共同检查实际领土及车长。</summary>
    /// <param name="from">一端车站。</param>
    /// <param name="to">另一端车站。</param>
    /// <param name="country">车辆国家。</param>
    /// <param name="military">运兵不经过外国领土。</param>
    /// <param name="length">整列实际长度。</param>
    internal static bool CanUseInterval(long from, long to, long country, bool military, float length)
    {
        if (!links.TryGetValue(Key(from, to), out var link) || link.State != RailwayLinkState.Normal ||
            link.Tiles.Count - 1 < length || !IsOperational(link)) return false;
        var owner = World.world.kingdoms.get(country);
        return owner != null && link.Tiles.All(t => t.zone?.city?.kingdom is not { } land ||
            (military ? land == owner : RailTrackRoutePlanner.HasPermission(owner, land)));
    }

    /// <summary>同时检查站点、外交、领土、轨道和四向连续性。</summary>
    /// <param name="link">待发布或发车的线路。</param>
    private static bool IsOperational(LinkInfo link)
    {
        var a = World.world.buildings.get(link.A);
        var b = World.world.buildings.get(link.B);
        return IsStationValid(a) && IsStationValid(b) && HasPermission(link, a, b) &&
               link.Tiles.Count >= 2 && link.Tiles[0] == RailwayStationService.TrackAnchor(a) &&
               link.Tiles[link.Tiles.Count - 1] == RailwayStationService.TrackAnchor(b) &&
               link.Tiles.Select((t, i) => ActorComponents.Train.IsTrackTile(t) && RailTrackRoutePlanner.CanBuild(t, a, b) &&
                   (i == 0 || ActorComponents.Train.AreAdjacent(link.Tiles[i - 1], t))).All(x => x);
    }

    /// <summary>运营与建设共用相同的领土许可。</summary>
    /// <param name="link">完整路径。</param>
    /// <param name="a">起站。</param>
    /// <param name="b">终站。</param>
    private static bool HasPermission(LinkInfo link, Building a, Building b)
        => RailTrackRoutePlanner.HasPermission(a.kingdom, b.kingdom) &&
           link.Tiles.All(t => t.zone?.city?.kingdom is not { } owner ||
               (RailTrackRoutePlanner.HasPermission(a.kingdom, owner) &&
                RailTrackRoutePlanner.HasPermission(b.kingdom, owner)));

    /// <summary>先处理站点和许可变化，再按轮转名额推进工程，最后更新网络。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        if (!Config.game_loaded) return;
        foreach (var link in links.Values)
        {
            if (link.State == RailwayLinkState.Revoked) continue;
            var a = World.world.buildings.get(link.A);
            var b = World.world.buildings.get(link.B);
            if (!IsStationValid(a) || !IsStationValid(b))
            {
                link.State = RailwayLinkState.Revoked;
                dirtyConnectivity = true;
            }
            else if (!RailTrackRoutePlanner.HasPermission(a.kingdom, b.kingdom))
            {
                if (link.State != RailwayLinkState.Paused || link.Reason != "通行许可")
                    Pause(link, "通行许可");
            }
            else if (link.State == RailwayLinkState.Paused && link.Reason == "通行许可")
            {
                link.State = link.ResumeState;
                dirtyConnectivity = true;
            }
        }
        double now = SimulationTime.Now;
        if (now >= nextWork)
        {
            nextWork = now + WorkInterval;
            RailwayManualTrackService.Tick();
            var projects = links.Values.Where(l => l.State != RailwayLinkState.Normal &&
                l.State != RailwayLinkState.Revoked).ToArray();
            int handled = 0;
            for (int i = 0; i < projects.Length && handled < ProjectsPerTick; i++)
            {
                var link = projects[(workCursor + i) % projects.Length];
                if (link.State == RailwayLinkState.Paused)
                {
                    var payer = World.world.cities.get(link.Payer);
                    if (link.Reason != "建设资源不足" || payer == null ||
                        !payer.hasEnoughResourcesFor(Cost(Math.Max(0, Units(link.Laid + CountMissing(link.Tiles)) - link.Paid))))
                        continue;
                    link.State = link.ResumeState;
                }
                if (link.State == RailwayLinkState.Damaged)
                {
                    if (link.PlayerDrawn) continue;
                    if (now < link.NextRepair) continue;
                    TryRepair(link);
                }
                if (link.State == RailwayLinkState.Construction || link.State == RailwayLinkState.Repairing)
                    BuildBatch(link);
                handled++;
            }
            if (projects.Length > 0) workCursor = (workCursor + Math.Max(1, handled)) % projects.Length;
            foreach (var link in links.Values.Where(l => l.State == RailwayLinkState.Normal))
                if (!IsOperational(link)) Damage(link);
        }
        if (dirtyConnectivity) PublishConnectivity();
    }

    /// <summary>先检查原线可否收费补轨；绕线前必须等旧区间清空。</summary>
    /// <param name="link">已经等待到维修时刻的线路。</param>
    private static void TryRepair(LinkInfo link)
    {
        var a = World.world.buildings.get(link.A);
        var b = World.world.buildings.get(link.B);
        if (link.Tiles.Any(t => !RailTrackRoutePlanner.CanBuildWithClearance(t, a, b)))
        {
            if (RailwayTrackOccupancy.IsOccupied(link.Tiles))
            {
                link.NextRepair = SimulationTime.Now + RetryInterval;
                return;
            }
            var route = RailTrackRoutePlanner.BuildPath(a, b);
            if (route.Count < 2)
            {
                link.Failures++;
                link.NextRepair = SimulationTime.Now + RetryInterval;
                if (link.Failures >= MaxFailures) Pause(link, "地形受阻");
                return;
            }
            RemoveTileMap(link);
            link.Tiles = route;
            AddTileMap(link);
        }
        link.State = RailwayLinkState.Repairing;
        link.Cursor = link.Laid = link.Paid = 0;
        link.Budget = Units(CountMissing(link.Tiles));
        link.Reason = "";
    }

    /// <summary>一批先校验和检查余额，成功铺轨后只扣累计新增的费用。</summary>
    /// <param name="link">正在施工或维修的工程。</param>
    private static void BuildBatch(LinkInfo link)
    {
        var a = World.world.buildings.get(link.A);
        var b = World.world.buildings.get(link.B);
        var payer = World.world.cities.get(link.Payer);
        int end = Math.Min(link.Tiles.Count, link.Cursor + TilesPerBatch);
        var batch = link.Tiles.Skip(link.Cursor).Take(end - link.Cursor).ToArray();
        if (batch.Any(t => !RailTrackRoutePlanner.CanBuildWithClearance(t, a, b)))
        {
            Damage(link);
            return;
        }
        var missing = batch.Where(NeedsTrack).ToArray();
        int due = Units(link.Laid + missing.Length) - link.Paid;
        if (payer == null || !payer.hasEnoughResourcesFor(Cost(due)))
        {
            Pause(link, "建设资源不足");
            return;
        }
        foreach (var tile in batch) RailTrackRoutePlanner.ClearForTrack(tile);
        foreach (var tile in missing) tile.setTopTileType(TopTileTypes.TrainTrack);
        link.Laid += missing.Length;
        if (due > 0) payer.spendResourcesForBuildingAsset(Cost(due));
        link.Paid += due;
        link.Cursor = end;
        if (end < link.Tiles.Count) return;
        if (IsOperational(link))
        {
            RailwayLog.Record("Repaired", station: link.A);
            link.State = RailwayLinkState.Normal;
            link.Failures = 0;
            link.Reason = "";
            dirtyConnectivity = true;
        }
        else Damage(link);
    }

    /// <summary>暂停保留工程、费用和重试前状态。</summary>
    /// <param name="link">无法继续的工程。</param>
    /// <param name="reason">可读的暂停原因。</param>
    private static void Pause(LinkInfo link, string reason)
    {
        if (link.State != RailwayLinkState.Paused) link.ResumeState = link.State;
        link.State = RailwayLinkState.Paused;
        link.Reason = reason;
        dirtyConnectivity = true;
    }

    /// <summary>每八格向上取整，不为站内空间和共用轨道付费。</summary>
    /// <param name="tiles">新铺格数。</param>
    private static int Units(int tiles) => (tiles + 7) / 8;
    /// <summary>每份轨道需要木、石、普通金属各一。</summary>
    /// <param name="units">费用份数。</param>
    private static ConstructionCost Cost(int units) => new(units, units, units);
    /// <summary>车站内部使用专用路径，不改写建筑下面的地形。</summary>
    /// <param name="tile">候选轨道格。</param>
    private static bool NeedsTrack(WorldTile tile)
        => tile.building?.asset != Buildings.TrainStation && tile.top_type != TopTileTypes.TrainTrack;
    /// <summary>计算尚未铺设的格子。</summary>
    /// <param name="tiles">工程路径。</param>
    private static int CountMissing(IEnumerable<WorldTile> tiles) => tiles.Count(NeedsTrack);
    /// <summary>车站必须是仍在使用的本模组车站。</summary>
    /// <param name="station">运行时建筑。</param>
    private static bool IsStationValid(Building station)
        => station?.asset == Buildings.TrainStation && station.isNormal() && !station.isRemoved();

    /// <summary>为整条工程登记地形事件索引。</summary>
    /// <param name="link">新增工程。</param>
    private static void AddTileMap(LinkInfo link)
    {
        foreach (var tile in link.Tiles)
        {
            if (!tileMap.TryGetValue(tile.tile_id, out var set)) tileMap[tile.tile_id] = set = new();
            set.Add(link);
        }
    }

    /// <summary>改线时移除旧索引。</summary>
    /// <param name="link">旧路径工程。</param>
    private static void RemoveTileMap(LinkInfo link)
    {
        foreach (var tile in link.Tiles)
            if (tileMap.TryGetValue(tile.tile_id, out var set))
            {
                set.Remove(link);
                if (set.Count == 0) tileMap.Remove(tile.tile_id);
            }
    }

    /// <summary>只发布真实可运营区间；每个远端连接携带同一条按时间选出的站序。</summary>
    private static void PublishConnectivity()
    {
        RailwayRouteService.Clear();
        var stations = World.world.buildings.getSimpleList().Where(IsStationValid).ToList();
        foreach (var station in stations) RailwayStationService.PrepareTrackEntrance(station);
        var points = stations.ToDictionary(s => s.id, RailwayStationService.GetPassengerTile);
        stations.RemoveAll(s => points[s.id] == null);
        var stationIds = new HashSet<long>(stations.Select(s => s.id));
        foreach (var existing in PortalRegistry.Instance.Snapshot(Portals.TrainStation))
            if (!stationIds.Contains(existing.Id)) PortalRegistry.Instance.Remove(existing.Id);
        var direct = stations.ToDictionary(s => s.id, _ => new List<PortalConnection>());
        foreach (var link in links.Values.Where(l => l.State == RailwayLinkState.Normal && IsOperational(l)))
        {
            if (!stationIds.Contains(link.A) || !stationIds.Contains(link.B)) continue;
            float time = (link.Tiles.Count - 1) / TrainConfig.TrackSpeed;
            direct[link.A].Add(new PortalConnection(link.B, time));
            direct[link.B].Add(new PortalConnection(link.A, time));
        }
        RailwayRouteService.Update(direct, stations);
        foreach (var station in stations)
        {
            var portal = station.GetBuildingComponent<Portal>();
            portal.Neighbours = direct[station.id].Select(c =>
                World.world.buildings.get(c.TargetId).GetBuildingComponent<Portal>()).ToList();
            var connections = RailwayRouteService.BuildConnections(station.id);
            portal.ConnectedPortals = connections.Select(c =>
                World.world.buildings.get(c.TargetId).GetBuildingComponent<Portal>()).Distinct().ToList();
            portal.ConnectedPortals.Add(portal);
            PortalRegistry.Instance.RegisterOrUpdate(new PortalDefinition(portal, station.id, points[station.id],
                TrainConfig.StopBaseWait, 1f, connections));
        }
        dirtyConnectivity = false;
    }

    /// <summary>站点创建、易主与外交事件后请求更新网络。</summary>
    internal static void RequestRebuild() => dirtyConnectivity = true;

    /// <summary>保存每条工程一次，包含分批进度和已付款数。</summary>
    /// <param name="station">负责保存的较小编号端点。</param>
    internal static List<TrainLinkSaveData> CaptureLinks(Building station)
        => links.Values.Where(l => Math.Min(l.A, l.B) == station.id).Select(CaptureLink).ToList();

    /// <summary>查看界面和选中网络读取当前工程快照，包含暂停和已撤销的线路。</summary>
    internal static TrainLinkSaveData[] InspectLines() => links.Values.Select(CaptureLink).ToArray();

    /// <summary>将一条实际工程转为不持有世界对象的记录。</summary>
    /// <param name="l">实际工程。</param>
    private static TrainLinkSaveData CaptureLink(LinkInfo l) => new()
        {
            StationA = l.A, StationB = l.B, Payer = l.Payer, Tiles = l.Tiles.Select(t => t.tile_id).ToArray(),
            State = l.State, ResumeState = l.ResumeState, Reason = l.Reason, Cursor = l.Cursor,
            Laid = l.Laid, Paid = l.Paid, Budget = l.Budget, Failures = l.Failures,
            RetryRemaining = (float)Math.Max(0, l.NextRepair - World.world.getCurWorldTime()),
            PlayerDrawn = l.PlayerDrawn
        };

    /// <summary>恢复真实工程，不免费补轨；运营连接在占用恢复前不会发车。</summary>
    /// <param name="records">已通过存档边界检查的记录。</param>
    internal static void RestoreLinks(IEnumerable<TrainLinkSaveData> records)
    {
        foreach (var r in records)
        {
            if (!IsStationValid(World.world.buildings.get(r.StationA)) ||
                !IsStationValid(World.world.buildings.get(r.StationB))) continue;
            ReplaceLink(new LinkInfo
            {
                A = r.StationA, B = r.StationB, Payer = r.Payer,
                Tiles = r.Tiles.Select(i => World.world.tiles_list[i]).ToList(),
                State = r.State, ResumeState = r.ResumeState, Reason = r.Reason,
                Cursor = r.Cursor, Laid = r.Laid, Paid = r.Paid, Budget = r.Budget,
                Failures = r.Failures, NextRepair = World.world.getCurWorldTime() + r.RetryRemaining,
                PlayerDrawn = r.PlayerDrawn
            });
        }
        PublishConnectivity();
        RailwayManualTrackService.RequestScan();
    }

    /// <summary>清除世界引用，不改变旧地图地形或库存。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        links.Clear();
        tileMap.Clear();
        RailwayManualTrackService.Clear();
        RailwayRouteService.Clear();
        RailwayTrackOccupancy.Clear();
        nextWork = 0;
        workCursor = 0;
        dirtyConnectivity = false;
    }
}
