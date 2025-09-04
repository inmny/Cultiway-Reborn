using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class AliveTimerSystem : QuerySystem<AliveTimer>
{
    public AliveTimerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachComponents(((ref AliveTimer timer) =>
        {
            timer.value += dt;
        }));
    }
}