using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.BuildingComponents;
using Friflo.Engine.ECS.Systems;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.Pathfinding
{
    public class PortalManager : BaseSystem
    {
        public static PortalManager Instance { get; } = new();
        private List<PortalRequest> _requests = new();
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            for (int i = 0; i < Instance._requests.Count; i++)
            {
                var r = _requests[i];
                if (
                    r.State == PortalRequestState.Completed
                    || r.Passengers.Count == 0
                )
                {
                    Instance._requests.RemoveAt(i);
                    i--;
                    continue;
                }
            }
        }
        internal static void RemoveDeadUnits()
        {
            foreach (var request in Instance._requests)
            {
                request.RemoveDeadUnits();
            }
        }
        public static void NewRequest(Portal start_portal, Portal target_portal, Actor passenger)
        {
            if (start_portal == null || target_portal == null || passenger == null)
            {
                return;
            }

            var startBuilding = start_portal.building;
            var targetBuilding = target_portal.building;
            if (startBuilding == null || targetBuilding == null)
            {
                return;
            }

            PortalRequest request = null;

            // 尝试复用已有请求：路径中包含起点，且终点在起点之后（不回头），否则尝试向前扩展
            foreach (var r in Instance._requests)
            {
                if (r.Portals == null || r.Portals.Count == 0)
                {
                    continue;
                }

                int startIdx = r.Portals.IndexOf(startBuilding);
                if (startIdx < 0)
                {
                    continue;
                }

                int targetIdx = r.Portals.IndexOf(targetBuilding);
                if (targetIdx >= startIdx)
                {
                    request = r;
                    break;
                }

                // 目标不在路径中或在起点之前，尝试从现有路径末尾向前延伸，不走回头路
                var existingSet = new HashSet<Building>(r.Portals);
                var lastPortal = r.Portals.LastOrDefault()?.GetBuildingComponent<Core.BuildingComponents.Portal>();
                if (lastPortal == null)
                {
                    continue;
                }

                var extendPath = FindPortalPath(lastPortal, target_portal, existingSet);
                if (extendPath != null && extendPath.Count > 0)
                {
                    // 扩展时保留原路径末端，追加新路径（跳过起点重复）
                    for (int i = 1; i < extendPath.Count; i++)
                    {
                        r.Portals.Add(extendPath[i].building);
                    }
                    request = r;
                    break;
                }
            }

            // 无可复用请求则新建
            if (request == null)
            {
                var path = FindPortalPath(start_portal, target_portal, null);
                if (path == null || path.Count == 0)
                {
                    return;
                }

                request = new PortalRequest
                {
                    Portals = path.Select(p => p.building).ToList(),
                    Driver = null,
                    State = PortalRequestState.WaitingDriver,
                    Passengers = new HashSet<Actor>()
                };
                Instance._requests.Add(request);
            }

            request.Passengers ??= new HashSet<Actor>();
            request.Passengers.Add(passenger);
        }

        private static List<Portal> FindPortalPath(Portal start, Portal target, HashSet<Building> blocked)
        {
            if (start == null || target == null)
            {
                return null;
            }

            var queue = new Queue<Portal>();
            var prev = new Dictionary<long, Portal>();
            var visited = new HashSet<long>();

            void Enqueue(Portal p, Portal from)
            {
                if (p == null || p.building == null)
                {
                    return;
                }
                var id = p.building.id;
                if (visited.Contains(id))
                {
                    return;
                }
                if (blocked != null && blocked.Contains(p.building))
                {
                    return;
                }
                visited.Add(id);
                prev[id] = from;
                queue.Enqueue(p);
            }

            Enqueue(start, null);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == null || cur.building == null)
                {
                    continue;
                }

                if (cur.building == target.building)
                {
                    // 回溯路径
                    var path = new List<Portal>();
                    var walk = cur;
                    while (walk != null)
                    {
                        path.Add(walk);
                        var pid = walk.building.id;
                        if (!prev.TryGetValue(pid, out var pprev))
                        {
                            break;
                        }
                        walk = pprev;
                    }
                    path.Reverse();
                    return path;
                }

                if (cur.Neighbours == null)
                {
                    continue;
                }

                foreach (var nb in cur.Neighbours)
                {
                    Enqueue(nb, cur);
                }
            }

            return null;
        }
    }
}