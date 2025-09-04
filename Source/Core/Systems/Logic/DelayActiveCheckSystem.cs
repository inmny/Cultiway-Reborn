using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class DelayActiveCheckSystem : QuerySystem<DelayActive>
{
    public DelayActiveCheckSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.AllTags(Tags.Get<TagInactive>());
    }
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity(((ref DelayActive delay, Entity entity) =>
        {
            delay.LeftTime -= dt;
            if (delay.LeftTime <= 0)
            {
                CommandBuffer.RemoveTag<TagInactive>(entity.Id);
                CommandBuffer.RemoveComponent<DelayActive>(entity.Id);
            }
        }));
    }
}