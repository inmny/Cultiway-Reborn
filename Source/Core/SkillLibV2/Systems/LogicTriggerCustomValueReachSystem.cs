using System;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerCustomValueReachSystem<TTrigger, TContext, TValue> : QuerySystem<TTrigger, TContext>
    where TValue : IComparable<TValue>
    where TContext : struct, ICustomValueReachContext<TValue>
    where TTrigger : struct, ICustomValueReachTrigger<TTrigger, TContext, TValue>
{
    public LogicTriggerCustomValueReachSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TTrigger trigger, ref TContext context, Entity trigger_entity) =>
        {
            if (!trigger.Enabled) return;
            var compare_result = context.Value.CompareTo(trigger.TargetValue);
            switch (trigger.ExpectedResult)
            {
                case CompareResult.EqualToTarget:
                    if (compare_result == 0) trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    break;
                case CompareResult.GreaterThanTarget:
                    if (compare_result > 0) trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    break;
                case CompareResult.LessThanTarget:
                    if (compare_result < 0) trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    break;
            }
        });
    }
}