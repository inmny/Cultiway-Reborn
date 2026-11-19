using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Performance;

namespace Cultiway.Content.Railways;

/// <summary>商船式收益只在一次真实访问后产生，原版背包是唯一的金币载荷。</summary>
internal static class RailwayTradeService
{
    /// <summary>低频检查加挂和贸易目标。</summary>
    private static double nextManage;

    /// <summary>优先补足货运能力，再为没有可执行客运的空闲车辆安排贸易。</summary>
    internal static void Tick()
    {
        if (SimulationTime.Now < nextManage) return;
        nextManage = SimulationTime.Now + 2;
        EnsureCargoCapacity();
        RailwayFreightService.Tick();
        foreach (var vehicle in RailwayFleetService.Vehicles.Where(v => v.Mode == RailwayVehicleMode.Idle &&
                     v.Station > 0 && v.Body.CargoCapacity > 0 && !v.Freight.Active &&
                     v.Actor.getHealth() >= v.Actor.getMaxHealth() * .8f).ToArray())
        {
            var home = World.world.buildings.get(vehicle.Home);
            if (!RailwayFleetService.ValidHome(home, vehicle.Country)) continue;
            if (vehicle.Trade.Active && vehicle.Trade.Visited && !vehicle.Trade.Settled)
            {
                if (vehicle.Station == vehicle.Home) Settle(vehicle);
                else if (!HasWaitingDemand(vehicle)) TrainTransportSystem.Instance.StartEmptyMove(vehicle,
                    vehicle.Home, RailwayVehicleMode.Trade);
                continue;
            }
            if (HasWaitingDemand(vehicle) || SimulationTime.Now < vehicle.TradeAvailable || vehicle.Actor.inventory.hasResources()) continue;
            if (vehicle.Station != vehicle.Home)
            {
                TrainTransportSystem.Instance.StartEmptyMove(vehicle, vehicle.Home, RailwayVehicleMode.Returning);
                continue;
            }
            var target = Targets(home, vehicle.Country, vehicle.Body.TrainLength)
                .Select(t => new { Station = t, Route = RailwayRouteService.Find(home.id, t.id,
                    vehicle.Country, false, vehicle.Body.TrainLength), Visits = Visits(vehicle, t.id),
                    ReturnTime = RailwayRouteService.Find(t.id, home.id, vehicle.Country, false, vehicle.Body.TrainLength).Time })
                .OrderBy(t => t.Visits).ThenBy(t => t.Route.Time + t.ReturnTime).ThenBy(t => t.Station.id).FirstOrDefault();
            if (target == null) continue;
            var outbound = target.Route;
            var returning = RailwayRouteService.Find(target.Station.id, home.id, vehicle.Country, false, vehicle.Body.TrainLength);
            var stops = outbound.Stops.Concat(returning.Stops.Skip(1)).ToArray();
            if (!TrainTransportSystem.Instance.StartTradeJourney(vehicle, stops)) continue;
            vehicle.Trade.Trip++;
            vehicle.Trade.Target = target.Station.id; vehicle.Trade.OriginCity = home.city.id;
            vehicle.Trade.Country = vehicle.Country; vehicle.Trade.Active = true;
            vehicle.Trade.Visited = false; vehicle.Trade.Settled = false; vehicle.Trade.SettlementCity = 0;
        }
    }

    /// <summary>有客运能力和合法贸易目标后才增加货运能力，优先加挂并复用本站库存。</summary>
    private static void EnsureCargoCapacity()
    {
        foreach (var home in World.world.buildings.getSimpleList().Where(b =>
                     b.asset == Buildings.TrainStation && b.isNormal() && b.city != null))
        {
            var passenger = RailwayFleetService.Vehicles.FirstOrDefault(v => v.Home == home.id &&
                v.Country == home.kingdom.id && v.Body.PassengerCapacity > 0 &&
                v.Mode != RailwayVehicleMode.Stopped && v.Mode != RailwayVehicleMode.Repairing);
            if (passenger == null || RailwayFleetService.Vehicles.Any(v => v.Home == home.id && v.Body.CargoCapacity > 0) ||
                !Targets(home, home.kingdom.id, ActorComponents.Train.LengthForCars(1)).Any()) continue;
            if (passenger.Mode == RailwayVehicleMode.Idle && passenger.Station == home.id &&
                RailwayFleetService.TryConfigure(passenger.Actor, passenger.Body.Cars.Concat(new[] { TrainCarKind.Cargo }).ToArray())) continue;
            if (passenger.Mode == RailwayVehicleMode.Idle && passenger.Station != home.id && !HasWaitingDemand(passenger) &&
                TrainTransportSystem.Instance.StartEmptyMove(passenger, home.id, RailwayVehicleMode.Returning)) continue;
            RailwayFleetService.TryBuild(home, new[] { TrainCarKind.Cargo });
        }
    }

    /// <summary>目标必须属于另一个城市，且完整往返都具有当前国家许可。</summary>
    /// <param name="home">结算归属站。</param>
    /// <param name="country">车辆国家。</param>
    /// <param name="length">实际整列长度。</param>
    private static IEnumerable<Building> Targets(Building home, long country, float length)
        => World.world.buildings.getSimpleList().Where(b => b.asset == Buildings.TrainStation && b.isNormal() &&
            b.city != null && b.city != home.city && RailTrackRoutePlanner.HasPermission(home.kingdom, b.kingdom) &&
            RailwayRouteService.Find(home.id, b.id, country, false, length) != null &&
            RailwayRouteService.Find(b.id, home.id, country, false, length) != null);

    /// <summary>只有当前车辆能执行的候车需求才阻止本车贸易。</summary>
    /// <param name="vehicle">准备发车的列车。</param>
    internal static bool HasWaitingDemand(RailwayVehicle vehicle)
        => vehicle.Body.PassengerCapacity > 0 && PortalManager.SnapshotRequests().Any(r => r.PortalType == Portals.TrainStation && !r.IsCompleted() &&
            r.Driver == null && !r.AllowEmptyRide && r.Portals.Count > 1 &&
            RailwayPassengerRules.Country(r) == vehicle.Country &&
            RailwayRouteService.Find(vehicle.Station, r.Portals[0].PortalBuilding.id,
                vehicle.Country, false, vehicle.Body.TrainLength) != null &&
            r.Portals.Skip(1).Select((p, i) => TrainTrackRepairSystem.CanUseInterval(
                r.Portals[i].PortalBuilding.id, p.PortalBuilding.id, vehicle.Country,
                RailwayPassengerRules.IsMilitary(r), vehicle.Body.TrainLength)).All(x => x));

    /// <summary>停靠目标时产生一次收益，停靠当前归属站时尝试原版城市入库。</summary>
    /// <param name="vehicle">正在停站的列车。</param>
    /// <param name="station">实际停靠站。</param>
    /// <param name="trading">当前是否沿既定贸易站序行驶。</param>
    internal static void OnStop(RailwayVehicle vehicle, Building station, bool trading)
    {
        var trade = vehicle.Trade;
        if (!trade.Active || trade.Country != vehicle.Country) return;
        if (trading && !trade.Visited && station.id == trade.Target && station.city != null &&
            station.city.id != trade.OriginCity && RailTrackRoutePlanner.HasPermission(vehicle.Actor.kingdom, station.kingdom) &&
            vehicle.Body.CargoCapacity - CargoLoad(vehicle.Actor) >= 5)
        {
            vehicle.Actor.addToInventory("gold", 5);
            trade.Visited = true;
            trade.Visits[station.id] = Visits(vehicle, station.id) + 1;
        }
        if (trade.Visited && !trade.Settled && station.id == vehicle.Home) Settle(vehicle);
    }

    /// <summary>使用原版城市收资源入口，并只移除实际接收的数量，空间不足则留在车上。</summary>
    /// <param name="vehicle">带回收益的列车。</param>
    private static void Settle(RailwayVehicle vehicle)
    {
        var home = World.world.buildings.get(vehicle.Home);
        if (!RailwayFleetService.ValidHome(home, vehicle.Country) || vehicle.Station != home.id || !home.city.hasStockpiles()) return;
        int gold = Gold(vehicle.Actor);
        if (gold > 0)
        {
            int accepted = RailwayFreightService.Deposit(home.city, "gold", gold);
            if (accepted > 0) vehicle.Actor.takeFromInventory("gold", accepted);
            if (accepted > 0) RailwayLog.Record("Settled", vehicle.Actor, home.id);
        }
        if (Gold(vehicle.Actor) > 0) return;
        vehicle.Trade.Settled = true; vehicle.Trade.Active = false;
        vehicle.Trade.SettlementCity = home.city.id;
        vehicle.TradeAvailable = SimulationTime.Now + 10;
    }

    /// <summary>去程中断不产生收益；已交易的背包继续等待实际返站。</summary>
    /// <param name="vehicle">中断或完成任务的列车。</param>
    internal static void OnJourneyFinished(RailwayVehicle vehicle)
    {
        if (!vehicle.Trade.Active || vehicle.Trade.Visited) return;
        vehicle.Trade.Active = false; vehicle.TradeAvailable = SimulationTime.Now + 10;
    }

    /// <summary>安全停站后可为更优先任务让车，车上收益不结算、不丢弃。</summary>
    /// <param name="vehicle">停站的贸易车辆。</param>
    internal static void Yield(RailwayVehicle vehicle)
    {
        if (!vehicle.Trade.Visited) { vehicle.Trade.Active = false; vehicle.TradeAvailable = SimulationTime.Now + 10; }
    }

    /// <summary>原版背包中的实际金币数量。</summary>
    /// <param name="actor">车头。</param>
    internal static int Gold(Actor actor)
        => actor.inventory.hasResources() && actor.inventory.getResources().TryGetValue("gold", out var gold) ? gold.amount : 0;

    /// <summary>所有实际资源共同占用货运车厢容量。</summary>
    /// <param name="actor">车头。</param>
    internal static int CargoLoad(Actor actor)
        => actor.inventory.hasResources() ? actor.inventory.getResources().Values.Sum(v => v.amount) : 0;

    /// <summary>按实际访问次数选择较少服务的目标。</summary>
    /// <param name="vehicle">车辆。</param>
    /// <param name="target">目标站。</param>
    private static int Visits(RailwayVehicle vehicle, long target)
        => vehicle.Trade.Visits.TryGetValue(target, out int count) ? count : 0;

    /// <summary>清世界时清除调度时刻。</summary>
    internal static void Clear() => nextManage = 0;
}

/// <summary>交易动作和访问历史；待入库金额始终只存在原版背包中。</summary>
internal sealed class RailwayTradeSaveData
{
    /// <summary>本车递增交易编号。</summary>
    public long Trip;
    /// <summary>目标站。</summary>
    public long Target;
    /// <summary>起始城市。</summary>
    public long OriginCity;
    /// <summary>发起时的车辆国家。</summary>
    public long Country;
    /// <summary>本次仍有交易或结算动作。</summary>
    public bool Active;
    /// <summary>已经完成一次外站停留并生成收益。</summary>
    public bool Visited;
    /// <summary>本次收益已经入库。</summary>
    public bool Settled;
    /// <summary>真正接收收益的城市。</summary>
    public long SettlementCity;
    /// <summary>各目标实际访问次数。</summary>
    public Dictionary<long, int> Visits = new();
}
