using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.BuildingComponents;

namespace Cultiway.Core.Libraries
{
    public class PortalAsset : Asset
    {
        public Action<Portal> RequestRebuildGraph;
        public HashSet<BuildingAsset> Buildings = new();
    }
}