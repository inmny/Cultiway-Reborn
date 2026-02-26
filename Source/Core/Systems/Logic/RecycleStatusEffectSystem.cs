using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class RecycleStatusEffectSystem : QuerySystem<StatusComponent>
{
    public RecycleStatusEffectSystem()
    {
        Filter.WithoutAllTags(Tags.Get<TagPrefab>());
        Filter.AllTags(Tags.Get<TagRecycle>());
    }
    protected override void OnUpdate()
    {
        Query.ForEachEntity(((ref StatusComponent status, Entity entity) =>
        {
            foreach (var status_owner in entity.GetIncomingLinks<StatusRelation>().Entities)
            {
                if (status_owner.HasComponent<ActorBinder>())
                {
                    status_owner.GetComponent<ActorBinder>().Actor?.setStatsDirty();
                }
            }
        }));
    }
}