using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Pathfinding;

public class WaterConnectivitySystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        WaterConnectivityUpdater.TryProcess();
    }
}

internal static class WaterConnectivityUpdater
{
    private static readonly object SyncRoot = new();
    private static bool _dirty;
    private static readonly ConcurrentDictionary<MapRegion, int> Components = new();

    public static void RequestRebuild()
    {
        _dirty = true;
    }

    public static void TryProcess()
    {
        if (!_dirty)
        {
            return;
        }

        var map = World.world;
        if (map?.tiles_list == null || map.tiles_list.Length == 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            _dirty = false;
        }

        Rebuild(map);
    }

    private static void Rebuild(MapBox map)
    {
        var portals = PortalRegistry.Instance.Snapshot(PortalLibrary.Dock);
        if (portals.Count == 0)
        {
            return;
        }

        Components.Clear();
        int component = 0;
        var portalToComp = new ConcurrentDictionary<long, int>();

        var componentEdges = new ConcurrentBag<(int A, int B)>();
        var componentSet = new ConcurrentDictionary<int, byte>();

        Parallel.ForEach(portals, portal =>
        {
            // 从portal对应建筑的tiles中，找到海洋地块，并取其region
            var tiles = portal.Portal?.building?.tiles;
            MapRegion startRegion = null;
            if (tiles != null)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    var tile = tiles[i];
                    if (tile != null && tile.Type != null && tile.Type.ocean)
                    {
                        startRegion = tile.region;
                        break;
                    }
                }
            }
            if (startRegion == null)
            {
                return;
            }

            if (Components.TryGetValue(startRegion, out var existingComp))
            {
                portalToComp[portal.Id] = existingComp;
                return;
            }

            var compId = Interlocked.Increment(ref component);
            if (!Components.TryAdd(startRegion, compId))
            {
                portalToComp[portal.Id] = Components[startRegion];
                return;
            }

            componentSet.TryAdd(compId, 0);

            var queue = new Queue<MapRegion>();
            queue.Enqueue(startRegion);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var neighbours = cur.neighbours;
                if (neighbours == null)
                {
                    continue;
                }

                for (int n = 0; n < neighbours.Count; n++)
                {
                    var nb = neighbours[n];
                    if (nb == null || nb.tiles.Count == 0)
                    {
                        continue;
                    }

                    var sample = nb.tiles[0];
                    if (sample == null || sample.Type == null || !sample.Type.ocean)
                    {
                        continue;
                    }

                    if (Components.TryAdd(nb, compId))
                    {
                        queue.Enqueue(nb);
                    }
                    else if (Components.TryGetValue(nb, out var otherComp) && otherComp != compId)
                    {
                        componentEdges.Add((compId, otherComp));
                    }
                }
            }

            portalToComp[portal.Id] = compId;
        });

        // 合并相交的连通分量（串行后处理，保持并行区域不变）
        if (!componentEdges.IsEmpty)
        {
            // 初始化并查集
            var parent = new Dictionary<int, int>();
            foreach (var id in componentSet.Keys.Concat(componentEdges.SelectMany(e => new[] { e.A, e.B })))
            {
                if (!parent.ContainsKey(id)) parent[id] = id;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb) return;
                if (ra < rb) parent[rb] = ra;
                else parent[ra] = rb;
            }

            foreach (var (A, B) in componentEdges)
            {
                Union(A, B);
            }

            // 重映射 Components 与 portalToComp 到合并后的根
            foreach (var kv in Components.ToList())
            {
                var newId = Find(kv.Value);
                Components[kv.Key] = newId;
            }

            foreach (var kv in portalToComp.ToList())
            {
                var newId = Find(kv.Value);
                portalToComp[kv.Key] = newId;
            }
        }

        var grouped = GroupPortals(portals, portalToComp);
        if (grouped.Count == 0)
        {
            return;
        }

        var config = PathfindingConfig.Default;
        foreach (var kv in grouped)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var orderedTargets = list
                    .Where(p => p.Id != entry.Id)
                    .OrderBy(p => Dist(entry.Tile, p.Tile))
                    .Take(config.PortalCandidates);

                var connections = new List<PortalConnection>();
                foreach (var target in orderedTargets)
                {
                    connections.Add(new PortalConnection(target.Id, EstimateTravel(entry.Tile, target.Tile)));
                }

                var updated = new PortalDefinition(entry.Portal, entry.Id, entry.Tile, entry.WaitTime, entry.TransferTime,
                    connections);
                PortalRegistry.Instance.RegisterOrUpdate(updated);
            }
        }
    }

    private static Dictionary<int, List<PortalSnapshot>> GroupPortals(
        IReadOnlyList<PortalSnapshot> portals, ConcurrentDictionary<long, int> portalToComp)
    {
        var grouped = new Dictionary<int, List<PortalSnapshot>>();
        for (int i = 0; i < portals.Count; i++)
        {
            var portal = portals[i];
            if (!portalToComp.TryGetValue(portal.Id, out var comp))
            {
                continue;
            }

            if (!grouped.TryGetValue(comp, out var list))
            {
                list = new List<PortalSnapshot>();
                grouped.Add(comp, list);
            }
            list.Add(portal);
        }

        return grouped;
    }

    private static float EstimateTravel(WorldTile a, WorldTile b)
    {
        var dist = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        var travel = dist * 0.5f;
        return travel < 1f ? 1f : travel;
    }

    private static int Dist(WorldTile a, WorldTile b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static void EnsureBuffers(int size)
    {
        // 占位，当前实现无需预分配
    }
}
