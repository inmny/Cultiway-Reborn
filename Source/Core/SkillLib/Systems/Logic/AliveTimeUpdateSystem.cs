using Cultiway.Const;
using Cultiway.Core.SkillLib.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class AliveTimeUpdateSystem : QuerySystem<AliveTimer>
{
    protected override void OnUpdate()
    {
        Query.WithoutAllTags(Tags.Get<PrefabTag>());
        Query.ForEachEntity((ref AliveTimer timer, Entity skill_entity) => { timer.alive_time += Tick.deltaTime; });
        Query.ForEachEntity((ref AliveTimer timer, Entity skill_entity) =>
        {
            if (timer.alive_time > SkillConst.recycle_time)
            {
                CommandBuffer.AddTag<RecycleTag>(skill_entity.Id);
            }
        });
        CommandBuffer.Playback();
    }
}