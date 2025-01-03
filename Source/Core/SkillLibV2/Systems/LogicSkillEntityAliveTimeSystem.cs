using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicSkillEntityAliveTimeSystem : QuerySystem<AliveTimer>
{
    public LogicSkillEntityAliveTimeSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.AllComponents(ComponentTypes.Get<SkillEntity>());
    }

    protected override void OnUpdate()
    {
        var delta_time = Tick.deltaTime;
        CommandBuffer cmd_buf = CommandBuffer;
        Query.ForEachEntity((ref AliveTimer timer, Entity entity) =>
        {
            timer.value += delta_time;
            if (timer.value >= SkillConst.recycle_time) cmd_buf.AddTag<TagRecycle>(entity.Id);
        });
        cmd_buf.Playback();
    }
}