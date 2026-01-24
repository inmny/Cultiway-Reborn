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
    public class RecycleEmptyGeoRegionSystem : QuerySystem<GeoRegionBinder>
    {
        protected override void OnUpdate()
        {
            Query.ForEachEntity(((ref GeoRegionBinder region, Entity entity) =>
            {
                if (entity.GetIncomingLinks<BelongToRelation>().Count == 0)
                {
                    ModClass.LogInfo($"RecycleEmptyGeoRegionSystem: Recycle {entity.Id}");
                    CommandBuffer.AddTag<TagRecycle>(entity.Id);
                    WorldboxGame.I.GeoRegions.removeObject(region.GeoRegion);
                }
            }));
            CommandBuffer.Playback();
        }
    }
}