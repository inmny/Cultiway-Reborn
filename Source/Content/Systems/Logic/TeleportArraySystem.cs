using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Persistence;
using Cultiway.Content.Skills;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Performance;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

public class TeleportArraySystem : BaseSystem
{
    private const int RequiredPassengers = 3;
    private const float BatchWaitTimeout = 5f;
    private const float RequestMaxAge = 12f;
    private const float EstimateWaitCost = 0.25f;
    private const float EstimateTransferCost = 0.25f;
    private const int PortalEntryIdStride = 256;

    private static bool _graphDirty = true;
    private readonly Dictionary<PortalRequest, RequestState> _states = new();
    private readonly HashSet<long> _pendingActorIds = new();
    private readonly List<PendingTeleport> _pendingTeleports = new();
    private int _worldGeneration = -1;

    public static void RequestRebuild()
    {
        _graphDirty = true;
    }

    public static bool IsTeleportArray(Building building)
    {
        return building?.asset != null && building.asset.id == Buildings.TeleportArray.id;
    }

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        if (!Config.game_loaded)
        {
            return;
        }

        ResetForNewWorld();
        if (_graphDirty)
        {
            RebuildGraph();
            _graphDirty = false;
        }

        double now = SimulationTime.Now;
        ProcessPendingTeleports(now);
        ProcessRequests(now);
    }

    private void ResetForNewWorld()
    {
        int worldGeneration = SimulationTime.Generation;
        if (_worldGeneration == worldGeneration)
        {
            return;
        }

        _worldGeneration = worldGeneration;
        _states.Clear();
        _pendingActorIds.Clear();
        foreach (PendingTeleport pending in _pendingTeleports) pending.Stance.Dispose();
        _pendingTeleports.Clear();
        _graphDirty = true;
    }

    private static void RebuildGraph()
    {
        var world = World.world;
        if (world?.buildings == null)
        {
            return;
        }

        var buildings = world.buildings.getSimpleList()
            .Where(b => IsTeleportArray(b) && b.isNormal() && !b.isRemoved() && !b.isRuin())
            .ToList();
        var portals = new List<PortalDefinition>();
        foreach (var building in buildings)
        {
            var portal = building.GetBuildingComponent<Portal>();
            if (portal == null)
            {
                portal = building.addComponent<Portal>();
            }
            portal.Asset = Portals.TeleportArray;

            var tiles = GetPortalTiles(building);
            for (var i = 0; i < tiles.Count; i++)
            {
                portals.Add(new PortalDefinition(portal, MakeEntryId(building.id, i), tiles[i], EstimateWaitCost,
                    EstimateTransferCost, Array.Empty<PortalConnection>()));
            }
        }

        var aliveIds = new HashSet<long>(portals.Select(p => p.Id));
        foreach (var existing in PortalRegistry.Instance.Snapshot(Portals.TeleportArray))
        {
            if (!aliveIds.Contains(existing.Id))
            {
                PortalRegistry.Instance.Remove(existing.Id);
            }
        }

        foreach (var entry in portals)
        {
            var targets = portals
                .Where(p => p.Portal != entry.Portal)
                .ToList();
            var connections = targets
                .Select(p => new PortalConnection(p.Id, EstimateTransferCost))
                .ToList();

            var neighbourPortals = targets.Select(p => p.Portal).Distinct().ToList();
            entry.Portal.Neighbours = neighbourPortals;
            entry.Portal.ConnectedPortals = neighbourPortals;
            PortalRegistry.Instance.RegisterOrUpdate(new PortalDefinition(entry.Portal, entry.Id, entry.Tile,
                EstimateWaitCost, EstimateTransferCost, connections));
        }
    }

    private static List<WorldTile> GetPortalTiles(Building building)
    {
        var result = new List<WorldTile>();
        var center = building?.current_tile ?? building?.getConstructionTile();
        if (building == null || center == null)
        {
            return result;
        }

        var occupied = building.tiles;
        if (occupied != null && occupied.Count > 0)
        {
            result.AddRange(occupied
                .Where(IsUsablePortalTile)
                .Distinct()
                .OrderBy(tile => Math.Abs(tile.x - center.x) + Math.Abs(tile.y - center.y))
                .ThenBy(tile => tile.data?.tile_id ?? int.MaxValue));
        }

        if (result.Count == 0 && IsUsablePortalTile(center))
        {
            result.Add(center);
        }

        return result;
    }

    private static bool IsUsablePortalTile(WorldTile tile)
    {
        var type = tile?.Type;
        return type != null && !type.block && !type.lava && !type.liquid;
    }

    private static long MakeEntryId(long buildingId, int index)
    {
        return unchecked(-(buildingId * PortalEntryIdStride + index + 1));
    }

    private void ProcessRequests(double now)
    {
        var requests = PortalManager.SnapshotRequests()
            .Where(r => r != null && !r.IsCompleted() && r.PortalType == Portals.TeleportArray)
            .ToList();
        var alive = new HashSet<PortalRequest>(requests);
        foreach (var stale in _states.Keys.Where(r => !alive.Contains(r)).ToList())
        {
            _states.Remove(stale);
        }

        foreach (var request in requests)
        {
            if (!_states.TryGetValue(request, out var state))
            {
                state = new RequestState(now);
                _states[request] = state;
            }

            ProcessRequest(request, state, now);
        }
    }

    private void ProcessRequest(PortalRequest request, RequestState state, double now)
    {
        if (request.Portals == null || request.Portals.Count == 0)
        {
            request.Cancel();
            return;
        }

        foreach (var portal in request.Portals.ToList())
        {
            if (portal?.ToLoad == null || portal.ToLoad.Count == 0)
            {
                continue;
            }

            var portalId = portal.PortalBuilding?.id ?? 0;
            if (portalId == 0)
            {
                continue;
            }

            if (!state.FirstWaitingAt.TryGetValue(portalId, out var firstWaitingAt))
            {
                firstWaitingAt = now;
                state.FirstWaitingAt[portalId] = firstWaitingAt;
            }

            var ready = portal.ToLoad.Count >= RequiredPassengers ||
                        now - firstWaitingAt >= BatchWaitTimeout ||
                        now - state.CreatedAt >= RequestMaxAge;
            if (ready)
            {
                ExecuteBatch(request, portal, now);
                state.FirstWaitingAt.Remove(portalId);
            }
        }

        if (request.Portals.All(p => (p.ToLoad == null || p.ToLoad.Count == 0) &&
                                     (p.ToUnload == null || p.ToUnload.Count == 0)))
        {
            request.Cancel();
        }
    }

    private void ExecuteBatch(PortalRequest request, PortalRequest.SinglePortal entry, double now)
    {
        foreach (var passenger in entry.ToLoad.ToList())
        {
            if (passenger == null || passenger.isRekt())
            {
                entry.RemoveLoad(passenger);
                continue;
            }

            if (_pendingActorIds.Contains(passenger.data.id))
            {
                continue;
            }

            var exit = FindUnloadPortal(request, passenger);
            var sourceTile = entry.GetLoadTile(passenger) ?? entry.PortalTile;
            var targetTile = exit?.GetUnloadTile(passenger) ?? exit?.PortalTile;
            if (exit == null || exit == entry || !IsStandingAt(passenger, sourceTile) ||
                !CanUsePortalTile(entry.PortalBuilding, sourceTile) || !CanUsePortalTile(exit.PortalBuilding, targetTile))
            {
                RemovePassengerFromRequest(request, passenger);
                if (passenger.isAlive())
                    PathFinder.Instance.TryRequestRecover(passenger, passenger.tile_target ?? passenger.current_tile);
                continue;
            }

            QueueTeleport(request, entry, exit, passenger, sourceTile, targetTile, now);
        }
    }

    private static PortalRequest.SinglePortal FindUnloadPortal(PortalRequest request, Actor passenger)
    {
        return request.Portals.FirstOrDefault(p => p?.ToUnload != null && p.ToUnload.Contains(passenger));
    }

    /// <summary>只为已经站在入口并取得站姿控制的旅客建立双端充能。</summary>
    /// <param name="request">原通行请求。</param><param name="entry">原上客端。</param><param name="exit">原下客端。</param>
    /// <param name="passenger">实际旅客。</param><param name="sourceTile">旅客当前入口。</param>
    /// <param name="targetTile">指定落点。</param><param name="now">当前世界时间。</param>
    private void QueueTeleport(PortalRequest request, PortalRequest.SinglePortal entry, PortalRequest.SinglePortal exit,
        Actor passenger, WorldTile sourceTile, WorldTile targetTile, double now)
    {
        var actorId = passenger.data.id;
        BodyControlLease stance = BodyControlState.TryAcquire(actorId, BodyOccupancy.Stance);
        if (stance == null) return;
        _pendingActorIds.Add(actorId);
        var pending = new PendingTeleport(request, entry, exit, passenger, stance, sourceTile, targetTile, now);
        _pendingTeleports.Add(pending);
        HoldPassenger(passenger);
        pending.EntryVisual = TeleportArrayConstruction.Charge(sourceTile, targetTile, actorId);
        pending.ExitVisual = TeleportArrayConstruction.Charge(targetTile, sourceTile, actorId);
    }

    private void ProcessPendingTeleports(double now)
    {
        for (var i = _pendingTeleports.Count - 1; i >= 0; i--)
        {
            var pending = _pendingTeleports[i];
            if (!IsPendingTeleportValid(pending))
            {
                RemovePendingTeleportAt(i);
                CancelPendingTeleport(pending);
                continue;
            }

            if (now >= pending.TransferAt)
            {
                RemovePendingTeleportAt(i);
                CompletePendingTeleport(pending);
                continue;
            }

            HoldPassenger(pending.Passenger);
        }
    }

    /// <summary>使用当前身份、原报名及两端实际状态确认仍可以继续充能。</summary>
    /// <param name="pending">原旅客已开始的行程。</param>
    private static bool IsPendingTeleportValid(PendingTeleport pending) =>
        pending.Passenger.data?.id == pending.ActorId && pending.Stance.IsHeld && !pending.Request.IsCompleted() &&
        pending.Request.Portals.Contains(pending.Entry) && pending.Request.Portals.Contains(pending.Exit) &&
        pending.Entry.ToLoad.Contains(pending.Passenger) && pending.Exit.ToUnload.Contains(pending.Passenger) &&
        IsStandingAt(pending.Passenger, pending.SourceTile) &&
        pending.Entry.PortalBuilding?.data?.id == pending.EntryId &&
        pending.Exit.PortalBuilding?.data?.id == pending.ExitId &&
        CanUsePortalTile(pending.Entry.PortalBuilding, pending.SourceTile) &&
        CanUsePortalTile(pending.Exit.PortalBuilding, pending.TargetTile);

    /// <summary>离开入口或已被物理作用带离地表的旅客不再搭乘。</summary>
    /// <param name="passenger">原旅客。</param><param name="source">行程上的实际入口。</param>
    private static bool IsStandingAt(Actor passenger, WorldTile source) =>
        source != null && passenger.isAlive() && passenger.current_tile == source &&
        !passenger.isFlying() && !passenger.under_forces && passenger.position_height <= 0f;

    /// <summary>核对设施仍可用，且原落脚格仍属于该阵并可通过。</summary>
    /// <param name="building">该端的原建筑。</param><param name="tile">本次选择的格子。</param>
    private static bool CanUsePortalTile(Building building, WorldTile tile) =>
        IsTeleportArray(building) && !building.isRekt() && building.isNormal() && !building.isRemoved() &&
        !building.isRuin() && IsUsablePortalTile(tile) &&
        (tile == building.current_tile || tile == building.getConstructionTile() || building.tiles?.Contains(tile) == true);

    private void CompletePendingTeleport(PendingTeleport pending)
    {
        var passenger = pending.Passenger;
        RemovePassengerFromRequest(pending.Request, passenger);

        if (PathFinder.Instance.PeekReadyStep(passenger, out var readyStep).Kind == PathPollKind.StepReady &&
            readyStep.Step.Method == MovementMethod.Portal && readyStep.Step.Entry?.Tile == pending.SourceTile &&
            readyStep.Step.Exit?.Tile == pending.TargetTile)
        {
            readyStep.Consume();
        }

        passenger.spawnOn(pending.TargetTile, 0f);
        passenger.next_step_position = passenger.current_position;
        passenger.timer_action = 0.05f;
        ActorPositionSaveService.Remember(passenger.GetExtend());
        TeleportArrayConstruction.Arrive(passenger.current_tile, pending.SourceTile, pending.ActorId);
        TryCancelCompletedRequest(pending.Request);
    }

    private static void CancelPendingTeleport(PendingTeleport pending)
    {
        var passenger = pending.Passenger;
        ConstructedAbilityService.Cancel(pending.EntryVisual);
        ConstructedAbilityService.Cancel(pending.ExitVisual);
        pending.Entry.RemovePassenger(passenger);
        pending.Exit.RemovePassenger(passenger);
        if (passenger.data?.id == pending.ActorId && passenger.isAlive())
            PathFinder.Instance.TryRequestRecover(passenger, passenger.tile_target ?? passenger.current_tile);
        TryCancelCompletedRequest(pending.Request);
    }

    private void RemovePendingTeleportAt(int index)
    {
        PendingTeleport pending = _pendingTeleports[index];
        pending.Stance.Dispose();
        _pendingActorIds.Remove(pending.ActorId);
        _pendingTeleports.RemoveAt(index);
    }

    private static void HoldPassenger(Actor passenger)
    {
        passenger.setNotMoving();
        passenger.next_step_position = passenger.current_position;
    }

    private static void RemovePassengerFromRequest(PortalRequest request, Actor passenger)
    {
        if (request?.Portals == null)
        {
            return;
        }

        foreach (var portal in request.Portals)
        {
            portal.RemovePassenger(passenger);
        }
    }

    private static void TryCancelCompletedRequest(PortalRequest request)
    {
        if (request?.Portals == null)
        {
            return;
        }

        if (request.Portals.All(p => (p.ToLoad == null || p.ToLoad.Count == 0) &&
                                     (p.ToUnload == null || p.ToUnload.Count == 0)))
        {
            request.Cancel();
        }
    }

    private sealed class RequestState
    {
        public RequestState(double createdAt)
        {
            CreatedAt = createdAt;
        }

        public double CreatedAt { get; }
        public Dictionary<long, double> FirstWaitingAt { get; } = new();
    }

    private sealed class PendingTeleport
    {
        public PendingTeleport(PortalRequest request, PortalRequest.SinglePortal entry, PortalRequest.SinglePortal exit,
            Actor passenger, BodyControlLease stance, WorldTile sourceTile, WorldTile targetTile, double now)
        {
            Request = request;
            Entry = entry;
            Exit = exit;
            Passenger = passenger;
            ActorId = passenger.id;
            EntryId = entry.PortalBuilding.id;
            ExitId = exit.PortalBuilding.id;
            Stance = stance;
            SourceTile = sourceTile;
            TargetTile = targetTile;
            TransferAt = now + TeleportArrayConstruction.ChargeSeconds;
        }

        public PortalRequest Request { get; }
        public PortalRequest.SinglePortal Entry { get; }
        public PortalRequest.SinglePortal Exit { get; }
        public Actor Passenger { get; }
        public long ActorId { get; }
        /// <summary>取入站时的建筑编号，避免复用的对象接过旧行程。</summary>
        public long EntryId { get; }
        /// <summary>取入站时指定的原出口建筑编号。</summary>
        public long ExitId { get; }
        /// <summary>只有这段充能占用旅客站姿。</summary>
        public BodyControlLease Stance { get; }
        public WorldTile SourceTile { get; }
        public WorldTile TargetTile { get; }
        public double TransferAt { get; }
        /// <summary>入口实际受理的共同画面，零表示尚未建立。</summary>
        public long EntryVisual { get; set; }
        /// <summary>出口实际受理的共同画面，零表示尚未建立。</summary>
        public long ExitVisual { get; set; }
    }
}
