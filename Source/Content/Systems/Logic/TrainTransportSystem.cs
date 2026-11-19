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

/// <summary>把需求分配给城市已有列车，沿真实轨道推进同一份行程。</summary>
public sealed class TrainTransportSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>当前世界的铁路调度入口。</summary>
    public static TrainTransportSystem Instance { get; private set; }
    /// <summary>正在执行的行程，一辆车只绑定一项。</summary>
    private readonly Dictionary<PortalRequest, RideState> rides = new();
    /// <summary>各站下一次允许发车的时间。</summary>
    private readonly Dictionary<long, double> stationCooldown = new();

    /// <summary>记录当前调度实例。</summary>
    public TrainTransportSystem() => Instance = this;

    /// <summary>一辆既有车辆正在执行的具体行程。</summary>
    private sealed class RideState
    {
        /// <summary>现有通用请求。</summary>
        internal PortalRequest Request;
        /// <summary>长期保留的车头。</summary>
        internal Actor Actor;
        /// <summary>实际车厢和原版承载。</summary>
        internal ActorComponents.Train Body;
        /// <summary>是否停靠车站。</summary>
        internal bool AtStation;
        /// <summary>事故后沿原轨倒退。</summary>
        internal bool Returning;
        /// <summary>当前行程的运兵规则。</summary>
        internal bool Military;
        /// <summary>军令、伤势或归属改变后在下一安全站结束载客。</summary>
        internal bool InterruptAtNextStation;
        /// <summary>客运、接客或返站用途。</summary>
        internal RailwayVehicleMode Purpose;
        /// <summary>基础停站结束时刻。</summary>
        internal double BaseDeadline;
        /// <summary>额外候车结束时刻。</summary>
        internal double HardDeadline;
        /// <summary>正在停靠或刚离开的车站。</summary>
        internal PortalRequest.SinglePortal CurrentPortal;
    }

    /// <summary>先推进已有行程，再按优先级派车；造车不依赖乘客是否已经发出请求。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        if (!Config.game_loaded) return;
        RailwayFleetService.Tick(Tick.deltaTime);
        var requests = PortalManager.SnapshotRequests();
        foreach (var state in rides.Values.ToArray())
        {
            if (state.Request.IsCompleted() || !requests.Contains(state.Request))
                Finish(state, state.Request.CompletionReason == PortalCompletionReason.None
                    ? PortalCompletionReason.Cancelled : state.Request.CompletionReason);
            else if (!state.Actor.isAlive()) Finish(state, PortalCompletionReason.Unavailable);
            else if (state.AtStation) Stop(state);
            else Travel(state);
        }
        RailwayFleetService.CheckStrandedVehicles();
        foreach (var request in PortalManager.SnapshotRequests().Where(r =>
                     r.PortalType == Portals.TrainStation && !r.IsCompleted() && r.Driver == null))
        {
            foreach (var passenger in request.Portals.SelectMany(p => p.ToLoad).Distinct().ToArray())
                if (!passenger.isAlive() || !RailwayPassengerRules.IsCommandCurrent(passenger))
                    TrainPassengerService.CancelWaiting(passenger, request);
        }
        MaintainIdleVehicles();
        Dispatch();
        RailwayTradeService.Tick();
        MapModeVisuals.RailwayMapOverlay.Tick();
    }

    /// <summary>任务完成保留车辆；低血量车优先回归属站维修。</summary>
    private void MaintainIdleVehicles()
    {
        foreach (var vehicle in RailwayFleetService.Vehicles.Where(v =>
                     v.Mode == RailwayVehicleMode.Idle && !IsWorking(v.Actor)).ToArray())
        {
            if (vehicle.Station == 0) { RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Stopped); continue; }
            if (vehicle.Actor.getHealth() >= vehicle.Actor.getMaxHealth()) continue;
            if (vehicle.Station == vehicle.Home || vehicle.Actor.getHealth() < vehicle.Actor.getMaxHealth() * .25f)
            {
                RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Repairing);
                continue;
            }
            if (vehicle.Actor.getHealth() < vehicle.Actor.getMaxHealth() * .8f)
                StartEmptyMove(vehicle, vehicle.Home, RailwayVehicleMode.Returning);
        }
    }

    /// <summary>先处理军令；连续三次后给最早普通需求一次机会，接客车按实际路程选择。</summary>
    private void Dispatch()
    {
        var waiting = PortalManager.SnapshotRequests().Where(r => r.PortalType == Portals.TrainStation &&
            !r.IsCompleted() && r.Driver == null && !r.AllowEmptyRide && r.Portals.Count > 1).ToList();
        foreach (var request in waiting.OrderByDescending(RailwayPassengerRules.IsMilitary)
                     .ThenBy(RailwayPassengerRules.QueueOrder))
        {
            long country = RailwayPassengerRules.Country(request);
            long origin = request.Portals[0].PortalBuilding.id;
            if (!IsStationAlive(request.Portals[0])) { CancelWaitingRequest(request); continue; }
            if (RailwayFleetService.Vehicles.Any(v => v.Country == country &&
                v.Mode == RailwayVehicleMode.Pickup && v.Destination == origin)) continue;
            bool military = RailwayPassengerRules.IsMilitary(request);
            bool ordinaryWaiting = waiting.Any(r => !RailwayPassengerRules.IsMilitary(r) &&
                RailwayPassengerRules.Country(r) == country && !r.IsCompleted());
            var candidates = RailwayFleetService.Vehicles.Where(v => v.Country == country &&
                v.Mode == RailwayVehicleMode.Idle && v.Station > 0 && !IsWorking(v.Actor) &&
                v.Body.PassengerCapacity > 0 && v.Actor.getHealth() >= v.Actor.getMaxHealth() * .8f &&
                (!military || v.PriorityStreak < 3 || !ordinaryWaiting))
                .Select(v => new { Vehicle = v, Route = RailwayRouteService.Find(v.Station, origin, country, false, v.Body.TrainLength) })
                .Where(c => c.Route != null && RouteFitsRequest(c.Vehicle, request, military))
                .OrderBy(c => c.Route.Time).ThenBy(c => c.Vehicle.Actor.id).ToArray();
            if (candidates.Length == 0) continue;
            var chosen = candidates[0].Vehicle;
            if (chosen.Station != origin) StartEmptyMove(chosen, origin, RailwayVehicleMode.Pickup);
            else Assign(chosen, request, RailwayVehicleMode.Passenger, military);
        }
    }

    /// <summary>实际车辆能够通过本次已承诺的每个区间。</summary>
    /// <param name="vehicle">候选车。</param>
    /// <param name="request">已按时间选好的站序。</param>
    /// <param name="military">军队许可。</param>
    private static bool RouteFitsRequest(RailwayVehicle vehicle, PortalRequest request, bool military)
        => request.Portals.Skip(1).Select((p, i) => TrainTrackRepairSystem.CanUseInterval(
            request.Portals[i].PortalBuilding.id, p.PortalBuilding.id, vehicle.Country, military, vehicle.Body.TrainLength)).All(x => x);

    /// <summary>绑定真实车辆并从实际就位时刻开始候车。</summary>
    /// <param name="vehicle">已在始发站的车辆。</param>
    /// <param name="request">本次行程。</param>
    /// <param name="purpose">车辆用途。</param>
    /// <param name="military">是否执行运兵。</param>
    private void Assign(RailwayVehicle vehicle, PortalRequest request, RailwayVehicleMode purpose, bool military = false)
    {
        var state = new RideState
        {
            Request = request, Actor = vehicle.Actor, Body = vehicle.Body, Purpose = purpose,
            Military = military, CurrentPortal = request.Portals[0]
        };
        request.Driver = vehicle.Actor;
        RailwayFreightService.OnAssigned(vehicle, request.Portals.Select(p => p.PortalBuilding.id).ToArray(), purpose);
        vehicle.Destination = request.Portals[request.Portals.Count - 1].PortalBuilding.id;
        RailwayFleetService.SetMode(vehicle, purpose);
        rides.Add(request, state);
        RailwayLog.Record("Assigned", vehicle.Actor, vehicle.Station);
        BeginStop(state);
    }

    /// <summary>无乘客也可行驶；接客、返站和实物补给共用行程、停站和占用。</summary>
    /// <param name="vehicle">当前站内车辆。</param>
    /// <param name="target">需要实际驶到的车站。</param>
    /// <param name="purpose">接客、返站或实物补给用途。</param>
    internal bool StartEmptyMove(RailwayVehicle vehicle, long target, RailwayVehicleMode purpose)
    {
        if (IsWorking(vehicle.Actor) || vehicle.Station == 0 || target == vehicle.Station) return false;
        var route = RailwayRouteService.Find(vehicle.Station, target, vehicle.Country, false, vehicle.Body.TrainLength);
        if (route == null) return false;
        var request = new PortalRequest
        {
            PortalType = Portals.TrainStation, AllowEmptyRide = true, State = PortalRequestState.WaitingPassengers,
            GroupKey = purpose == RailwayVehicleMode.Freight ? RailwayPassengerRules.CivilianGroup(vehicle.Country) : null,
            Portals = route.Stops.Select(StopAt).ToList()
        };
        PortalManager.RestoreRequest(request);
        Assign(vehicle, request, purpose);
        return true;
    }

    /// <summary>贸易往返绑定同一份实际站序，混合列车只接受沿途顺路的居民。</summary>
    /// <param name="vehicle">站内空闲列车。</param>
    /// <param name="stops">含往返在内的完整停站顺序。</param>
    internal bool StartTradeJourney(RailwayVehicle vehicle, IReadOnlyList<long> stops)
    {
        if (IsWorking(vehicle.Actor) || vehicle.Station == 0 || stops.Count < 3 || stops[0] != vehicle.Station) return false;
        var request = new PortalRequest
        {
            PortalType = Portals.TrainStation, AllowEmptyRide = true,
            GroupKey = RailwayPassengerRules.CivilianGroup(vehicle.Country),
            State = PortalRequestState.WaitingPassengers, Portals = stops.Select(StopAt).ToList()
        };
        if (!RouteFitsRequest(vehicle, request, false)) return false;
        PortalManager.RestoreRequest(request);
        Assign(vehicle, request, RailwayVehicleMode.Trade);
        return true;
    }

    /// <summary>创建真实站点的停站记录。</summary>
    /// <param name="id">车站编号。</param>
    private static PortalRequest.SinglePortal StopAt(long id)
    {
        var station = World.world.buildings.get(id);
        return new PortalRequest.SinglePortal
        {
            PortalBuilding = station, PortalTile = RailwayStationService.GetPassengerTile(station),
            ToLoad = new HashSet<Actor>(), ToUnload = new HashSet<Actor>()
        };
    }

    /// <summary>先下客并处理军令变化，再按到站先后和真实空位登车。</summary>
    /// <param name="state">正在停靠的车辆。</param>
    private void Stop(RideState state)
    {
        var request = state.Request;
        var portal = request.Portals[0];
        if (!IsStationAlive(portal)) { Finish(state, PortalCompletionReason.Unavailable); return; }
        var vehicle = RailwayFleetService.Get(state.Actor);
        state.CurrentPortal = portal;
        RailwayFreightService.OnStop(vehicle, portal.PortalBuilding, request.Portals.Select(p => p.PortalBuilding),
            state.Purpose == RailwayVehicleMode.Passenger);
        RailwayTradeService.OnStop(vehicle, portal.PortalBuilding, state.Purpose == RailwayVehicleMode.Trade);
        if ((state.Body.Carrier.hasPassengers() && !RailwayFleetService.ValidHome(World.world.buildings.get(vehicle.Home), vehicle.Country)) ||
            state.Body.Carrier.getPassengers().Any(p => p.kingdom.id != vehicle.Country ||
                !RailwayPassengerRules.IsCommandCurrent(p))) state.InterruptAtNextStation = true;
        bool injured = state.Body.Carrier.hasPassengers() && state.Actor.getHealth() < state.Actor.getMaxHealth() * .8f;
        foreach (var passenger in state.Body.Carrier.getPassengers().ToArray())
        {
            if (!portal.ToUnload.Contains(passenger) && !injured && !state.InterruptAtNextStation &&
                passenger.kingdom.id == vehicle.Country &&
                RailwayPassengerRules.IsCommandCurrent(passenger)) continue;
            bool completed = portal.ToUnload.Contains(passenger) && !injured && !state.InterruptAtNextStation &&
                             RailwayPassengerRules.IsCommandCurrent(passenger);
            request.RemovePassenger(passenger);
            TrainPassengerService.Disembark(passenger, state.Body.Carrier, portal.PortalTile, completed);
        }
        foreach (var passenger in portal.ToUnload.ToArray()) portal.RemoveUnload(passenger);
        if (injured || state.InterruptAtNextStation)
        {
            Finish(state, PortalCompletionReason.Unavailable);
            return;
        }
        JoinCompatibleWaiting(state);
        foreach (var passenger in portal.ToLoad.OrderBy(p =>
                 { p.data.get(RailwayPassengerRules.QueueKey, out long order, long.MaxValue); return order; }).ToArray())
        {
            if (!passenger.isAlive() || !RailwayPassengerRules.IsCommandCurrent(passenger) ||
                passenger.kingdom.id != vehicle.Country)
            { TrainPassengerService.CancelWaiting(passenger, request); continue; }
            if (Toolbox.SquaredDistTile(passenger.current_tile, portal.PortalTile) <= 4 &&
                TrainPassengerService.Board(passenger, state.Body)) portal.RemoveLoad(passenger);
        }
        if (request.Portals.Count == 1 && portal.ToLoad.Count == 0)
        { Finish(state, PortalCompletionReason.Arrived); return; }
        if (state.Purpose is RailwayVehicleMode.Trade or RailwayVehicleMode.Freight && !state.Body.Carrier.hasPassengers() &&
            request.Portals.All(p => p.ToLoad.Count == 0) && RailwayTradeService.HasWaitingDemand(vehicle))
        {
            if (state.Purpose == RailwayVehicleMode.Trade) RailwayTradeService.Yield(vehicle);
            Finish(state, PortalCompletionReason.Cancelled);
            return;
        }
        double now = SimulationTime.Now;
        bool full = state.Body.Carrier.countPassengers() >= state.Body.PassengerCapacity;
        if (now < state.BaseDeadline ||
            (!full && portal.ToLoad.Any(p => Toolbox.SquaredDistTile(p.current_tile, portal.PortalTile) <= 16) &&
             now < state.HardDeadline)) return;
        if (stationCooldown.TryGetValue(portal.PortalBuilding.id, out double next) && now < next) return;
        PreserveRemainingQueue(state);
        if (!request.AllowEmptyRide && !state.Body.Carrier.hasPassengers() &&
            request.Portals.Skip(1).All(p => p.ToLoad.Count == 0))
        { Finish(state, PortalCompletionReason.Arrived); return; }
        if (request.Portals.Count < 2) { Finish(state, PortalCompletionReason.Arrived); return; }
        var destination = request.Portals[1];
        if (!TrainTrackRepairSystem.CanUseInterval(portal.PortalBuilding.id, destination.PortalBuilding.id,
                vehicle.Country, state.Military, state.Body.TrainLength) ||
            !TrainTrackRepairSystem.TryGetPath(portal.PortalBuilding.id, destination.PortalBuilding.id, out var track))
        { Finish(state, PortalCompletionReason.Unavailable); return; }
        if (!RailwayTrackOccupancy.TryAcquire(state.Actor.id, track,
                state.Purpose is RailwayVehicleMode.Trade or RailwayVehicleMode.Freight && !state.Body.Carrier.hasPassengers() ? 0 : 1)) return;
        request.Portals.RemoveAt(0);
        state.Body.ConfigureTrack(track); state.Body.Show(); state.AtStation = false;
        vehicle.Station = 0;
        request.State = PortalRequestState.Driving;
        stationCooldown[portal.PortalBuilding.id] = now + TrainConfig.MinDepartInterval;
        RailwayLog.Record("Departed", state.Actor, portal.PortalBuilding.id);
    }

    /// <summary>只把本站已到位且剩余站序一致的候车者交给本车，不延长已承诺路线。</summary>
    /// <param name="state">正在安全停站的列车。</param>
    private static void JoinCompatibleWaiting(RideState state)
    {
        if (state.Purpose != RailwayVehicleMode.Passenger && state.Purpose != RailwayVehicleMode.Trade &&
            state.Purpose != RailwayVehicleMode.Freight) return;
        var current = state.Request.Portals[0];
        foreach (var waiting in PortalManager.SnapshotRequests().Where(r => r != state.Request &&
                     !r.IsCompleted() && r.Driver == null && r.PortalType == Portals.TrainStation &&
                     r.GroupKey == state.Request.GroupKey && r.Portals.Count > 1 &&
                     r.Portals[0].PortalBuilding == current.PortalBuilding).ToArray())
        {
            if (!state.Request.Portals.Take(waiting.Portals.Count).Select(p => p.PortalBuilding)
                    .SequenceEqual(waiting.Portals.Select(p => p.PortalBuilding))) continue;
            foreach (var passenger in waiting.Portals[0].ToLoad.ToArray())
            {
                if (Toolbox.SquaredDistTile(passenger.current_tile, current.PortalTile) > 4) continue;
                int destination = waiting.Portals.FindIndex(p => p.ToUnload.Contains(passenger));
                if (destination < 1 || state.Body.Carrier.countPassengers() + current.ToLoad.Count >= state.Body.PassengerCapacity) break;
                waiting.RemovePassenger(passenger);
                current.AddLoad(passenger, current.PortalTile);
                state.Request.Portals[destination].AddUnload(passenger, state.Request.Portals[destination].PortalTile);
            }
        }
    }

    /// <summary>满载后把已到站者完整留在后续队列；迟到者取消本次申请。</summary>
    /// <param name="state">即将离站的行程。</param>
    private static void PreserveRemainingQueue(RideState state)
    {
        var request = state.Request;
        var origin = request.Portals[0];
        foreach (var passenger in origin.ToLoad.ToArray())
        {
            if (Toolbox.SquaredDistTile(passenger.current_tile, origin.PortalTile) > 4)
            { TrainPassengerService.CancelWaiting(passenger, request); continue; }
            int destination = request.Portals.FindIndex(p => p.ToUnload.Contains(passenger));
            if (destination < 1) { TrainPassengerService.CancelWaiting(passenger, request); continue; }
            var target = request.Portals[destination].PortalBuilding;
            request.RemovePassenger(passenger);
            if (!PortalManager.NewRequest(origin.PortalBuilding.GetBuildingComponent<Portal>(),
                    target.GetBuildingComponent<Portal>(), passenger))
                TrainPassengerService.CancelWaiting(passenger, request);
        }
    }

    /// <summary>逐格行驶；障碍前保持原位置，无法补轨时先下客再沿原轨退回。</summary>
    /// <param name="state">运行行程。</param>
    private void Travel(RideState state)
    {
        var vehicle = RailwayFleetService.Get(state.Actor);
        var origin = state.CurrentPortal.PortalBuilding;
        var destination = state.Request.Portals[0].PortalBuilding;
        if (state.Returning)
        {
            if (origin == null || !origin.isNormal() || state.Body.HasUnsafeFooting)
            { Finish(state, PortalCompletionReason.Unavailable); return; }
            state.Body.Retreat(Tick.deltaTime);
            if (state.Body.Distance <= 0)
            {
                vehicle.Station = origin.id; state.AtStation = true;
                Finish(state, PortalCompletionReason.Unavailable);
            }
            return;
        }
        if (state.Body.HasUnsafeFooting)
        { Finish(state, PortalCompletionReason.Unavailable); return; }
        if (state.Body.Carrier.hasPassengers() &&
            !RailwayFleetService.ValidHome(World.world.buildings.get(vehicle.Home), vehicle.Country))
            state.InterruptAtNextStation = true;
        if (state.Body.Carrier.getPassengers().Any(p => p.kingdom.id != vehicle.Country ||
            !RailwayPassengerRules.IsCommandCurrent(p)))
            state.InterruptAtNextStation = true;
        if (state.Body.Carrier.hasPassengers() && state.Actor.getHealth() < state.Actor.getMaxHealth() * .25f)
        { BeginRetreat(state); return; }
        var country = World.world.kingdoms.get(vehicle.Country);
        bool permitted = country != null && destination != null && destination.isNormal() &&
            (state.Military ? destination.kingdom == country : RailTrackRoutePlanner.HasPermission(country, destination.kingdom));
        int rear = Math.Max(0, (int)(state.Body.Distance - state.Body.TrainLength));
        permitted &= country != null && state.Body.Track.Skip(rear).All(t => t.zone?.city?.kingdom is not { } land ||
            (state.Military ? land == country : RailTrackRoutePlanner.HasPermission(country, land)));
        if (!permitted) { BeginRetreat(state); return; }
        state.Body.Advance(Tick.deltaTime);
        if (state.Body.IsBlocked && (TrainTrackRepairSystem.IsRevokedInterval(origin?.id ?? 0, destination.id) ||
            state.Body.Track.Any(t => !RailTrackRoutePlanner.IsSafeGround(t) ||
            (t.building != null && t.building.asset != Buildings.TrainStation))))
        { BeginRetreat(state); return; }
        if (state.Body.IsSegmentFinished) BeginStop(state);
    }

    /// <summary>撤销本车所有上下客关联，保留车头、原轨和车上资源。</summary>
    /// <param name="state">中断行程。</param>
    private void BeginRetreat(RideState state)
    {
        var origin = state.CurrentPortal.PortalBuilding;
        var country = World.world.kingdoms.get(RailwayFleetService.Get(state.Actor).Country);
        if (origin == null || !origin.isNormal() || country == null ||
            !RailTrackRoutePlanner.HasPermission(country, origin.kingdom))
        { Finish(state, PortalCompletionReason.Unavailable); return; }
        state.Returning = true; state.Request.AllowEmptyRide = true;
        foreach (var passenger in state.Request.Portals.SelectMany(p => p.ToLoad).Distinct().ToArray())
            TrainPassengerService.CancelWaiting(passenger, state.Request);
        foreach (var passenger in state.Body.Carrier.getPassengers().ToArray()) state.Request.RemovePassenger(passenger);
        TrainPassengerService.Evacuate(state.Body.Carrier, state.Actor.current_tile);
        var vehicle = RailwayFleetService.Get(state.Actor);
        RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Returning);
    }

    /// <summary>车尾完全进入站内后释放区间，从实际抵站时刻计候车时间。</summary>
    /// <param name="state">刚抵站的行程。</param>
    private static void BeginStop(RideState state)
    {
        state.AtStation = true; state.CurrentPortal = state.Request.Portals[0];
        var vehicle = RailwayFleetService.Get(state.Actor);
        vehicle.Station = state.CurrentPortal.PortalBuilding.id;
        RailwayTrackOccupancy.Release(state.Actor.id); state.Body.Hide();
        state.Request.State = PortalRequestState.WaitingPassengers;
        state.BaseDeadline = SimulationTime.Now + TrainConfig.StopBaseWait;
        state.HardDeadline = state.BaseDeadline + TrainConfig.StopExtraWaitMax;
        RailwayLog.Record("Arrived", state.Actor, vehicle.Station);
    }

    /// <summary>结束任务并清理实际乘客，正常完成和失败都不销毁长期车辆。</summary>
    /// <param name="state">待结束行程。</param>
    /// <param name="reason">结束原因。</param>
    private void Finish(RideState state, PortalCompletionReason reason)
    {
        rides.Remove(state.Request);
        foreach (var passenger in state.Request.Portals.SelectMany(p => p.ToLoad).Distinct().ToArray())
            TrainPassengerService.CancelWaiting(passenger, state.Request);
        TrainPassengerService.Evacuate(state.Body.Carrier, state.Actor.current_tile);
        state.Request.Finish(reason); state.Request.Driver = null;
        var vehicle = RailwayFleetService.Get(state.Actor);
        if (vehicle == null) return;
        if (reason != PortalCompletionReason.Arrived) RailwayLog.Record("Cancelled", state.Actor, vehicle.Station);
        if (state.Purpose == RailwayVehicleMode.Trade) RailwayTradeService.OnJourneyFinished(vehicle);
        RailwayFreightService.OnJourneyFinished(vehicle, reason != PortalCompletionReason.Arrived);
        vehicle.Destination = 0;
        if (state.AtStation)
        {
            RailwayTrackOccupancy.Release(state.Actor.id);
            vehicle.Body.Hide();
            RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Idle);
        }
        else RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Stopped);
        if (reason == PortalCompletionReason.Arrived && state.Purpose == RailwayVehicleMode.Passenger)
            vehicle.PriorityStreak = state.Military ? vehicle.PriorityStreak + 1 : 0;
    }

    /// <summary>取消无效候车时人物留在实际位置，重新比较其他出行方式。</summary>
    /// <param name="request">尚未派车的请求。</param>
    private static void CancelWaitingRequest(PortalRequest request)
    {
        foreach (var passenger in request.Portals.SelectMany(p => p.ToLoad).Distinct().ToArray())
            TrainPassengerService.CancelWaiting(passenger, request);
        request.Finish(PortalCompletionReason.Unavailable);
    }

    /// <summary>只有实际伤害或玩家删除才释放车队名额，位移只中止原任务。</summary>
    /// <param name="train">车头。</param>
    /// <param name="beingDestroyed">原版已开始销毁。</param>
    public void CancelTrain(Actor train, bool beingDestroyed = false)
    {
        var state = rides.Values.FirstOrDefault(r => r.Actor == train);
        if (state != null) Finish(state, PortalCompletionReason.Unavailable);
        else
        {
            var body = train.getActorComponent<ActorComponents.Train>();
            if (body != null) TrainPassengerService.Evacuate(body.Carrier, train.current_tile);
        }
        RailwayTrackOccupancy.Release(train.id);
        if (beingDestroyed) RailwayFleetService.Remove(train);
        else if (RailwayFleetService.Get(train) is { } vehicle)
        {
            vehicle.Station = 0;
            vehicle.Body.ConfigureTrack(Array.Empty<WorldTile>());
            RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Stopped);
        }
    }

    /// <summary>判断已有车辆是否正在执行唯一行程。</summary>
    /// <param name="actor">车头。</param>
    internal bool IsWorking(Actor actor) => rides.Values.Any(r => r.Actor == actor);

    /// <summary>先检查归属站与停靠站；行驶中按真实进度检查能到达且可继续返站的端点。</summary>
    /// <param name="vehicle">需要判断是否长期被困的列车。</param>
    internal bool CanReturnHome(RailwayVehicle vehicle)
    {
        if (!RailwayFleetService.ValidHome(World.world.buildings.get(vehicle.Home), vehicle.Country)) return false;
        bool Connected(long station) => RailwayRouteService.Find(station, vehicle.Home, vehicle.Country,
            false, vehicle.Body.TrainLength) != null;
        if (vehicle.Station > 0) return vehicle.Station == vehicle.Home || Connected(vehicle.Station);
        if (vehicle.Mode == RailwayVehicleMode.Stopped || vehicle.Body.IsBlocked) return false;
        var track = vehicle.Body.Track;
        if (track.Count < 2) return false;
        var country = World.world.kingdoms.get(vehicle.Country);
        bool Usable(WorldTile tile) => ActorComponents.Train.IsTrackTile(tile) &&
            (tile.zone?.city?.kingdom is not { } owner || RailTrackRoutePlanner.HasPermission(country, owner));
        int front = Math.Min(track.Count, (int)Math.Ceiling(vehicle.Body.Distance) + 1);
        int rear = Math.Max(0, (int)(vehicle.Body.Distance - vehicle.Body.TrainLength));
        var first = track[0].building;
        if (first?.asset == Buildings.TrainStation && track.Take(front).All(Usable) && Connected(first.id)) return true;
        var last = track[track.Count - 1].building;
        return last?.asset == Buildings.TrainStation && track.Skip(rear).All(Usable) && Connected(last.id);
    }

    /// <summary>旧轨恢复后，从原进度驶向最近安全端点，不生成车、不改套新路径。</summary>
    /// <param name="vehicle">停在旧区间中的空车。</param>
    internal bool TryRecoverStopped(RailwayVehicle vehicle)
    {
        var track = vehicle.Body.Track;
        if (IsWorking(vehicle.Actor) || track.Count < 2 || vehicle.Body.HasUnsafeFooting ||
            vehicle.Body.Carrier.hasPassengers()) return false;
        var country = World.world.kingdoms.get(vehicle.Country);
        if (country == null || !country.isAlive()) return false;
        var first = track[0].building;
        var last = track[track.Count - 1].building;
        int rear = Math.Max(0, (int)(vehicle.Body.Distance - vehicle.Body.TrainLength));
        bool CanUse(WorldTile tile) => ActorComponents.Train.IsTrackTile(tile) &&
            (tile.zone?.city?.kingdom is not { } land || RailTrackRoutePlanner.HasPermission(country, land));
        bool backward = first?.asset == Buildings.TrainStation && first.isNormal() &&
            RailTrackRoutePlanner.HasPermission(country, first.kingdom) &&
            track.Take(Math.Min(track.Count, (int)vehicle.Body.Distance + 1)).All(CanUse);
        bool forward = last?.asset == Buildings.TrainStation && last.isNormal() &&
            RailTrackRoutePlanner.HasPermission(country, last.kingdom) && track.Skip(rear).All(CanUse);
        if (!backward && !forward) return false;
        bool reverse = backward && (!forward || vehicle.Body.Distance <
            track.Count - 1 + vehicle.Body.TrainLength - vehicle.Body.Distance);
        var target = reverse ? first : last;
        var request = new PortalRequest
        {
            PortalType = Portals.TrainStation, Driver = vehicle.Actor, AllowEmptyRide = true,
            State = PortalRequestState.Driving, Portals = new List<PortalRequest.SinglePortal> { StopAt(target.id) }
        };
        rides.Add(request, new RideState
        {
            Request = request, Actor = vehicle.Actor, Body = vehicle.Body, Returning = reverse,
            CurrentPortal = new PortalRequest.SinglePortal { PortalBuilding = first },
            Purpose = RailwayVehicleMode.Returning
        });
        vehicle.Destination = target.id;
        RailwayFleetService.SetMode(vehicle, RailwayVehicleMode.Returning);
        vehicle.Body.Show();
        PortalManager.RestoreRequest(request);
        return true;
    }

    /// <summary>中途加入只使用已有站序和真实空位，不改变运行路线。</summary>
    /// <param name="actor">已经到站的候车者。</param>
    /// <param name="request">已有请求。</param>
    /// <param name="start">上车站。</param>
    /// <param name="target">下车站。</param>
    internal bool CanJoinJourney(Actor actor, PortalRequest request, Portal start, Portal target)
    {
        if (!rides.TryGetValue(request, out var state) ||
            (state.Purpose != RailwayVehicleMode.Passenger && state.Purpose != RailwayVehicleMode.Trade &&
             state.Purpose != RailwayVehicleMode.Freight) ||
            state.Returning || RailwayFleetService.Get(state.Actor).Country != actor.kingdom.id) return false;
        var stop = request.Portals.FirstOrDefault(p => p.PortalBuilding == start.building);
        if (stop == null || Toolbox.SquaredDistTile(actor.current_tile, stop.PortalTile) > 4) return false;
        int unloading = state.Body.Carrier.getPassengers().Count(p => stop.ToUnload.Contains(p));
        int reservations = stop.ToLoad.Count(p => Toolbox.SquaredDistTile(p.current_tile, stop.PortalTile) <= 4);
        return state.Body.Carrier.countPassengers() - unloading + reservations < state.Body.PassengerCapacity;
    }

    /// <summary>对外估计同国车队到接客站的时间，包含当前任务和实际空驶。</summary>
    /// <param name="station">接客站。</param>
    /// <param name="country">乘客国家。</param>
    internal float EstimatePickup(long station, long country)
    {
        float best = float.PositiveInfinity;
        foreach (var vehicle in RailwayFleetService.Vehicles.Where(v => v.Country == country &&
                     v.Body.PassengerCapacity > 0 && v.Mode != RailwayVehicleMode.Repairing &&
                     v.Mode != RailwayVehicleMode.Stopped && v.Actor.getHealth() >= v.Actor.getMaxHealth() * .8f))
        {
            long end = vehicle.Station;
            float delay = 0;
            var ride = rides.Values.FirstOrDefault(r => r.Actor == vehicle.Actor);
            if (ride != null)
            {
                if (ride.Returning) { end = ride.CurrentPortal.PortalBuilding.id; delay = ride.Body.Distance / TrainConfig.TrackSpeed; }
                else
                {
                    end = ride.Request.Portals[ride.Request.Portals.Count - 1].PortalBuilding.id;
                    delay = ride.AtStation ? (float)Math.Max(0, ride.BaseDeadline - SimulationTime.Now) :
                        Math.Max(0, ride.Body.Track.Count - 1 + ride.Body.TrainLength - ride.Body.Distance) / TrainConfig.TrackSpeed;
                    for (int i = 0; i < ride.Request.Portals.Count - 1; i++)
                    {
                        var segment = RailwayRouteService.Find(ride.Request.Portals[i].PortalBuilding.id,
                            ride.Request.Portals[i + 1].PortalBuilding.id, country, ride.Military, ride.Body.TrainLength);
                        if (segment == null) { delay = float.PositiveInfinity; break; }
                        delay += segment.Time + TrainConfig.StopBaseWait;
                    }
                }
            }
            if (end == 0) continue;
            var pickup = RailwayRouteService.Find(end, station, country, false, vehicle.Body.TrainLength);
            if (pickup != null) best = Math.Min(best, delay + pickup.Time);
        }
        return best;
    }

    /// <summary>快照同时保存车队、任务、车厢、车尾清空和站内排队。</summary>
    /// <param name="actor">正在保存的车头。</param>
    internal TrainSaveData Capture(Actor actor)
    {
        var body = actor.getActorComponent<ActorComponents.Train>();
        var data = new TrainSaveData
        {
            Cars = body.Cars.ToArray(), Track = body.Track.Select(t => t.tile_id).ToArray(), Distance = body.Distance,
            Vehicle = RailwayFleetService.Capture(actor), QueueOrder = RailwayTrackOccupancy.GetQueueOrder(actor.id)
        };
        var state = rides.Values.FirstOrDefault(r => r.Actor == actor);
        if (state == null) return data;
        data.AtStation = state.AtStation; data.Returning = state.Returning; data.Military = state.Military;
        data.InterruptAtNextStation = state.InterruptAtNextStation; data.Purpose = state.Purpose;
        data.GroupKey = state.Request.GroupKey; data.CurrentStation = state.CurrentPortal.PortalBuilding?.id ?? 0;
        data.BaseRemaining = (float)Math.Max(0, state.BaseDeadline - World.world.getCurWorldTime());
        data.HardRemaining = (float)Math.Max(0, state.HardDeadline - World.world.getCurWorldTime());
        foreach (var portal in state.Request.Portals)
            data.Stops.Add(new TrainStopSaveData
            {
                Station = portal.PortalBuilding.id, Tile = portal.PortalTile.tile_id,
                ToLoad = portal.ToLoad.Where(a => a.isAlive()).Select(a => a.id).ToArray(),
                ToUnload = portal.ToUnload.Where(a => a.isAlive()).Select(a => a.id).ToArray()
            });
        return data;
    }

    /// <summary>原版恢复实际乘客后继续同一车头与任务，站内车只恢复停车状态。</summary>
    /// <param name="actor">已经恢复的车头。</param>
    /// <param name="data">经过边界检查的快照。</param>
    internal void Restore(Actor actor, TrainSaveData data)
    {
        if (data.Stops.Count == 0)
        {
            if (data.Vehicle.Station == 0 && data.Track.Length > 1)
                RailwayTrackOccupancy.Restore(actor.id, actor.getActorComponent<ActorComponents.Train>().Track);
            return;
        }
        var request = new PortalRequest
        {
            Driver = actor, PortalType = Portals.TrainStation, GroupKey = data.GroupKey,
            AllowEmptyRide = data.Returning || data.Purpose != RailwayVehicleMode.Passenger,
            State = data.AtStation ? PortalRequestState.WaitingPassengers : PortalRequestState.Driving,
            Portals = data.Stops.Select(s => new PortalRequest.SinglePortal
            {
                PortalBuilding = World.world.buildings.get(s.Station), PortalTile = World.world.tiles_list[s.Tile],
                ToLoad = ResolveActors(s.ToLoad), ToUnload = ResolveActors(s.ToUnload)
            }).ToList()
        };
        var origin = World.world.buildings.get(data.CurrentStation);
        var state = new RideState
        {
            Request = request, Actor = actor, Body = actor.getActorComponent<ActorComponents.Train>(),
            AtStation = data.AtStation, Returning = data.Returning, Military = data.Military,
            InterruptAtNextStation = data.InterruptAtNextStation, Purpose = data.Purpose,
            CurrentPortal = new PortalRequest.SinglePortal { PortalBuilding = origin, PortalTile = origin?.current_tile },
            BaseDeadline = World.world.getCurWorldTime() + data.BaseRemaining,
            HardDeadline = World.world.getCurWorldTime() + data.HardRemaining
        };
        if ((origin == null && data.Returning) || request.Portals.Any(p => !IsStationAlive(p)))
        { Finish(state, PortalCompletionReason.Unavailable); return; }
        rides.Add(request, state);
        if (data.AtStation)
        {
            state.Body.Hide();
            if (data.QueueOrder > 0 && request.Portals.Count > 1 &&
                TrainTrackRepairSystem.TryGetPath(origin.id, request.Portals[1].PortalBuilding.id, out var track))
                RailwayTrackOccupancy.RestoreQueue(actor.id, track, data.QueueOrder,
                    data.Purpose is RailwayVehicleMode.Trade or RailwayVehicleMode.Freight && !state.Body.Carrier.hasPassengers() ? 0 : 1);
        }
        else
        {
            state.Body.Show();
            if (!RailwayTrackOccupancy.Restore(actor.id, state.Body.Track))
            { Finish(state, PortalCompletionReason.Unavailable); return; }
        }
        PortalManager.RestoreRequest(request);
    }

    /// <summary>恢复仍存活的实际人物引用。</summary>
    /// <param name="ids">存档编号。</param>
    private static HashSet<Actor> ResolveActors(IEnumerable<long> ids)
        => new(ids.Select(id => World.world.units.get(id)).Where(a => a != null && a.isAlive()));

    /// <summary>上下客点必须仍为站外可走的安全陆地。</summary>
    /// <param name="portal">停站。</param>
    private static bool IsStationAlive(PortalRequest.SinglePortal portal)
        => portal.PortalBuilding?.asset == Buildings.TrainStation && portal.PortalBuilding.isNormal() &&
           RailTrackRoutePlanner.IsSafeGround(portal.PortalTile) && portal.PortalTile.building == null;

    /// <summary>切换世界时只清除旧引用。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        rides.Clear(); stationCooldown.Clear(); RailwayTrackOccupancy.Clear();
        RailwayFleetService.Clear(); RailwayPassengerRules.Clear();
        RailwayTradeService.Clear();
    }
}

