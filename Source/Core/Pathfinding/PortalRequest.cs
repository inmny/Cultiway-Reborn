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
                portal.ToLoad.RemoveWhere(p => p.isRekt());
                portal.ToUnload.RemoveWhere(p => p.isRekt());
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
