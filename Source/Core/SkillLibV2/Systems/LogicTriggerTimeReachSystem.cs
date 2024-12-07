using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerTimeReachSystem : QuerySystem<TimeReachTrigger, TimeReachContext>
{
    public LogicTriggerTimeReachSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachComponents((ref TimeReachTrigger trigger, ref TimeReachContext context) =>
        {
            if (!trigger.Enabled) return;
            context.timer += dt;
            if (trigger.target_time >= context.timer) return;
            context.JustTriggered = true;
        });
        Query.ForEachEntity(
            (ref TimeReachTrigger trigger, ref TimeReachContext context, Entity trigger_entity) =>
            {
                if (!trigger.Enabled) return;
                if (!context.JustTriggered) return;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                context.JustTriggered = false;
                trigger.Enabled = false;
            });
    }
}