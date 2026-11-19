using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Railways;

/// <summary>按国家和运兵许可计算同一份估时与实际站序。</summary>
internal static class RailwayRouteService
{
    /// <summary>已检查真实轨道的直连图，不复制全世界地形。</summary>
    private static Dictionary<long, List<PortalConnection>> direct = new();
    /// <summary>当前图中的车站。</summary>
    private static Dictionary<long, Building> stations = new();

    /// <summary>一次网络更新替换整个小型站点图。</summary>
    /// <param name="graph">真实区间和纯行驶时间。</param>
    /// <param name="points">可上下客的正常车站。</param>
    internal static void Update(Dictionary<long, List<PortalConnection>> graph, IEnumerable<Building> points)
    {
        direct = graph;
        stations = points.ToDictionary(p => p.id);
    }

    /// <summary>对外发布同国现有车辆能够承担的民用和运兵路线。</summary>
    /// <param name="origin">当前上车站。</param>
    internal static List<PortalConnection> BuildConnections(long origin)
    {
        var result = new List<PortalConnection>();
        var countries = RailwayFleetService.Vehicles.Where(v => v.Body.PassengerCapacity > 0 &&
            v.Country > 0 && v.Mode != RailwayVehicleMode.Stopped && v.Mode != RailwayVehicleMode.Repairing &&
            v.Actor.getHealth() >= v.Actor.getMaxHealth() * .8f).Select(v => v.Country).Distinct().ToArray();
        foreach (long country in countries)
        {
            float wait = TrainTransportSystem.Instance.EstimatePickup(origin, country);
            if (float.IsPositiveInfinity(wait)) continue;
            float length = RailwayFleetService.PassengerLength(country);
            foreach (bool military in new[] { false, true })
            foreach (long target in stations.Keys.Where(id => id != origin).OrderBy(id => id))
            {
                var route = Find(origin, target, country, military, length);
                if (route == null) continue;
                result.Add(new PortalConnection(target, route.Time + wait, country, military ? 2 : 1));
            }
        }
        return result;
    }

    /// <summary>运输请求沿用与该国、该批次许可相同的最短时间路线。</summary>
    /// <param name="start">上车站。</param>
    /// <param name="target">下车站。</param>
    /// <param name="passenger">请求人。</param>
    internal static IReadOnlyList<Portal> GetRoute(Portal start, Portal target, Actor passenger)
        => Find(start.building.id, target.building.id, passenger.kingdom.id, passenger.army != null,
            RailwayFleetService.PassengerLength(passenger.kingdom.id))?.Stops.Select(id =>
                World.world.buildings.get(id).GetBuildingComponent<Portal>()).ToArray();

    /// <summary>找到满足实际车长、国家和军队规则的完整路线。</summary>
    /// <param name="origin">真实始发站。</param>
    /// <param name="target">真实终点站。</param>
    /// <param name="country">车队所属国家。</param>
    /// <param name="military">是否承运军队。</param>
    /// <param name="length">实际车长。</param>
    internal static RailwayRoute Find(long origin, long target, long country, bool military, float length)
    {
        if (!stations.ContainsKey(origin) || !stations.ContainsKey(target)) return null;
        var owner = World.world.kingdoms.get(country);
        if (owner == null || !Allowed(stations[origin]) || !Allowed(stations[target])) return null;
        if (origin == target) return new RailwayRoute { Stops = new[] { origin }, Time = 0 };
        var costs = new Dictionary<long, float> { [origin] = 0 };
        var previous = new Dictionary<long, long>();
        var open = new HashSet<long> { origin };
        var visited = new HashSet<long>();
        while (open.Count > 0)
        {
            long current = open.OrderBy(id => costs[id]).ThenBy(id => id).First();
            open.Remove(current);
            if (current == target)
            {
                var path = new List<long> { target };
                while (current != origin) { current = previous[current]; path.Add(current); }
                path.Reverse();
                return new RailwayRoute { Stops = path.ToArray(), Time = costs[target] };
            }
            visited.Add(current);
            foreach (var connection in direct[current].OrderBy(c => c.TargetId))
            {
                long next = connection.TargetId;
                if (visited.Contains(next) || !Allowed(stations[next]) ||
                    !TrainTrackRepairSystem.CanUseInterval(current, next, country, military, length)) continue;
                float time = costs[current] + connection.TravelTime + length / TrainConfig.TrackSpeed +
                             (current == origin ? 0 : TrainConfig.StopBaseWait);
                if (costs.TryGetValue(next, out float old) && old <= time) continue;
                costs[next] = time; previous[next] = current; open.Add(next);
            }
        }
        return null;

        bool Allowed(Building station) => military ? station.kingdom == owner :
            RailTrackRoutePlanner.HasPermission(owner, station.kingdom);
    }

    /// <summary>清除旧世界或旧网络引用。</summary>
    internal static void Clear() { direct.Clear(); stations.Clear(); }
}

/// <summary>同一运输选择的实际停站序列和模拟耗时。</summary>
internal sealed class RailwayRoute
{
    /// <summary>包含始发和终点的站序。</summary>
    internal long[] Stops;
    /// <summary>行驶、车尾清空与中途基础停站时间。</summary>
    internal float Time;
}
