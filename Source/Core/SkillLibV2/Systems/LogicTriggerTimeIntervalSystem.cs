using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.Triggers;
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
        var curr_time = Tick.time;
        Query.ForEachComponents((ref TimeIntervalTrigger trigger, ref TimeIntervalContext context) =>
        {
            var target_time = context.next_trigger_time;
            if (target_time >= curr_time) return;

            var interval_time = trigger.interval_time;
            context.JustTriggered = true;
            context.trigger_times++;
            context.next_trigger_time += interval_time *
                                         (1 + (int)((curr_time - target_time) / interval_time));
        });
        Query.ForEachEntity(
            (ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity trigger_entity) =>
            {
                if (!context.JustTriggered) return;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                context.JustTriggered = false;
            });
    }
}