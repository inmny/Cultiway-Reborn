using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Pathfinding
{
    public class PortalManager : BaseSystem
    {
        public static PortalManager Instance { get; } = new();
        private List<PortalRequest> _requests = new();
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
        }
        internal static void RemoveDeadUnits()
        {
            foreach (var request in Instance._requests)
            {
                request.RemoveDeadUnits();
            }
        }
        public static void NewRequest(Building start_portal, Building target_portal, Actor passenger)
        {
            PortalRequest request = null;
            
            foreach (var r in Instance._requests)
            {
                
            }
        }
    }
}