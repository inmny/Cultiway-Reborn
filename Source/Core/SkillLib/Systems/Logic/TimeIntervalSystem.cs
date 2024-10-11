using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class TimeIntervalSystem : QuerySystem<AliveTimer, TimeIntervalTrigger>
{
    protected override void OnUpdate()
    {
        Query.WithoutAllTags(Tags.Get<PrefabTag>());
        Query.ForEachEntity((ref AliveTimer timer, ref TimeIntervalTrigger trigger, Entity skill_entity) =>
        {
            if (!trigger.Enabled) return;
            if (Mathf.FloorToInt(timer.alive_time                    / trigger.interval) !=
                Mathf.FloorToInt((timer.alive_time - Tick.deltaTime) / trigger.interval))
            {
                var action_entity = trigger.ActionContainer;
                ref var action_component =
                    ref action_entity.GetComponent<TimeIntervalActionContainerInfo>();
                action_component.Meta.action(ref trigger, ref skill_entity, ref action_entity);
            }
        });
    }
}