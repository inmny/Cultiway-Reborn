using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.GeoLib.Components
{
    public struct GeoRegionBinder(long id) : IComponent
    {
        public long ID = id;
        [Ignore]
        public GeoRegion GeoRegion{
            get{
                if (_geo_region != null)
                    return _geo_region;
                _geo_region = WorldboxGame.I.GeoRegions.get(ID);
                return _geo_region;
            }
        }
        internal GeoRegion _geo_region;
    }
}