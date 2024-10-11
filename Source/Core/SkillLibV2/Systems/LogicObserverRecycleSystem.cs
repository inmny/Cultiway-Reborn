using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicObserverRecycleSystem : QuerySystem<ObserverEntity>
{
    public LogicObserverRecycleSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.AllTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer cmd_buf = CommandBuffer;

        foreach (Entity e in Query.Entities) cmd_buf.DeleteEntity(e.Id);

        cmd_buf.Playback();
    }
}