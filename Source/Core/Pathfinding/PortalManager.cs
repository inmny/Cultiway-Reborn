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
                if (r.IsCompleted())
                {
                    Instance._requests.RemoveAt(i);
                    i--;
                    continue;
                }
                ValidateRequest(r);
                if (r.IsCompleted())
                {
                    Instance._requests.RemoveAt(i);
                    i--;
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
        public static PortalRequest GetRequest(Actor actor)
        {
            foreach (var r in Instance._requests)
            {
                if (r.IsCompleted())
                {
                    continue;
                }
                if (r.Portals.Any(p => p.ToLoad.Contains(actor)))
                {
                    return r;
                }
            }
            return null;
        }
        public static void CancelDriverRequest(Actor actor)
        {
            foreach (var r in Instance._requests)
            {
                if (r.Driver == actor)
                {
                    r.Driver = null;
                    if (!r.IsCompleted())
                    {
                        r.State = PortalRequestState.WaitingDriver;
                    }
                    break;
                }
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

            int startIdx = -1;
            int targetIdx = -1;

            // 尝试复用已有请求：路径中包含起点，且终点在起点之后（不回头），否则尝试向前扩展
            foreach (var r in Instance._requests)
            {
                if (r.Portals == null || r.Portals.Count == 0)
                {
                    continue;
                }

                startIdx = r.Portals.FindIndex(p => p.PortalBuilding == startBuilding);
                if (startIdx < 0)
                {
                    continue;
                }

                targetIdx = r.Portals.FindIndex(p => p.PortalBuilding == targetBuilding);
                if (targetIdx >= startIdx)
                {
                    request = r;
                    break;
                }

                // 目标不在路径中或在起点之前，尝试从现有路径末尾向前延伸，不走回头路
                var existingSet = new HashSet<Building>(r.Portals.Select(p => p.PortalBuilding));
                var lastPortal = r.Portals.LastOrDefault()?.PortalBuilding.GetBuildingComponent<Portal>();
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
                        r.Portals.Add(new PortalRequest.SinglePortal
                        {
                            PortalBuilding = extendPath[i].building,
                            PortalTile = extendPath[i].building.getConstructionTile(),
                            ToLoad = new HashSet<Actor>(),
                            ToUnload = new HashSet<Actor>()
                        });
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
                    Driver = null,
                    State = PortalRequestState.WaitingDriver,
                    Portals = path.Select(p => new PortalRequest.SinglePortal
                    {
                        PortalBuilding = p.building,
                        PortalTile = p.building.getConstructionTile(),
                        ToLoad = new HashSet<Actor>(),
                        ToUnload = new HashSet<Actor>()
                    }).ToList()
                };
                startIdx = 0;
                targetIdx = path.Count - 1;
                Instance._requests.Add(request);
            }

            request.Portals[startIdx].ToLoad.Add(passenger);
            request.Portals[targetIdx].ToUnload.Add(passenger);
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

        internal static bool AssignNewRequestForDriver(Actor pActor)
        {
            if (pActor == null || pActor.isRekt())
            {
                return false;
            }

            PortalRequest best = null;
            var bestDist = int.MaxValue;
            var actorTile = pActor.current_tile;
            for (int i = 0; i < Instance._requests.Count; i++)
            {
                var request = Instance._requests[i];
                if (request.IsCompleted())
                {
                    continue;
                }
                if (request.Driver != null && request.Driver != pActor)
                {
                    continue;
                }
                if (request.Portals == null || request.Portals.Count == 0)
                {
                    continue;
                }

                var targetTile = request.Portals[0].PortalTile ??
                                 request.Portals[0].PortalBuilding?.getConstructionTile();
                if (targetTile == null || actorTile == null)
                {
                    continue;
                }

                var dist = Math.Abs(targetTile.x - actorTile.x) + Math.Abs(targetTile.y - actorTile.y);
                if (dist < bestDist)
                {
                    best = request;
                    bestDist = dist;
                }
            }

            if (best == null)
            {
                return false;
            }

            best.Driver = pActor;
            best.State = PortalRequestState.WaitingPassengers;
            return true;
        }
        internal static PortalRequest GetRequestForDriver(Actor pActor)
        {
            foreach (var r in Instance._requests)
            {
                if (r.Driver == pActor && !r.IsCompleted())
                {
                    return r;
                }
            }
            return null;
        }

        internal static void RemoveDeadBuildings()
        {
            foreach (var r in Instance._requests)
            {
                r.RemoveDeadBuildings();
            }
        }

        private static void ValidateRequest(PortalRequest request)
        {
            if (request == null || request.IsCompleted())
            {
                return;
            }

            request.RemoveDeadUnits();
            request.RemoveDeadBuildings();

            while (!request.IsCompleted() && !IsPortalAlive(request.Portals[0]))
            {
                if (request.Portals.Count > 1)
                {
                    request.Portals[1].ToUnload.UnionWith(request.Portals[0].ToUnload);
                    request.Portals[1].ToLoad.UnionWith(request.Portals[0].ToLoad);
                }
                request.Portals.RemoveAt(0);
            }

            if (request.IsCompleted())
            {
                return;
            }

            if (request.Driver != null && request.Driver.isRekt())
            {
                request.Driver = null;
                request.State = PortalRequestState.WaitingDriver;
            }

            if (request.Driver == null && request.State == PortalRequestState.Driving)
            {
                request.State = PortalRequestState.WaitingDriver;
            }

            // 没有上下客需求则直接完成
            if (request.Portals.All(p => p.ToLoad.Count == 0 && p.ToUnload.Count == 0))
            {
                request.Cancel();
            }
        }

        private static bool IsPortalAlive(PortalRequest.SinglePortal portal)
        {
            if (portal == null)
            {
                return false;
            }
            if (portal.PortalBuilding == null || portal.PortalTile == null)
            {
                return false;
            }
            return !portal.PortalBuilding.isRekt();
        }
    }
}
