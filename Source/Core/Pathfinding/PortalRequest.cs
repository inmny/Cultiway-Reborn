using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public class SinglePortal
        {
            public Building PortalBuilding;
            public WorldTile PortalTile;
            public HashSet<Actor> ToLoad;
            public HashSet<Actor> ToUnload;
        }

        internal void Clear()
        {
            Portals.Clear();
            Driver = null;
        }
        public void Cancel()
        {
            State = PortalRequestState.Completed;
            Driver = null;
            Portals.Clear();
        }

        internal void RemoveDeadUnits()
        {
            foreach (var portal in Portals)
            {
                portal.ToLoad.RemoveWhere(p => p.isRekt());
                portal.ToUnload.RemoveWhere(p => p.isRekt());
            }
        }
        internal void RemoveDeadBuildings()
        {
            Portals.RemoveAll(p => p.PortalBuilding.isRekt());
        }
        public bool IsCompleted()
        {
            return State == PortalRequestState.Completed || Portals.Count == 0;
        }
    }
}