using System.Data;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Predefined;

public static class TriggerActions
{
    public static TriggerActionMeta<TTrigger, TContext> GetRecycleActionMeta<TTrigger, TContext>()
        where TContext : struct, IEventContext
        where TTrigger : struct, IEventTrigger<TTrigger, TContext>
    {
        try
        {
            return TriggerActionMeta<TTrigger, TContext>.StartBuild("SimpleRecycle")
                .AppendAction(simple_recycle)
                .Build();
        }
        catch (DuplicateNameException e)
        {
            return TriggerActionBaseMeta.AllDict[e.Message] as TriggerActionMeta<TTrigger, TContext>;
        }
    }

    private static void simple_recycle<TTrigger, TContext>(ref TTrigger trigger,      ref TContext context,
                                                           Entity       skill_entity, Entity       modifier_container)
    {
        skill_entity.AddTag<TagRecycle>();
    }

    internal static void cast_count_increase<TTrigger, TContext>(ref TTrigger trigger,      ref TContext context,
                                                                 Entity       skill_entity, Entity modifier_container)
    {
        foreach (Entity trigger_entity in skill_entity.ChildEntities)
            if (trigger_entity.HasComponent<CastCountReachContext>())
                trigger_entity.GetComponent<CastCountReachContext>().Value++;
    }

    internal static void Init()
    {
    }
}