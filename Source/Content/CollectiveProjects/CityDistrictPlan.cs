using System;
using System.Collections.Generic;
using Cultiway.Core.CollectiveProjects;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>由当时存在的篝火和城市分区共同确定的同一工程。</summary>
internal sealed class CityDistrictPlan
{
    public long SourceBuilding;
    public CityDistrictNode[] Nodes = Array.Empty<CityDistrictNode>();
    public int Completed;

    internal CityDistrictNode Current => Completed < Nodes.Length ? Nodes[Completed] : null;

    internal static bool HasDistricts(CollectiveProjectView project) =>
        project.Payload is CityMagicUtilityProjectPayload { Districts: not null };

    internal bool HasSource(City city)
    {
        Building building = World.world.buildings.get(SourceBuilding);
        return building?.asset?.type == "type_bonfire" && building.city == city && building.isUsable();
    }

    internal bool IsConnected(City city, WorldTile target)
    {
        if (Current is not { } node || target?.zone?.city != city || !HasSource(city)) return false;
        TileZone previous = World.world.buildings.get(SourceBuilding).current_tile.zone;
        foreach (int id in node.Route)
        {
            TileZone next = CityMagicUtilityProjectRules.ResolveTile(id)?.zone;
            if (next?.city != city || Array.IndexOf(previous.neighbours_all, next) < 0) return false;
            previous = next;
        }
        return previous == target.zone;
    }

    /// <summary>只在工程验收实际净化后前进，下一轮重新冻结执行足迹。</summary>
    internal static bool Advance(CollectiveProjectView project)
    {
        var payload = (CityMagicUtilityProjectPayload)project.Payload;
        if (payload.Districts is not { } plan) return false;
        plan.Completed++;
        payload.HasExecutionBaseline = false;
        payload.ConfirmedWorldChange = null;
        payload.ExecutionFootprintTileIds = Array.Empty<int>();
        return plan.Current != null;
    }

    /// <summary>规划时沿真实连着的本城分区保存路线，运行时逐段重新判断归属。</summary>
    internal static Dictionary<TileZone, int[]> Routes(City city, TileZone root)
    {
        var routes = new Dictionary<TileZone, int[]> { [root] = Array.Empty<int>() };
        var queue = new Queue<TileZone>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            TileZone zone = queue.Dequeue();
            int[] route = routes[zone];
            foreach (TileZone neighbour in zone.neighbours_all)
            {
                if (neighbour?.city != city || routes.ContainsKey(neighbour)) continue;
                int[] path = new int[route.Length + 1];
                Array.Copy(route, path, route.Length);
                path[route.Length] = neighbour.centerTile.tile_id;
                routes.Add(neighbour, path);
                queue.Enqueue(neighbour);
            }
        }
        return routes;
    }
}

/// <summary>一个真实污染节点及经城市原分区到达的连接。</summary>
internal sealed class CityDistrictNode
{
    public int Target;
    public int[] Route = Array.Empty<int>();
}
