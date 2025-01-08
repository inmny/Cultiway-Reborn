using System;
using System.Data;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Predefined;

public static class TriggerActions
{
    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GetRecycleActionMetaOnCollideCaster()
    {
        try
        {
            return TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>.StartBuild("RecycleOnCollideCaster")
                .AppendAction(recycle_on_collide_caster)
                .Build();
        }
        catch (DuplicateNameException e)
        {
            return TriggerActionBaseMeta.AllDict[e.Message] as
                TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>;
        }
    }

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GetCollisionDamageActionMeta(ElementComposition damage_composition, bool unique = false)
    {
        try
        {
            string id = unique ? $"ObjCollisionDamage.{damage_composition}.{Guid.NewGuid()}" : $"ObjCollisionDamage.{damage_composition}";
            return TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>.StartBuild(id)
                .AppendAction(single_damage)
                .Build();
            void single_damage(ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity skill_entity,
                Entity                                 modifier_container, Entity entity_modifiers)
            {
                if (!context.obj.isAlive()) return;
                if (context.obj.isActor())
                {
                    ActorExtend target = context.obj.a.GetExtend();
                    target.GetHit(skill_entity.GetComponent<SkillStrength>().value, ref damage_composition,
                        skill_entity.GetComponent<SkillCaster>().value.Base);
                }
                else
                {
                    context.obj.b.getHit(skill_entity.GetComponent<SkillStrength>().value,
                        pAttacker: skill_entity.GetComponent<SkillCaster>().value.Base);
                }
            }
        }
        catch (DuplicateNameException e)
        {
            return TriggerActionBaseMeta.AllDict[e.Message] as
                TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>;
        }
    }
    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GetSingleCollisionDamageActionMeta(ElementComposition damage_composition)
    {
        try
        {
            return TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>.StartBuild($"SingleObjCollisionDamage.{damage_composition}")
                .AppendAction(single_damage)
                .Build();
            void single_damage(ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity skill_entity,
                Entity                                 modifier_container, Entity entity_modifiers)
            {
                if (!context.obj.isAlive()) return;
                if (context.obj.isActor())
                {
                    ActorExtend target = context.obj.a.GetExtend();
                    target.GetHit(skill_entity.GetComponent<SkillStrength>().value, ref damage_composition,
                        skill_entity.GetComponent<SkillCaster>().value.Base);
                }
                else
                {
                    context.obj.b.getHit(skill_entity.GetComponent<SkillStrength>().value,
                        pAttacker: skill_entity.GetComponent<SkillCaster>().value.Base);
                }

                trigger.Enabled = false;
            }
        }
        catch (DuplicateNameException e)
        {
            return TriggerActionBaseMeta.AllDict[e.Message] as
                TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>;
        }
    }

    public static TriggerActionMeta<TTrigger, TContext> GetRecycleActionMeta<TTrigger, TContext>(bool unique = false)
        where TContext : struct, IEventContext
        where TTrigger : struct, IEventTrigger<TTrigger, TContext>
    {
        try
        {
            if (unique)
                return TriggerActionMeta<TTrigger, TContext>.StartBuild($"SimpleRecycle.{Guid.NewGuid()}")
                    .AppendAction(simple_recycle)
                    .Build();
            return TriggerActionMeta<TTrigger, TContext>.StartBuild("SimpleRecycle")
                .AppendAction(simple_recycle)
                .Build();
        }
        catch (DuplicateNameException e)
        {
            return TriggerActionBaseMeta.AllDict[e.Message] as TriggerActionMeta<TTrigger, TContext>;
        }
    }

    private static void recycle_on_collide_caster(ref ObjCollisionTrigger trigger,      ref ObjCollisionContext context,
                                                  Entity                  skill_entity, Entity modifier_container, Entity entity_modifiers)
    {
        if (context.obj == skill_entity.GetComponent<SkillCaster>().AsActor)
            skill_entity.AddTag<TagRecycle>();
    }

    private static void simple_recycle<TTrigger, TContext>(ref TTrigger trigger,      ref TContext context,
                                                           Entity       skill_entity, Entity       modifier_container, Entity entity_modifiers)
    {
        skill_entity.AddTag<TagRecycle>();
    }

    internal static void cast_count_increase<TTrigger, TContext>(ref TTrigger trigger,      ref TContext context,
                                                                 Entity       skill_entity, Entity modifier_container, Entity entity_modifiers)
    {
        foreach (Entity trigger_entity in skill_entity.ChildEntities)
            if (trigger_entity.HasComponent<CastCountReachContext>())
                trigger_entity.GetComponent<CastCountReachContext>().Value++;
    }

    internal static void Init()
    {
    }
}