using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class TimeReachSystem : QuerySystem<AliveTimer, TimeReachTrigger>
{
    protected override void OnUpdate()
    {
        Query.WithoutAllTags(Tags.Get<PrefabTag>());
        Query.ForEachEntity((ref AliveTimer timer, ref TimeReachTrigger trigger, Entity skill_entity) =>
        {
            if (!trigger.Enabled) return;
            if (timer.alive_time >= trigger.target_time && timer.alive_time - Tick.deltaTime < trigger.target_time)
            {
                var action_entity = trigger.ActionContainer;
                ref var action_component =
                    ref action_entity.GetComponent<TimeReachActionContainerInfo>();
                action_component.Meta.action(ref trigger, ref skill_entity, ref action_entity);
            }
        });
    }
}