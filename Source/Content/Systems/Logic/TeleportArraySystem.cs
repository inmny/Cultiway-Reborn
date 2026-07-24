using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Performance;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class TeleportArraySystem : BaseSystem
{
    private const int RequiredPassengers = 3;
    private const float BatchWaitTimeout = 5f;
    private const float RequestMaxAge = 12f;
    private const float EstimateWaitCost = 0.25f;
    private const float EstimateTransferCost = 0.25f;
    private const float TeleportChargeDuration = 0.55f;
    private const float TeleportPulseInterval = 0.18f;
    private const int PortalEntryIdStride = 256;

    private static bool _graphDirty = true;
    private readonly Dictionary<PortalRequest, RequestState> _states = new();
    private readonly HashSet<long> _pendingActorIds = new();
    private readonly List<PendingTeleport> _pendingTeleports = new();

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

        if (_graphDirty)
        {
            RebuildGraph();
            _graphDirty = false;
        }

        double now = SimulationTime.Now;
        ProcessPendingTeleports(now);
        ProcessRequests(now);
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

            var exit = FindUnloadPortal(request, passenger) ?? request.Portals.LastOrDefault();
            if (exit == null)
            {
                continue;
            }

            var targetTile = exit.GetUnloadTile(passenger) ?? exit.PortalTile ?? exit.PortalBuilding?.getConstructionTile();
            if (targetTile == null)
            {
                continue;
            }

            exit.RemoveUnload(passenger);
            QueueTeleport(request, entry, exit, passenger, targetTile, now);
        }
    }

    private static PortalRequest.SinglePortal FindUnloadPortal(PortalRequest request, Actor passenger)
    {
        return request.Portals.FirstOrDefault(p => p?.ToUnload != null && p.ToUnload.Contains(passenger));
    }

    private void QueueTeleport(PortalRequest request, PortalRequest.SinglePortal entry, PortalRequest.SinglePortal exit,
        Actor passenger, WorldTile targetTile, double now)
    {
        var sourceTile = entry.GetLoadTile(passenger) ?? passenger.current_tile ?? entry.PortalTile ??
                         entry.PortalBuilding?.getConstructionTile();
        if (sourceTile == null)
        {
            return;
        }

        var actorId = passenger.data.id;
        _pendingActorIds.Add(actorId);
        _pendingTeleports.Add(new PendingTeleport(request, entry, exit, passenger, actorId, sourceTile, targetTile, now));
        HoldPassenger(passenger, TeleportChargeDuration + 0.05f);
        SpawnChargeEffects(sourceTile, targetTile, passenger);
    }

    private void ProcessPendingTeleports(double now)
    {
        for (var i = _pendingTeleports.Count - 1; i >= 0; i--)
        {
            var pending = _pendingTeleports[i];
            if (!IsPendingTeleportValid(pending))
            {
                CancelPendingTeleport(pending);
                RemovePendingTeleportAt(i);
                continue;
            }

            HoldPassenger(pending.Passenger, 0.1f);

            if (now >= pending.TransferAt)
            {
                CompletePendingTeleport(pending);
                RemovePendingTeleportAt(i);
                continue;
            }

            if (now >= pending.NextPulseAt)
            {
                SpawnPulseEffects(pending.SourceTile, pending.TargetTile, pending.Passenger);
                pending.NextPulseAt = now + TeleportPulseInterval;
            }
        }
    }

    private static bool IsPendingTeleportValid(PendingTeleport pending)
    {
        if (pending?.Passenger == null || pending.Passenger.isRekt())
        {
            return false;
        }

        if (pending.SourceTile == null || pending.TargetTile == null)
        {
            return false;
        }

        return IsPortalBuildingAlive(pending.Entry?.PortalBuilding) &&
               IsPortalBuildingAlive(pending.Exit?.PortalBuilding);
    }

    private static bool IsPortalBuildingAlive(Building building)
    {
        return building != null && !building.isRekt() && IsTeleportArray(building) && building.isNormal();
    }

    private void CompletePendingTeleport(PendingTeleport pending)
    {
        var passenger = pending.Passenger;
        RemovePassengerFromRequest(pending.Request, passenger);

        if (PathFinder.Instance.PeekReadyStep(passenger, out var readyStep).Kind == PathPollKind.StepReady &&
            readyStep.Step.Method == MovementMethod.Portal)
        {
            readyStep.Consume();
        }

        SpawnDepartureEffects(pending.SourceTile, passenger);
        passenger.spawnOn(pending.TargetTile, 0f);
        passenger.next_step_position = passenger.current_position;
        passenger.timer_action = 0.05f;
        SpawnArrivalEffects(pending.TargetTile, passenger);
        TryCancelCompletedRequest(pending.Request);
    }

    private static void CancelPendingTeleport(PendingTeleport pending)
    {
        if (pending == null)
        {
            return;
        }

        var passenger = pending.Passenger;
        if (passenger == null || passenger.isRekt())
        {
            RemovePassengerFromRequest(pending.Request, passenger);
            TryCancelCompletedRequest(pending.Request);
            return;
        }

        RemovePassengerFromRequest(pending.Request, passenger);
        PathFinder.Instance.TryRequestRecover(passenger, passenger.tile_target ?? passenger.current_tile);
        TryCancelCompletedRequest(pending.Request);
    }

    private void RemovePendingTeleportAt(int index)
    {
        _pendingActorIds.Remove(_pendingTeleports[index].ActorId);
        _pendingTeleports.RemoveAt(index);
    }

    private static void HoldPassenger(Actor passenger, float wait)
    {
        passenger.setNotMoving();
        passenger.next_step_position = passenger.current_position;
        passenger.timer_action = Mathf.Max(passenger.timer_action, wait);
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

    private static void SpawnChargeEffects(WorldTile sourceTile, WorldTile targetTile, Actor passenger)
    {
        var scale = GetEffectScale(passenger);
        SpawnTileEffect("fx_cast_ground_blue", sourceTile, scale * 0.85f);
        SpawnTileEffect("fx_cast_top_blue", sourceTile, scale * 0.65f);
        SpawnTileEffect("fx_teleport_singularity", sourceTile, scale * 0.45f);

        SpawnTileEffect("fx_cast_ground_blue", targetTile, scale * 0.85f);
        SpawnTileEffect("fx_cast_top_blue", targetTile, scale * 0.65f);
        SpawnTileEffect("fx_teleport_singularity", targetTile, scale * 0.45f);
    }

    private static void SpawnPulseEffects(WorldTile sourceTile, WorldTile targetTile, Actor passenger)
    {
        var scale = GetEffectScale(passenger);
        SpawnTileEffect("fx_cast_top_blue", sourceTile, scale * 0.45f);
        SpawnTileEffect("fx_cast_top_blue", targetTile, scale * 0.45f);
    }

    private static void SpawnDepartureEffects(WorldTile sourceTile, Actor passenger)
    {
        var scale = GetEffectScale(passenger);
        SpawnActorTeleportEffect(passenger, passenger.current_position, scale, false);
        SpawnTileEffect("fx_teleport_singularity", sourceTile, scale * 0.5f);
    }

    private static void SpawnArrivalEffects(WorldTile targetTile, Actor passenger)
    {
        var scale = GetEffectScale(passenger);
        SpawnActorTeleportEffect(passenger, targetTile.posV3, scale, true);
        SpawnTileEffect("fx_spawn", targetTile, scale * 0.45f);
        SpawnTileEffect("fx_cast_ground_blue", targetTile, scale * 0.7f);
    }

    private static void SpawnActorTeleportEffect(Actor passenger, Vector3 position, float scale, bool arrival)
    {
        var effectId = passenger.asset?.effect_teleport;
        if (string.IsNullOrEmpty(effectId))
        {
            effectId = "fx_teleport_blue";
        }

        var effect = EffectsLibrary.spawnAt(effectId, position, scale);
        if (arrival && effect?.sprite_animation != null)
        {
            effect.sprite_animation.setFrameIndex(9);
        }
    }

    private static void SpawnTileEffect(string effectId, WorldTile tile, float scale)
    {
        if (tile == null)
        {
            return;
        }

        EffectsLibrary.spawnAtTile(effectId, tile, Mathf.Max(0.1f, scale));
    }

    private static float GetEffectScale(Actor passenger)
    {
        if (passenger?.stats == null)
        {
            return 1f;
        }

        return Mathf.Clamp(passenger.stats["scale"], 0.4f, 2.5f);
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
            Actor passenger, long actorId, WorldTile sourceTile, WorldTile targetTile, double now)
        {
            Request = request;
            Entry = entry;
            Exit = exit;
            Passenger = passenger;
            ActorId = actorId;
            SourceTile = sourceTile;
            TargetTile = targetTile;
            TransferAt = now + TeleportChargeDuration;
            NextPulseAt = now + TeleportPulseInterval;
        }

        public PortalRequest Request { get; }
        public PortalRequest.SinglePortal Entry { get; }
        public PortalRequest.SinglePortal Exit { get; }
        public Actor Passenger { get; }
        public long ActorId { get; }
        public WorldTile SourceTile { get; }
        public WorldTile TargetTile { get; }
        public double TransferAt { get; }
        public double NextPulseAt { get; set; }
    }
}
