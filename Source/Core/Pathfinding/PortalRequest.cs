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
        public List<Building> Portals;
        public Actor Driver;
        public PortalRequestState State;
        public HashSet<Actor> Passengers;
        internal void RemoveDeadUnits()
        {
            Passengers.RemoveWhere(p => p.isRekt());
        }
    }
}