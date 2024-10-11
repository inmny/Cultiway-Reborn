using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicObserverReuseCheckSystem : QuerySystem<ObserverEntity>
{
    public LogicObserverReuseCheckSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.AllTags(Tags.Get<TagReadyRecycle>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer cmd_buf = CommandBuffer;
        Query.ForEachEntity((ref ObserverEntity observer, Entity entity) =>
        {
            if (observer.LinkedCount > 0) cmd_buf.RemoveTag<TagReadyRecycle>(entity.Id);
        });
        cmd_buf.Playback();
    }
}