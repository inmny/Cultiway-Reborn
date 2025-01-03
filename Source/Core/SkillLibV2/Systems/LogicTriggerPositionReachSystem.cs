using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerPositionReachSystem : QuerySystem<PositionReachTrigger, PositionReachContext>
{
    public LogicTriggerPositionReachSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(
            (ref PositionReachTrigger trigger, ref PositionReachContext context, Entity trigger_entity) =>
            {
                if (!trigger.Enabled) return;
                Entity skill_entity = trigger_entity.Parent;
                var skill_pos = skill_entity.GetComponent<Position>();
                var target_pos = skill_entity.GetComponent<SkillTargetPos>();
                var distance = trigger.distance;

                if (Vector3.Distance(skill_pos.value, target_pos.v3) < distance)
                    context.JustTriggered = true;
            });
        Query.ForEachEntity(
            (ref PositionReachTrigger trigger, ref PositionReachContext context, Entity trigger_entity) =>
            {
                if (!trigger.Enabled) return;
                if (!context.JustTriggered) return;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                context.JustTriggered = false;
                trigger.Enabled = false;
            });
    }
}