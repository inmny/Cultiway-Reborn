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

        /// <summary>直接通行只使用原入口与出口，端点失效时重新寻路。</summary>
        public bool RequiresDirectJourney;
    }
}
