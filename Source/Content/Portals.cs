using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content
{
    [Dependency(typeof(Buildings))]
    public class Portals : ExtendLibrary<PortalAsset, Portals>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }
        public static PortalAsset TrainStation {get; private set;}
        protected override void OnInit()
        {
            TrainStation.Buildings.Add(Buildings.TrainStation);
        }
    }
}