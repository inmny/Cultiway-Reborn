using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Pathfinding
{
    public enum PortalRequestState
    {
        WaitingDriver,
        WaitingPassengers,
        Driving,
        Completed
    }
    public class PortalRequest
    {
        public Actor Driver;
        public PortalRequestState State;
        public List<SinglePortal> Portals;
        public PortalAsset PortalType;
        public bool AllowEmptyRide;
        public bool IsExperimentalRide;
        public class SinglePortal
        {
            public Building PortalBuilding;
            public WorldTile PortalTile;
            public HashSet<Actor> ToLoad;
            public HashSet<Actor> ToUnload;
            public Dictionary<long, WorldTile> LoadTiles;
            public Dictionary<long, WorldTile> UnloadTiles;

            public void AddLoad(Actor actor, WorldTile tile = null)
            {
                if (actor == null)
                {
                    return;
                }

                ToLoad.Add(actor);
                AddActorTile(ref LoadTiles, actor, tile);
            }

            public void AddUnload(Actor actor, WorldTile tile = null)
            {
                if (actor == null)
                {
                    return;
                }

                ToUnload.Add(actor);
                AddActorTile(ref UnloadTiles, actor, tile);
            }

            public WorldTile GetLoadTile(Actor actor)
            {
                return GetActorTile(LoadTiles, actor);
            }

            public WorldTile GetUnloadTile(Actor actor)
            {
                return GetActorTile(UnloadTiles, actor);
            }

            public void RemoveLoad(Actor actor)
            {
                ToLoad?.Remove(actor);
                RemoveActorTile(LoadTiles, actor);
            }

            public void RemoveUnload(Actor actor)
            {
                ToUnload?.Remove(actor);
                RemoveActorTile(UnloadTiles, actor);
            }

            public void RemovePassenger(Actor actor)
            {
                RemoveLoad(actor);
                RemoveUnload(actor);
            }

            public void MergePassengerTilesFrom(SinglePortal source)
            {
                MergeActorTiles(ref LoadTiles, source?.LoadTiles);
                MergeActorTiles(ref UnloadTiles, source?.UnloadTiles);
            }

            private static void AddActorTile(ref Dictionary<long, WorldTile> map, Actor actor, WorldTile tile)
            {
                if (!TryGetActorId(actor, out var id))
                {
                    return;
                }

                if (tile == null)
                {
                    map?.Remove(id);
                    return;
                }

                map ??= new Dictionary<long, WorldTile>();
                map[id] = tile;
            }

            private static WorldTile GetActorTile(Dictionary<long, WorldTile> map, Actor actor)
            {
                return TryGetActorId(actor, out var id) && map != null && map.TryGetValue(id, out var tile)
                    ? tile
                    : null;
            }

            private static void RemoveActorTile(Dictionary<long, WorldTile> map, Actor actor)
            {
                if (TryGetActorId(actor, out var id))
                {
                    map?.Remove(id);
                }
            }

            private static void MergeActorTiles(ref Dictionary<long, WorldTile> target,
                Dictionary<long, WorldTile> source)
            {
                if (source == null || source.Count == 0)
                {
                    return;
                }

                target ??= new Dictionary<long, WorldTile>();
                foreach (var pair in source)
                {
                    target[pair.Key] = pair.Value;
                }
            }

            private static bool TryGetActorId(Actor actor, out long id)
            {
                if (actor?.data == null)
                {
                    id = 0;
                    return false;
                }

                id = actor.data.id;
                return true;
            }
        }

        internal void Clear()
        {
            Portals.Clear();
            Driver = null;
            PortalType = null;
            AllowEmptyRide = false;
            IsExperimentalRide = false;
        }
        public void Cancel()
        {
            State = PortalRequestState.Completed;
            Driver = null;
            Portals.Clear();
            PortalType = null;
            AllowEmptyRide = false;
            IsExperimentalRide = false;
        }

        internal void RemoveDeadUnits()
        {
            foreach (var portal in Portals)
            {
                foreach (var actor in portal.ToLoad.Where(p => p.isRekt()).ToList())
                {
                    portal.RemoveLoad(actor);
                }

                foreach (var actor in portal.ToUnload.Where(p => p.isRekt()).ToList())
                {
                    portal.RemoveUnload(actor);
                }
            }
        }
        internal void RemoveDeadBuildings()
        {
            for (int i = 0; i < Portals.Count;)
            {
                var portal = Portals[i];
                if (portal.PortalBuilding != null && !portal.PortalBuilding.isRekt())
                {
                    i++;
                    continue;
                }

                // 被摧毁则将上下客需求并入下一节点，避免数据丢失
                if (i + 1 < Portals.Count)
                {
                    Portals[i + 1].ToLoad.UnionWith(portal.ToLoad);
                    Portals[i + 1].ToUnload.UnionWith(portal.ToUnload);
                    Portals[i + 1].MergePassengerTilesFrom(portal);
                }
                Portals.RemoveAt(i);
            }

            if (Portals.Count == 0)
            {
                State = PortalRequestState.Completed;
            }
        }
        public bool IsCompleted()
        {
            return State == PortalRequestState.Completed || Portals.Count == 0;
        }
    }
}
