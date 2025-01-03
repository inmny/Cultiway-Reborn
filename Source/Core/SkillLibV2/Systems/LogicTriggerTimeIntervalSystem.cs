using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerTimeIntervalSystem : QuerySystem<TimeIntervalTrigger, TimeIntervalContext>
{
    public LogicTriggerTimeIntervalSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachComponents((ref TimeIntervalTrigger trigger, ref TimeIntervalContext context) =>
        {
            if (!trigger.Enabled) return;
            context.next_trigger_time -= dt;
            if (context.next_trigger_time >= 0) return;

            var interval_time = trigger.interval_time;
            context.JustTriggered = true;
            context.trigger_times++;
            context.next_trigger_time = interval_time;
        });
        Query.ForEachEntity(
            (ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity trigger_entity) =>
            {
                if (!trigger.Enabled) return;
                if (!context.JustTriggered) return;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                context.JustTriggered = false;
            });
    }
}