using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components
{
    public struct BelongToRelation : ILinkRelation
    {
        public Entity entity;
        public GeoRegionLayer layer;

        public Entity GetRelationKey()
        {
            return entity;
        }
    }
}
