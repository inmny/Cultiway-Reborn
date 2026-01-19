using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.GeoLib.Systems
{
    public class RecycleEmptyGeoRegionSystem : QuerySystem<GeoRegion>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref GeoRegion region, Entity entity) =>
            {
                if (entity.GetIncomingLinks<BelongToRelation>().Count == 0)
                {
                    CommandBuffer.AddTag<TagRecycle>(entity.Id);
                }
            }));
            CommandBuffer.Playback();
        }
    }
}