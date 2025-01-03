using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class AliveTimerSystem : QuerySystem<AliveTimer>
{
    public AliveTimerSystem()
    {
        Filter.WithoutAllTags(Tags.Get<TagPrefab>());
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