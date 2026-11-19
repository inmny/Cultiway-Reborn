using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;

namespace Cultiway.Content.Railways;

/// <summary>铁路数据随原版世界保存，分两步恢复编组与乘坐任务。</summary>
internal static class RailwaySaveService
{
    /// <summary>车头数据中的编组与运行快照。</summary>
    internal const string TrainKey = "cw.railway.train";
    /// <summary>车站数据中的有序线路。</summary>
    private const string LinksKey = "cw.railway.links";
    /// <summary>车站的车厢库存和造车间隔。</summary>
    private const string DepotKey = "cw.railway.depot";

    /// <summary>原版保存车头前写入行程，不改变其运行状态。</summary>
    /// <param name="actor">即将保存的原版单位。</param>
    internal static void SaveTrain(Actor actor)
    {
        if (actor.asset != Actors.Train) return;
        actor.data.set(TrainKey, JsonConvert.SerializeObject(TrainTransportSystem.Instance.Capture(actor)));
    }

    /// <summary>原版保存车站前写入本站负责的线路。</summary>
    /// <param name="building">即将保存的建筑。</param>
    internal static void SaveStation(Building building)
    {
        if (building.asset != Buildings.TrainStation) return;
        building.data.set(LinksKey, JsonConvert.SerializeObject(TrainTrackRepairSystem.CaptureLinks(building)));
        building.data.set(DepotKey, JsonConvert.SerializeObject(RailwayFleetService.CaptureDepot(building)));
    }

    /// <summary>先恢复合法线路和车厢，保证原版能按运输编号找到承载组件。</summary>
    internal static void BeforeLoadPassengers()
    {
        var links = new List<TrainLinkSaveData>();
        foreach (var building in World.world.buildings)
        {
            if (building.asset != Buildings.TrainStation) continue;
            building.data.get(DepotKey, out string depotJson, null);
            if (depotJson != null)
            {
                var depot = JsonConvert.DeserializeObject<RailwayDepotSaveData>(depotJson);
                if (depot == null || depot.PassengerCars < 0 || depot.CargoCars < 0 ||
                    float.IsNaN(depot.BuildRemaining) || float.IsInfinity(depot.BuildRemaining) || depot.BuildRemaining < 0)
                    throw new InvalidDataException("车站库存或造车间隔无效");
                RailwayFleetService.RestoreDepot(building, depot);
            }
            building.data.get(LinksKey, out string json, null);
            if (json == null) continue;
            var saved = JsonConvert.DeserializeObject<List<TrainLinkSaveData>>(json);
            foreach (var link in saved)
            {
                ValidateTiles(link.Tiles);
                if (link.Tiles.Length < 2 || link.StationA == link.StationB || link.Payer <= 0 ||
                    !Enum.IsDefined(typeof(RailwayLinkState), link.State) ||
                    !Enum.IsDefined(typeof(RailwayLinkState), link.ResumeState) ||
                    link.Cursor < 0 || link.Cursor > link.Tiles.Length || link.Laid < 0 || link.Paid < 0 ||
                    link.Paid != (link.Laid + 7) / 8 || link.Budget < 0 || link.Failures < 0 ||
                    float.IsNaN(link.RetryRemaining) || float.IsInfinity(link.RetryRemaining) || link.RetryRemaining < 0)
                    throw new InvalidDataException("铁路存档中的工程状态或费用无效");
                for (int i = 1; i < link.Tiles.Length; i++)
                    if (!ActorComponents.Train.AreAdjacent(World.world.tiles_list[link.Tiles[i - 1]],
                            World.world.tiles_list[link.Tiles[i]]))
                        throw new InvalidDataException("铁路存档中的工程路径不连续");
                links.Add(link);
            }
        }
        TrainTrackRepairSystem.RestoreLinks(links);
        foreach (var actor in World.world.units)
        {
            if (actor.asset != Actors.Train || !TryRead(actor, out var data)) continue;
            var body = actor.getActorComponent<ActorComponents.Train>();
            body.SetCars(data.Cars);
            if (data.Track.Length > 0)
                body.ConfigureTrack(data.Track.Select(i => World.world.tiles_list[i]).ToArray(), data.Distance);
            RailwayFleetService.Restore(actor, data.Vehicle);
            if (data.Vehicle.Station == 0 && data.Track.Length > 1 &&
                !RailwayTrackOccupancy.Restore(actor.id, body.Track))
                throw new InvalidDataException("铁路存档中有两列车占用同一区间");
        }
    }

    /// <summary>原版登车恢复后重建行程，尚未分配车辆的候车者重新登记。</summary>
    internal static void AfterLoadPassengers()
    {
        foreach (var actor in World.world.units.getSimpleList().ToArray())
            if (actor.asset == Actors.Train && TryRead(actor, out var data))
                TrainTransportSystem.Instance.Restore(actor, data);
        foreach (var actor in World.world.units)
        {
            actor.data.get(TrainPassengerService.OriginKey, out long origin, -1);
            actor.data.get(TrainPassengerService.DestinationKey, out long destination, -1);
            if (!actor.isAlive() || actor.is_inside_boat || origin < 0 || destination < 0 ||
                PortalManager.GetRequest(actor) != null) continue;
            var start = World.world.buildings.get(origin)?.GetBuildingComponent<Portal>();
            var end = World.world.buildings.get(destination)?.GetBuildingComponent<Portal>();
            if (start != null && end != null && PortalManager.NewRequest(start, end, actor)) continue;
            TrainPassengerService.CancelWaiting(actor, new PortalRequest { Portals = new() });
        }
    }

    /// <summary>在反序列化边界检查组成和格子范围。</summary>
    /// <param name="actor">已经创建的车头。</param>
    /// <param name="data">经过检查的存档数据。</param>
    /// <returns>存在本版本的列车记录时为真。</returns>
    private static bool TryRead(Actor actor, out TrainSaveData data)
    {
        data = null;
        actor.data.get(TrainKey, out string json, null);
        if (json == null) return false;
        data = JsonConvert.DeserializeObject<TrainSaveData>(json);
        if (data?.Cars == null || data.Stops == null || data.Vehicle == null || data.Cars.Any(c =>
                c != TrainCarKind.Passenger && c != TrainCarKind.Cargo) ||
            float.IsNaN(data.Distance) || float.IsInfinity(data.Distance) || data.Distance < 0f || data.QueueOrder < 0)
            throw new InvalidDataException("列车存档中的编组或进度无效");
        if (data.Vehicle.Trade?.Visits == null || data.Vehicle.Trade.Trip < 0 ||
            data.Vehicle.Trade.Visits.Any(v => v.Key <= 0 || v.Value < 0) ||
            !Enum.IsDefined(typeof(RailwayVehicleMode), data.Vehicle.Mode) ||
            !Enum.IsDefined(typeof(RailwayVehicleMode), data.Purpose) ||
            float.IsNaN(data.Vehicle.RepairFraction) || float.IsInfinity(data.Vehicle.RepairFraction) ||
            data.Vehicle.RepairFraction < 0 || float.IsNaN(data.Vehicle.TradeRemaining) ||
            float.IsInfinity(data.Vehicle.TradeRemaining) || data.Vehicle.TradeRemaining < 0 ||
            float.IsNaN(data.Vehicle.StrandedElapsed) || float.IsInfinity(data.Vehicle.StrandedElapsed) ||
            data.Vehicle.StrandedElapsed < 0)
            throw new InvalidDataException("列车归属、维修或返站计时无效");
        var freight = data.Vehicle.Freight;
        if (freight?.Amounts == null || !Enum.IsDefined(typeof(RailwayFreightState), freight.State) ||
            freight.Unloaded < 0 || freight.Amounts.Any(p => !RailwayFreightService.Resources.Contains(p.Key) || p.Value <= 0) ||
            freight.Amounts.Values.Sum() > data.Cars.Count(c => c == TrainCarKind.Cargo) * ActorComponents.Train.CargoPerCar ||
            (freight.Active && (freight.Country <= 0 || freight.SourceCity <= 0 || freight.SourceStation <= 0 ||
                freight.Amounts.Count == 0 || data.Vehicle.Trade.Active)) ||
            (freight.State == RailwayFreightState.Reserved && (freight.TargetCity <= 0 || freight.TargetStation <= 0)))
            throw new InvalidDataException("列车实物补给的数量或进度无效");
        ValidateTiles(data.Track);
        foreach (var stop in data.Stops)
        {
            ValidateTiles(new[] { stop.Tile });
            if (stop.ToLoad == null || stop.ToUnload == null)
                throw new InvalidDataException("列车存档缺少上下客记录");
        }
        return true;
    }

    /// <summary>存档格子编号必须属于当前世界。</summary>
    /// <param name="tiles">从存档读出的格子编号。</param>
    private static void ValidateTiles(int[] tiles)
    {
        if (tiles == null || tiles.Any(i => i < 0 || i >= World.world.tiles_list.Length))
            throw new InvalidDataException("铁路存档引用了当前世界之外的格子");
    }
}
