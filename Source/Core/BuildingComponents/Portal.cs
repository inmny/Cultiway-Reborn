using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.BuildingComponents
{
    public class Portal : BaseBuildingComponent
    {
        public PortalAsset Asset;
        public List<Portal> Neighbours;
        public List<Portal> ConnectedPortals;
    }
}