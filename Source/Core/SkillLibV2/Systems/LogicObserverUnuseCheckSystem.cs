using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicObserverUnuseCheckSystem : QuerySystem<ObserverEntity>
{
    public LogicObserverUnuseCheckSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagReadyRecycle>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer cmd_buf = CommandBuffer;
        Query.ForEachEntity((ref ObserverEntity observer, Entity entity) =>
        {
            if (observer.LinkedCount == 0)
            {
                cmd_buf.AddTag<TagReadyRecycle>(entity.Id);
                cmd_buf.SetComponent(entity.Id, new RecycleTimer());
            }
        });
        cmd_buf.Playback();
    }
}