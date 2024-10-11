using Cultiway.Const;
using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicObserverRecycleTimerSystem : QuerySystem<RecycleTimer>
{
    public LogicObserverRecycleTimerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<PrefabTag>());
        Filter.AllTags(Tags.Get<TagReadyRecycle>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer cmd_buf = CommandBuffer;
        Query.ForEachEntity((ref RecycleTimer timer, Entity entity) =>
        {
            timer.value += Tick.deltaTime;
            if (timer.value >= SkillConst.recycle_time) cmd_buf.AddTag<TagRecycle>(entity.Id);
        });
        cmd_buf.Playback();
    }
}