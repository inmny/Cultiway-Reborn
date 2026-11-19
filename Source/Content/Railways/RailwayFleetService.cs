using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Performance;

namespace Cultiway.Content.Railways;

/// <summary>城市付费造车、名额、编组库存和车辆归属的唯一入口。</summary>
internal static class RailwayFleetService
{
    /// <summary>所有已登记车头，包含运行和维修中的车辆。</summary>
    private static readonly Dictionary<long, RailwayVehicle> vehicles = new();
    /// <summary>各站未挂接车厢和造车间隔。</summary>
    private static readonly Dictionary<long, (RailwayDepotSaveData data, double nextBuild)> depots = new();
    /// <summary>低频检查城市造车与运营信息。</summary>
    private static double nextManage;
    /// <summary>每两秒检查一次返站条件，不逐帧寻找返站线路。</summary>
    private static double nextStrandedCheck;
    /// <summary>当前世界的车辆集合，不扫描世界居民。</summary>
    internal static IEnumerable<RailwayVehicle> Vehicles => vehicles.Values;

    /// <summary>实际可承担新客运的最短列车，用于发布与请求一致的站序。</summary>
    /// <param name="country">车辆国家。</param>
    internal static float PassengerLength(long country)
        => vehicles.Values.Where(v => v.Country == country && v.Body.PassengerCapacity > 0 &&
            v.Mode != RailwayVehicleMode.Stopped && v.Mode != RailwayVehicleMode.Repairing &&
            v.Actor.getHealth() >= v.Actor.getMaxHealth() * .8f).Select(v => v.Body.TrainLength)
            .DefaultIfEmpty(ActorComponents.Train.LengthForCars(2)).Min();

    /// <summary>按车头取得其长期运行状态。</summary>
    /// <param name="actor">车头。</param>
    internal static RailwayVehicle Get(Actor actor)
        => actor != null && vehicles.TryGetValue(actor.id, out var vehicle) ? vehicle : null;

    /// <summary>先处理死亡和归属，再按时间维修，最后低频补足城市客运能力。</summary>
    /// <param name="elapsed">模拟秒数。</param>
    internal static void Tick(float elapsed)
    {
        foreach (var vehicle in vehicles.Values.ToArray())
        {
            if (!vehicle.Actor.isAlive()) { Remove(vehicle.Actor); continue; }
            CheckOwnership(vehicle);
            if (vehicle.Mode != RailwayVehicleMode.Repairing) continue;
            float target = vehicle.Actor.getMaxHealth() * (vehicle.Station == vehicle.Home ? 1f : .25f);
            vehicle.RepairFraction += elapsed * vehicle.Actor.getMaxHealth() * .1f;
            int restore = Math.Min((int)vehicle.RepairFraction, Math.Max(0, (int)Math.Ceiling(target - vehicle.Actor.getHealth())));
            if (restore > 0) { vehicle.Actor.restoreHealth(restore); vehicle.RepairFraction -= restore; }
            if (vehicle.Actor.getHealth() >= target)
            { vehicle.RepairFraction = 0; SetMode(vehicle, RailwayVehicleMode.Idle); }
        }
        if (SimulationTime.Now < nextManage) return;
        nextManage = SimulationTime.Now + 2;
        foreach (var vehicle in vehicles.Values.Where(v => v.Mode == RailwayVehicleMode.Stopped &&
                     v.Station == 0 && v.Country > 0).ToArray())
            TrainTransportSystem.Instance.TryRecoverStopped(vehicle);
        foreach (var station in World.world.buildings.getSimpleList().Where(b =>
                     b.asset == Buildings.TrainStation && b.isNormal() && b.city != null))
        {
            if (!TrainTrackRepairSystem.GetPhysicalNeighbours(station.id).Any()) continue;
            if (vehicles.Values.Any(v => v.Home == station.id && v.Body.PassengerCapacity > 0)) continue;
            var parked = vehicles.Values.FirstOrDefault(v => v.Home == station.id && v.Station == station.id &&
                v.Mode == RailwayVehicleMode.Idle && v.Body.PassengerCapacity == 0);
            if (parked != null && TryConfigure(parked.Actor,
                    parked.Body.Cars.Concat(new[] { TrainCarKind.Passenger, TrainCarKind.Passenger }).ToArray())) continue;
            TryBuild(station, new[] { TrainCarKind.Passenger, TrainCarKind.Passenger });
        }
        TrainTrackRepairSystem.RequestRebuild();
    }

    /// <summary>行程推进后检查返站能力；到期先由原版死亡入口下客并清理行程，再播放无伤害爆炸。</summary>
    internal static void CheckStrandedVehicles()
    {
        double now = SimulationTime.Now;
        if (now < nextStrandedCheck) return;
        nextStrandedCheck = now + 2;
        foreach (var vehicle in vehicles.Values.ToArray())
        {
            if (TrainTransportSystem.Instance.CanReturnHome(vehicle))
            { vehicle.StrandedSince = -1; continue; }
            if (vehicle.StrandedSince < 0) vehicle.StrandedSince = now;
            if (now - vehicle.StrandedSince < TrainConfig.StrandedTimeout) continue;
            vehicle.Trade.Active = false;
            vehicle.Body.Hide();
            var tile = vehicle.Actor.current_tile;
            RailwayLog.Record("StrandedLost", vehicle.Actor, vehicle.Home);
            vehicle.Actor.die(pDestroy: true, pType: AttackType.None, pCountDeath: false);
            EffectsLibrary.spawnAtTile("fx_explosion_tiny", tile, .5f);
        }
    }

    /// <summary>成功创建完整列车才付款，全部名额在同一次逻辑操作中检查和登记。</summary>
    /// <param name="home">归属站。</param>
    /// <param name="cars">不含火车头的实际编组。</param>
    internal static RailwayVehicle TryBuild(Building home, IReadOnlyList<TrainCarKind> cars)
    {
        if (home == null || !ValidHome(home, home.kingdom?.id ?? 0) ||
            !HasSlots(home.id, cars) || !Fits(home.id, cars.Count)) return null;
        var depot = Depot(home.id);
        if (SimulationTime.Now < depot.nextBuild) return null;
        var cost = Cost(cars.Count(c => c == TrainCarKind.Passenger), cars.Count(c => c == TrainCarKind.Cargo), true);
        if (!home.city.hasEnoughResourcesFor(cost)) return null;
        var actor = World.world.units.createNewUnit(Actors.Train.id, home.current_tile);
        if (actor == null) return null;
        actor.setHomeBuilding(home);
        actor.joinCity(home.city);
        var vehicle = new RailwayVehicle
        {
            Actor = actor, Home = home.id, Country = home.kingdom.id, City = home.city.id,
            Station = home.id, Mode = RailwayVehicleMode.Idle
        };
        actor.getActorComponent<ActorComponents.Train>().SetCars(cars);
        vehicle.Body.ConfigureTrack(new[] { home.current_tile });
        vehicle.Body.Hide();
        vehicles.Add(actor.id, vehicle);
        home.city.spendResourcesForBuildingAsset(cost);
        depots[home.id] = (depot.data, SimulationTime.Now + 10);
        TrainTrackRepairSystem.RequestRebuild();
        RailwayLog.Record("Built", actor, home.id);
        return vehicle;
    }

    /// <summary>仅归属站内空载空闲时调整；拆下的车厢入库，优先复用库存。</summary>
    /// <param name="actor">已有车头。</param>
    /// <param name="cars">调整后的完整车厢顺序。</param>
    internal static bool TryConfigure(Actor actor, IReadOnlyList<TrainCarKind> cars)
    {
        var vehicle = Get(actor);
        if (vehicle == null || vehicle.Mode != RailwayVehicleMode.Idle || vehicle.Station != vehicle.Home ||
            vehicle.Body.Carrier.hasPassengers() || actor.inventory.hasResources() || vehicle.Freight.Active ||
            !HasSlots(vehicle.Home, cars, actor.id) || !Fits(vehicle.Home, cars.Count)) return false;
        var home = World.world.buildings.get(vehicle.Home);
        if (!ValidHome(home, vehicle.Country)) return false;
        var depot = Depot(home.id);
        int passengerDelta = cars.Count(c => c == TrainCarKind.Passenger) - vehicle.Body.Cars.Count(c => c == TrainCarKind.Passenger);
        int cargoDelta = cars.Count(c => c == TrainCarKind.Cargo) - vehicle.Body.Cars.Count(c => c == TrainCarKind.Cargo);
        int reusedPassenger = Math.Min(Math.Max(0, passengerDelta), depot.data.PassengerCars);
        int reusedCargo = Math.Min(Math.Max(0, cargoDelta), depot.data.CargoCars);
        var cost = Cost(Math.Max(0, passengerDelta) - reusedPassenger, Math.Max(0, cargoDelta) - reusedCargo, false);
        if (!home.city.hasEnoughResourcesFor(cost)) return false;
        vehicle.Body.SetCars(cars);
        depot.data.PassengerCars += Math.Max(0, -passengerDelta) - reusedPassenger;
        depot.data.CargoCars += Math.Max(0, -cargoDelta) - reusedCargo;
        home.city.spendResourcesForBuildingAsset(cost);
        TrainTrackRepairSystem.RequestRebuild();
        return true;
    }

    /// <summary>正在运行的空车也不能绕过车站调整入口。</summary>
    /// <param name="actor">要改变编组的车头。</param>
    internal static bool CanConfigure(Actor actor)
        => Get(actor) is not { } vehicle || (vehicle.Mode == RailwayVehicleMode.Idle &&
            vehicle.Station == vehicle.Home && !actor.inventory.hasResources() && !vehicle.Freight.Active);

    /// <summary>按实际能力占名额，混合列车同时占客运和货运名额。</summary>
    /// <param name="home">归属站编号。</param>
    /// <param name="cars">候选编组。</param>
    /// <param name="except">正在调整的车头不重复计数。</param>
    private static bool HasSlots(long home, IReadOnlyList<TrainCarKind> cars, long except = 0)
        => !vehicles.Values.Any(v => v.Home == home && v.Actor.id != except &&
            ((cars.Contains(TrainCarKind.Passenger) && v.Body.PassengerCapacity > 0) ||
             (cars.Contains(TrainCarKind.Cargo) && v.Body.CargoCapacity > 0)));

    /// <summary>至少存在一个能容纳整列车的合法出站区间。</summary>
    /// <param name="home">归属站编号。</param>
    /// <param name="cars">实际车厢数量。</param>
    private static bool Fits(long home, int cars)
        => TrainTrackRepairSystem.GetPhysicalNeighbours(home).Any(id =>
            TrainTrackRepairSystem.TryGetPath(home, id, out var path) &&
            path.Count - 1 >= ActorComponents.Train.LengthForCars(cars));

    /// <summary>车头与实际新增车厢分别计价。</summary>
    /// <param name="passenger">新造载客车厢数。</param>
    /// <param name="cargo">新造货运车厢数。</param>
    /// <param name="head">是否新造火车头。</param>
    private static ConstructionCost Cost(int passenger, int cargo, bool head)
        => new((head ? 6 : 0) + passenger * 2 + cargo * 4, 0,
            (head ? 3 : 0) + passenger + cargo * 2, (head ? 12 : 0) + passenger * 4 + cargo * 8);

    /// <summary>归属失效后保留原国家，停驶或从实际所在站申请同国接管。</summary>
    /// <param name="vehicle">已有车辆。</param>
    private static void CheckOwnership(RailwayVehicle vehicle)
    {
        var country = World.world.kingdoms.get(vehicle.Country);
        if (country == null || !country.isAlive())
        {
            vehicle.Actor.setCity(null);
            vehicle.Actor.setKingdom(World.world.kingdoms_wild.get("nature"));
            SetMode(vehicle, RailwayVehicleMode.Stopped);
            return;
        }
        if (vehicle.Actor.city != null && vehicle.Actor.city.kingdom != country) vehicle.Actor.setCity(null);
        if (vehicle.Actor.kingdom != country) vehicle.Actor.setKingdom(country);
        var currentHome = World.world.buildings.get(vehicle.Home);
        if (ValidHome(currentHome, vehicle.Country))
        {
            if (vehicle.City != currentHome.city.id || vehicle.Actor.city != currentHome.city)
            { vehicle.City = currentHome.city.id; vehicle.Actor.joinCity(currentHome.city); }
            return;
        }
        if (vehicle.Home != 0)
        {
            vehicle.Home = 0; vehicle.City = 0;
            vehicle.Actor.clearHomeBuilding(); vehicle.Actor.setCity(null);
            TrainTrackRepairSystem.RequestRebuild();
        }
        if (TrainTransportSystem.Instance.IsWorking(vehicle.Actor)) return;
        var home = World.world.buildings.get(vehicle.Station);
        if (ValidHome(home, vehicle.Country) && HasSlots(home.id, vehicle.Body.Cars, vehicle.Actor.id))
        {
            vehicle.Home = home.id; vehicle.City = home.city.id;
            vehicle.Actor.setHomeBuilding(home); vehicle.Actor.joinCity(home.city);
            SetMode(vehicle, RailwayVehicleMode.Idle);
        }
        else
        {
            var replacement = World.world.buildings.getSimpleList().Where(b => ValidHome(b, vehicle.Country) &&
                    HasSlots(b.id, vehicle.Body.Cars, vehicle.Actor.id))
                .Select(b => new { Home = b, Route = RailwayRouteService.Find(vehicle.Station, b.id,
                    vehicle.Country, false, vehicle.Body.TrainLength) }).Where(c => c.Route != null)
                .OrderBy(c => c.Route.Time).ThenBy(c => c.Home.id).FirstOrDefault();
            if (replacement == null || !TrainTransportSystem.Instance.StartEmptyMove(vehicle,
                    replacement.Home.id, RailwayVehicleMode.Returning)) SetMode(vehicle, RailwayVehicleMode.Stopped);
        }
    }

    /// <summary>仅同国且有实际城市归属的正常车站可以接管。</summary>
    /// <param name="home">候选车站。</param>
    /// <param name="country">原国家编号。</param>
    internal static bool ValidHome(Building home, long country)
        => home?.asset == Buildings.TrainStation && home.isNormal() && home.city != null && home.city.isAlive() &&
           home.kingdom.id == country && home.city.kingdom.id == country;

    /// <summary>设施毁坏时未挂接的车厢随站点损失。</summary>
    /// <param name="station">失效站点。</param>
    internal static void RemoveDepot(long station) => depots.Remove(station);

    /// <summary>状态改变时更新寻路可用性。</summary>
    /// <param name="vehicle">车辆。</param>
    /// <param name="mode">新状态。</param>
    internal static void SetMode(RailwayVehicle vehicle, RailwayVehicleMode mode)
    {
        if (vehicle.Mode == mode) return;
        vehicle.Mode = mode;
        if (mode == RailwayVehicleMode.Repairing) RailwayLog.Record("Repair", vehicle.Actor, vehicle.Station);
        TrainTrackRepairSystem.RequestRebuild();
    }

    /// <summary>实际毁车后释放名额；正常行程完成不调用此入口。</summary>
    /// <param name="actor">被移除的车头。</param>
    internal static void Remove(Actor actor)
    {
        if (!vehicles.TryGetValue(actor.id, out var vehicle)) return;
        RailwayFreightService.OnDestroyed(vehicle);
        RailwayLog.Record("Destroyed", actor);
        vehicles.Remove(actor.id);
        TrainTrackRepairSystem.RequestRebuild();
    }

    /// <summary>取得或建立本站的车厢库存。</summary>
    /// <param name="id">车站编号。</param>
    private static (RailwayDepotSaveData data, double nextBuild) Depot(long id)
    {
        if (!depots.TryGetValue(id, out var depot)) depots[id] = depot = (new RailwayDepotSaveData(), 0);
        return depot;
    }

    /// <summary>保存原国家、停车位置和维修状态。</summary>
    /// <param name="actor">正在保存的车头。</param>
    internal static RailwayVehicleSaveData Capture(Actor actor)
    {
        var vehicle = Get(actor);
        if (vehicle == null) return new RailwayVehicleSaveData { Mode = RailwayVehicleMode.Stopped };
        return new RailwayVehicleSaveData
        {
            Home = vehicle.Home, Country = vehicle.Country, City = vehicle.City, Station = vehicle.Station,
            Mode = vehicle.Mode, RepairFraction = vehicle.RepairFraction, PriorityStreak = vehicle.PriorityStreak,
            TradeRemaining = (float)Math.Max(0, vehicle.TradeAvailable - World.world.getCurWorldTime()),
            Destination = vehicle.Destination, Trade = vehicle.Trade, Freight = vehicle.Freight,
            StrandedElapsed = vehicle.StrandedSince < 0 ? 0 : (float)Math.Max(0,
                World.world.getCurWorldTime() - vehicle.StrandedSince)
        };
    }

    /// <summary>读档恢复已有车头，不重新付款或补造。</summary>
    /// <param name="actor">原版恢复的车头。</param>
    /// <param name="data">车辆状态。</param>
    internal static void Restore(Actor actor, RailwayVehicleSaveData data)
    {
        vehicles[actor.id] = new RailwayVehicle
        {
            Actor = actor, Home = data.Home, Country = data.Country, City = data.City, Station = data.Station,
            Mode = data.Mode, RepairFraction = data.RepairFraction, PriorityStreak = data.PriorityStreak,
            TradeAvailable = World.world.getCurWorldTime() + data.TradeRemaining, Destination = data.Destination,
            Trade = data.Trade, Freight = data.Freight,
            StrandedSince = data.StrandedElapsed > 0 ? World.world.getCurWorldTime() - data.StrandedElapsed : -1
        };
        var home = World.world.buildings.get(data.Home);
        if (ValidHome(home, data.Country)) actor.setHomeBuilding(home);
        if (data.Station > 0) actor.getActorComponent<ActorComponents.Train>().Hide();
    }

    /// <summary>保存本站库存及剩余造车间隔。</summary>
    /// <param name="station">归属站。</param>
    internal static RailwayDepotSaveData CaptureDepot(Building station)
    {
        var depot = Depot(station.id);
        return new RailwayDepotSaveData { PassengerCars = depot.data.PassengerCars, CargoCars = depot.data.CargoCars,
            BuildRemaining = (float)Math.Max(0, depot.nextBuild - World.world.getCurWorldTime()) };
    }

    /// <summary>恢复车站库存，不把库存计为整列运营能力。</summary>
    /// <param name="station">归属站。</param>
    /// <param name="data">已检查的库存数据。</param>
    internal static void RestoreDepot(Building station, RailwayDepotSaveData data)
        => depots[station.id] = (data, World.world.getCurWorldTime() + data.BuildRemaining);

    /// <summary>切换世界时清除旧车辆引用。</summary>
    internal static void Clear() { vehicles.Clear(); depots.Clear(); nextManage = nextStrandedCheck = 0; }
}
