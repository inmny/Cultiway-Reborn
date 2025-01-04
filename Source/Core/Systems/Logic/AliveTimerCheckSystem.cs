using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class AliveTimerCheckSystem : QuerySystem<AliveTimer, AliveTimeLimit>
{
    public AliveTimerCheckSystem()
    {
        Filter.WithoutAllTags(Tags.Get<TagPrefab, TagRecycle>());
    }
    protected override void OnUpdate()
    {
        var cmd_buf = CommandBuffer;
        Query.ForEachEntity(((ref AliveTimer timer, ref AliveTimeLimit limit, Entity e) =>
        {
            if (timer.value >= limit.value) cmd_buf.AddTag<TagRecycle>(e.Id);
        }));
        cmd_buf.Playback();
    }
}