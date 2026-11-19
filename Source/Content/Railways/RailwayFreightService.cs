using System;
using System.Collections.Generic;
using System.Linq;
using ai.behaviours;
using Cultiway.Core.Performance;

namespace Cultiway.Content.Railways;

/// <summary>在现有调度中预留、装卸同国建设材料，原版背包始终保存唯一的实物数量。</summary>
internal static class RailwayFreightService
{
    /// <summary>首批可运材料及装车时的稳定顺序。</summary>
    internal static readonly string[] Resources = { "wood", "stone", "common_metals" };

    /// <summary>一个已选出的装货、收货组合，不另建运行中的运输任务。</summary>
    private sealed class Plan
    {
        /// <summary>装货站。</summary>
        internal Building Source;
        /// <summary>收货站。</summary>
        internal Building Target;
        /// <summary>按真实空位分配的数量。</summary>
        internal Dictionary<string, int> Amounts;
        /// <summary>接货加送货的运行时间。</summary>
        internal float Time;
    }

    /// <summary>复用原版可建建筑的判断，保留两份建设预算，最低为木四十、石四十、金属二十。</summary>
    /// <param name="city">计算建设储备的城市。</param>
    internal static ConstructionCost GetTarget(City city)
    {
        var cost = new ConstructionCost(40, 40, 20);
        var template = AssetManager.city_build_orders.get(city.getActorAsset().build_order_template_id);
        foreach (var order in template.list)
        {
            if (!CityBehBuild.canUseBuildAsset(order, city)) continue;
            var build = order.getBuildingAsset(city).cost;
            cost.wood = Math.Max(cost.wood, build.wood * 2);
            cost.stone = Math.Max(cost.stone, build.stone * 2);
            cost.common_metals = Math.Max(cost.common_metals, build.common_metals * 2);
        }
        return cost;
    }

    /// <summary>读取一项材料的建设保有量。</summary>
    /// <param name="target">同一次检查计算出的建设预算。</param>
    /// <param name="resource">首批建设材料。</param>
    internal static int TargetAmount(ConstructionCost target, string resource)
        => resource == "wood" ? target.wood : resource == "stone" ? target.stone : target.common_metals;

    /// <summary>尚未实际扣货的其他车辆占用的源城资源。</summary>
    /// <param name="city">源城编号。</param>
    /// <param name="resource">材料编号。</param>
    /// <param name="except">当前车辆不重复扣自己的预留。</param>
    internal static int Reserved(long city, string resource, RailwayVehicle except = null)
        => RailwayFleetService.Vehicles.Where(v => v != except && v.Freight.State == RailwayFreightState.Reserved &&
            v.Freight.SourceCity == city).Sum(v => v.Freight.Amounts.TryGetValue(resource, out int n) ? n : 0);

    /// <summary>已预留和已装车的补给都占接收城缺口，退货不占建设补给需求。</summary>
    /// <param name="city">目标城编号。</param>
    /// <param name="resource">材料编号。</param>
    /// <param name="except">当前车辆不重复扣自己的在途量。</param>
    internal static int Incoming(long city, string resource, RailwayVehicle except = null)
        => RailwayFleetService.Vehicles.Where(v => v != except && v.Freight.Active && !v.Freight.ReturningCargo &&
            v.Freight.TargetCity == city).Sum(v => v.Freight.Amounts.TryGetValue(resource, out int n) ? n : 0);

    /// <summary>先撤销失效预留，再继续已有补给，最后为无客运任务的空车安排实物运输。</summary>
    internal static void Tick()
    {
        var stations = World.world.buildings.getSimpleList().Where(b => b.asset == Buildings.TrainStation &&
            b.isNormal() && b.city != null && b.city.isAlive()).OrderBy(b => b.id).ToArray();
        var targets = new Dictionary<long, ConstructionCost>();
        ConstructionCost Levels(City city)
        {
            if (!targets.TryGetValue(city.id, out var value)) targets[city.id] = value = GetTarget(city);
            return value;
        }
        foreach (var vehicle in RailwayFleetService.Vehicles.OrderBy(v => v.Actor.id).ToArray())
        {
            var freight = vehicle.Freight;
            if (freight.State == RailwayFreightState.Reserved &&
                (!ValidStation(freight.SourceStation, freight.SourceCity, freight.Country) ||
                 !ValidStation(freight.TargetStation, freight.TargetCity, freight.Country) || freight.Country != vehicle.Country))
                CancelReservation(vehicle, "OwnershipChanged");
            else if (freight.State == RailwayFreightState.Loaded &&
                     !ValidStation(freight.TargetStation, freight.TargetCity, freight.Country))
            { freight.TargetCity = freight.TargetStation = 0; freight.Reason = "OwnershipChanged"; }
            if (vehicle.Mode != RailwayVehicleMode.Idle || vehicle.Station == 0 ||
                TrainTransportSystem.Instance.IsWorking(vehicle.Actor) || vehicle.Actor.getHealth() < vehicle.Actor.getMaxHealth() * .8f) continue;
            if (freight.Active)
            { if (!RailwayTradeService.HasWaitingDemand(vehicle)) Continue(vehicle, stations); continue; }
            if (!CanPlan(vehicle) || RailwayTradeService.HasWaitingDemand(vehicle)) continue;
            var plan = FindPlan(vehicle, stations, Levels);
            if (plan == null) continue;
            Reserve(vehicle, plan);
            long next = vehicle.Station == plan.Source.id ? plan.Target.id : plan.Source.id;
            if (!TrainTransportSystem.Instance.StartEmptyMove(vehicle, next, RailwayVehicleMode.Freight))
                CancelReservation(vehicle, "NoRoute");
        }
    }

    /// <summary>按装运量、运行时间和站点编号选定一次补给，计算中不改库存。</summary>
    /// <param name="vehicle">站内空车。</param>
    /// <param name="stations">本轮站点快照。</param>
    /// <param name="levels">本轮按城市复用的保有量。</param>
    private static Plan FindPlan(RailwayVehicle vehicle, Building[] stations, Func<City, ConstructionCost> levels)
    {
        Plan best = null;
        foreach (var source in stations.Where(s => s.kingdom.id == vehicle.Country))
        foreach (var target in stations.Where(s => s.kingdom.id == vehicle.Country && s.city != source.city))
        {
            var amounts = PlanAmounts(vehicle, source.city, target.city, levels(source.city), levels(target.city));
            if (amounts.Count == 0) continue;
            var pickup = Route(vehicle, vehicle.Station, source.id);
            var delivery = Route(vehicle, source.id, target.id);
            if (pickup == null || delivery == null) continue;
            float time = pickup.Time + delivery.Time;
            if (best != null && (best.Amounts.Values.Sum() > amounts.Values.Sum() ||
                (best.Amounts.Values.Sum() == amounts.Values.Sum() && best.Time <= time))) continue;
            best = new Plan { Source = source, Target = target, Amounts = amounts, Time = time };
        }
        return best;
    }

    /// <summary>只为源城超出保有量、目标未被其他车辆覆盖的缺口分配真实货位。</summary>
    /// <param name="vehicle">候选列车。</param>
    /// <param name="source">源城。</param>
    /// <param name="target">目标城。</param>
    /// <param name="sourceLevel">源城保有量。</param>
    /// <param name="targetLevel">目标城补给目标。</param>
    /// <param name="requested">实际装车时只校正原预留的材料，不被新出现的其他需求占走货位。</param>
    private static Dictionary<string, int> PlanAmounts(RailwayVehicle vehicle, City source, City target,
        ConstructionCost sourceLevel, ConstructionCost targetLevel, IReadOnlyDictionary<string, int> requested = null)
    {
        var result = new Dictionary<string, int>();
        int free = vehicle.Body.CargoCapacity - RailwayTradeService.CargoLoad(vehicle.Actor);
        foreach (string resource in Resources)
        {
            if (!HasRoom(target, resource)) continue;
            int supply = source.getResourcesAmount(resource) - TargetAmount(sourceLevel, resource) - Reserved(source.id, resource, vehicle);
            int need = TargetAmount(targetLevel, resource) - target.getResourcesAmount(resource) - Incoming(target.id, resource, vehicle);
            int amount = Math.Min(free, Math.Min(supply, need));
            if (requested != null) amount = requested.TryGetValue(resource, out int reserved) ? Math.Min(amount, reserved) : 0;
            if (amount <= 0) continue;
            result[resource] = amount; free -= amount;
        }
        return result;
    }

    /// <summary>一辆车只保留一份预留；数量索引由当前车队记录直接得出。</summary>
    /// <param name="vehicle">承运车。</param>
    /// <param name="plan">已经核对过线路的补给。</param>
    private static void Reserve(RailwayVehicle vehicle, Plan plan)
    {
        vehicle.Freight = new RailwayFreightSaveData
        {
            State = RailwayFreightState.Reserved, SourceStation = plan.Source.id, SourceCity = plan.Source.city.id,
            TargetStation = plan.Target.id, TargetCity = plan.Target.city.id, Country = vehicle.Country, Amounts = plan.Amounts
        };
        RailwayLog.Record("FreightReserved", vehicle.Actor, plan.Source.id);
    }

    /// <summary>到站先卸已有货物，再按原有客运站序接顺路补给，最后在源站实际扣货。</summary>
    /// <param name="vehicle">已经实际停站的车。</param>
    /// <param name="station">实际车站。</param>
    /// <param name="remaining">已承诺的剩余停站顺序，空值表示仅继续旧补给。</param>
    /// <param name="passengerJourney">是否为已确定站序的客运。</param>
    internal static void OnStop(RailwayVehicle vehicle, Building station, IEnumerable<Building> remaining = null, bool passengerJourney = false)
    {
        var data = vehicle.Freight;
        if (data.State == RailwayFreightState.Loaded && station.id == data.TargetStation &&
            ValidStation(station.id, data.TargetCity, data.Country) && data.Country == vehicle.Country)
            Unload(vehicle, station.city);
        if (!vehicle.Freight.Active && passengerJourney && station.city != null && station.kingdom.id == vehicle.Country &&
            station.city.kingdom.id == vehicle.Country && CanPlan(vehicle))
        {
            var sourceLevel = GetTarget(station.city);
            foreach (var target in remaining.Skip(1).Where(t => t.city != null && t.city != station.city && t.kingdom.id == vehicle.Country))
            {
                var amounts = PlanAmounts(vehicle, station.city, target.city, sourceLevel, GetTarget(target.city));
                if (amounts.Count == 0) continue;
                Reserve(vehicle, new Plan { Source = station, Target = target, Amounts = amounts });
                break;
            }
        }
        data = vehicle.Freight;
        if (data.State != RailwayFreightState.Reserved || station.id != data.SourceStation) return;
        if (!ValidStation(station.id, data.SourceCity, data.Country) ||
            !ValidStation(data.TargetStation, data.TargetCity, data.Country) || data.Country != vehicle.Country)
        { CancelReservation(vehicle, "OwnershipChanged"); return; }
        var targetCity = World.world.cities.get(data.TargetCity);
        var quantities = PlanAmounts(vehicle, station.city, targetCity, GetTarget(station.city), GetTarget(targetCity), data.Amounts);
        int loaded = 0;
        foreach (string resource in data.Amounts.Keys.ToArray())
        {
            int amount = quantities.TryGetValue(resource, out int possible) ? Math.Min(data.Amounts[resource], possible) : 0;
            int before = station.city.getResourcesAmount(resource);
            if (amount > 0) station.city.takeResource(resource, amount);
            int taken = before - station.city.getResourcesAmount(resource);
            if (taken > 0) { vehicle.Actor.addToInventory(resource, taken); data.Amounts[resource] = taken; loaded += taken; }
            else data.Amounts.Remove(resource);
        }
        if (loaded == 0) { CancelReservation(vehicle, "StockChanged"); return; }
        data.State = RailwayFreightState.Loaded; data.Reason = "";
        RailwayLog.Record("FreightLoaded", vehicle.Actor, station.id);
    }

    /// <summary>只把城市实际接收的数量从原版背包扣掉，满仓或缺口已补齐时保留余货。</summary>
    /// <param name="vehicle">实际停站的载货车。</param>
    /// <param name="city">经过国家及城市编号核对的收货城。</param>
    private static void Unload(RailwayVehicle vehicle, City city)
    {
        var data = vehicle.Freight;
        var level = GetTarget(city);
        foreach (string resource in data.Amounts.Keys.ToArray())
        {
            int actual = Math.Min(data.Amounts[resource], vehicle.Actor.inventory.getResource(resource));
            int need = data.ReturningCargo ? actual : Math.Max(0, TargetAmount(level, resource) -
                city.getResourcesAmount(resource) - Incoming(city.id, resource, vehicle));
            int accepted = Math.Min(actual, need);
            if (accepted > 0) accepted = Deposit(city, resource, accepted);
            if (accepted > 0)
            {
                vehicle.Actor.takeFromInventory(resource, accepted); data.Unloaded += accepted; data.ReceivedCity = city.id;
                RailwayLog.Record("FreightUnloaded", vehicle.Actor, data.TargetStation);
            }
            int left = actual - accepted;
            if (left > 0) data.Amounts[resource] = left;
            else data.Amounts.Remove(resource);
        }
        if (data.Amounts.Count > 0) { data.Reason = "StorageOrDemand"; return; }
        data.State = RailwayFreightState.Delivered; data.Reason = "";
        vehicle.TradeAvailable = SimulationTime.Now + 10;
    }

    /// <summary>保留实际货物，优先原目标，再改送有需求的同国城，最后退回源城或归属站。</summary>
    /// <param name="vehicle">结束上一段行程的空闲车。</param>
    /// <param name="stations">本轮可用车站。</param>
    private static void Continue(RailwayVehicle vehicle, Building[] stations)
    {
        var data = vehicle.Freight;
        if (data.State == RailwayFreightState.Reserved)
        {
            long next = vehicle.Station == data.SourceStation ? data.TargetStation : data.SourceStation;
            if (!TrainTransportSystem.Instance.StartEmptyMove(vehicle, next, RailwayVehicleMode.Freight))
                CancelReservation(vehicle, "NoRoute");
            return;
        }
        var target = stations.FirstOrDefault(s => s.id == data.TargetStation && s.city.id == data.TargetCity &&
            s.kingdom.id == vehicle.Country && (data.ReturningCargo || NeedsCargo(vehicle, s.city)) && Route(vehicle, vehicle.Station, s.id) != null);
        if (target == null)
        {
            target = stations.Where(s => s.kingdom.id == vehicle.Country && NeedsCargo(vehicle, s.city))
                .Select(s => new { Station = s, Route = Route(vehicle, vehicle.Station, s.id) }).Where(s => s.Route != null)
                .OrderBy(s => s.Route.Time).ThenBy(s => s.Station.id).Select(s => s.Station).FirstOrDefault();
            data.ReturningCargo = false;
            if (target == null)
            {
                target = stations.Where(s => s.kingdom.id == vehicle.Country && (s.id == data.SourceStation || s.id == vehicle.Home) &&
                    Route(vehicle, vehicle.Station, s.id) != null).OrderBy(s => s.id != data.SourceStation).FirstOrDefault();
                data.ReturningCargo = target != null;
            }
            if (target == null) { data.Reason = "NoRoute"; return; }
            data.TargetStation = target.id; data.TargetCity = target.city.id;
            data.Reason = data.ReturningCargo ? "Returning" : "Redirected";
        }
        if (vehicle.Station == target.id) OnStop(vehicle, target);
        else TrainTransportSystem.Instance.StartEmptyMove(vehicle, target.id, RailwayVehicleMode.Freight);
    }

    /// <summary>当前城仍缺少本车实际携带的至少一种材料。</summary>
    /// <param name="vehicle">载货车。</param>
    /// <param name="city">候选接收城。</param>
    private static bool NeedsCargo(RailwayVehicle vehicle, City city)
    {
        var level = GetTarget(city);
        return vehicle.Freight.Amounts.Any(p => vehicle.Actor.inventory.getResource(p.Key) > 0 &&
            TargetAmount(level, p.Key) > city.getResourcesAmount(p.Key) + Incoming(city.id, p.Key, vehicle));
    }

    /// <summary>沿用原版资源硬上限；采集工作的停止阈值不作为运输仓库容量。</summary>
    /// <param name="city">候选收货城。</param>
    /// <param name="resource">原版资源编号。</param>
    private static bool HasRoom(City city, string resource)
        => city.stockpiles.Any(s => s.isUsable() && s.getResourcesAmount(resource) < AssetManager.resources.get(resource).maximum);

    /// <summary>逐个可用仓库入库并用前后差额计数；原版增加资源的返回值是新总量，不是接收数量。</summary>
    /// <param name="city">实际收货城市。</param>
    /// <param name="resource">入库资源。</param>
    /// <param name="amount">背包中可交付的数量。</param>
    internal static int Deposit(City city, string resource, int amount)
    {
        int received = 0;
        int limit = AssetManager.resources.get(resource).maximum;
        foreach (var stock in city.stockpiles)
        {
            if (!stock.isUsable()) continue;
            int before = stock.getResourcesAmount(resource);
            int batch = Math.Min(amount - received, Math.Max(0, limit - before));
            if (batch <= 0) continue;
            stock.addResources(resource, batch);
            received += stock.getResourcesAmount(resource) - before;
            if (received == amount) break;
        }
        if (received > 0) city._storage_version++;
        return received;
    }

    /// <summary>客运改派不能占住不再顺路的空预留，已经装车的材料保留在车上。</summary>
    /// <param name="vehicle">被改派的车。</param>
    /// <param name="stops">新行程已有站序。</param>
    /// <param name="purpose">新行程用途。</param>
    internal static void OnAssigned(RailwayVehicle vehicle, IReadOnlyList<long> stops, RailwayVehicleMode purpose)
    {
        if (vehicle.Freight.State != RailwayFreightState.Reserved || purpose == RailwayVehicleMode.Freight) return;
        int source = stops.ToList().IndexOf(vehicle.Freight.SourceStation);
        int target = stops.ToList().IndexOf(vehicle.Freight.TargetStation);
        if (source < 0 || target <= source) CancelReservation(vehicle, "PassengerPriority");
    }

    /// <summary>中断只撤销未装车的预留，已装材料等待实际改送或退货。</summary>
    /// <param name="vehicle">结束本段行程的车。</param>
    /// <param name="interrupted">本段是否中断。</param>
    internal static void OnJourneyFinished(RailwayVehicle vehicle, bool interrupted)
    {
        if (vehicle.Freight.State == RailwayFreightState.Reserved) CancelReservation(vehicle, "JourneyInterrupted");
        else if (interrupted && vehicle.Freight.State == RailwayFreightState.Loaded) vehicle.Freight.Reason = "JourneyInterrupted";
    }

    /// <summary>未装车的资源仍在源城，撤销时只释放索引。</summary>
    /// <param name="vehicle">预留所属车。</param>
    /// <param name="reason">明确取消原因。</param>
    private static void CancelReservation(RailwayVehicle vehicle, string reason)
    {
        vehicle.Freight.State = RailwayFreightState.Cancelled;
        vehicle.Freight.Amounts.Clear(); vehicle.Freight.Reason = reason;
        RailwayLog.Record("FreightCancelled", vehicle.Actor, vehicle.Station);
    }

    /// <summary>实际毁车时记录唯一背包中的损失，再销毁全部资源并释放任务索引。</summary>
    /// <param name="vehicle">即将从车队移除的车。</param>
    internal static void OnDestroyed(RailwayVehicle vehicle)
    {
        if (vehicle.Actor.inventory.hasResources()) RailwayLog.Record("CargoLost", vehicle.Actor, vehicle.Station,
            string.Join("; ", vehicle.Actor.inventory.getResources().OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
                LocalizedTextManager.getText(AssetManager.resources.get(p.Key).getLocaleID()) + "=" + p.Value.amount)));
        vehicle.Actor.inventory.empty();
        vehicle.Freight.Amounts.Clear(); vehicle.Freight.State = RailwayFreightState.Cancelled;
    }

    /// <summary>只有空背包、无贸易和补给任务且有有效归属站的车辆能接受新预留。</summary>
    /// <param name="vehicle">候选车。</param>
    private static bool CanPlan(RailwayVehicle vehicle)
        => vehicle.Body.CargoCapacity > 0 && !vehicle.Freight.Active && !vehicle.Trade.Active &&
           !vehicle.Actor.inventory.hasResources() && vehicle.Actor.getHealth() >= vehicle.Actor.getMaxHealth() * .8f &&
           RailwayFleetService.ValidHome(World.world.buildings.get(vehicle.Home), vehicle.Country);

    /// <summary>供需端必须仍是同一城市、同一国家的正常车站。</summary>
    /// <param name="station">站点编号。</param>
    /// <param name="city">预期城市。</param>
    /// <param name="country">任务国家。</param>
    private static bool ValidStation(long station, long city, long country)
    {
        var value = World.world.buildings.get(station);
        return value?.asset == Buildings.TrainStation && value.isNormal() && value.city != null &&
               value.city.isAlive() && value.city.id == city && value.city.kingdom.id == country && value.kingdom.id == country;
    }

    /// <summary>空驶、实物补给均复用实际车长和许可线路。</summary>
    /// <param name="vehicle">承运车。</param>
    /// <param name="from">当前站。</param>
    /// <param name="to">目标站。</param>
    private static RailwayRoute Route(RailwayVehicle vehicle, long from, long to)
        => RailwayRouteService.Find(from, to, vehicle.Country, false, vehicle.Body.TrainLength);
}
