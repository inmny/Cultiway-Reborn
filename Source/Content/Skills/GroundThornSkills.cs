using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Skills;

public class GroundThornSkills : ICanInit
{
    public static SkillEntityMeta SingleGroundThornEntity { get; private set; }
    public static SkillEntityMeta GroundThornCasterEntity { get; private set; }
    
    public static SkillEntityMeta LineGroundThornCasterEntity { get; private set; }
    public static SkillEntityMeta CircleGroundThornCasterEntity { get; private set; }
    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GroundThornDamageActionMeta
    {
        get;
        private set;
    } = TriggerActions.GetCollisionDamageActionMeta(new([0, 0, 0, 0, 100, 0, 0, 0]),
        nameof(GroundThornDamageActionMeta));
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> LineGroundThornSpawnActionMeta { get;
        private set;
    }
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> CircleGroundThornSpawnActionMeta { get;
        private set;
    }
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnGroundThornActionMeta { get;
        private set;
    }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSingleGroundThorn { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartLineGroundThorn { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartCircleGroundThorn { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllGroundThorn { get; private set; }
    public void Init()
    {
        SingleGroundThornEntity = SkillEntityMeta.StartBuild(nameof(SingleGroundThornEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/ground_thorn"), 0.3f, 0.3f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = GroundThornDamageActionMeta
            }, 0.5f)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(ground_thorn_modifiers_application)
            .Build();
        GroundThornDamageActionMeta.StartModify()
            .PrependAction(ground_thorn_apply_force);
        LineGroundThornSpawnActionMeta = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(LineGroundThornSpawnActionMeta))
            .AppendAction(spawn_single_ground_thorn)
            .Build();
        LineGroundThornCasterEntity = SkillEntityMeta.StartBuild(nameof(LineGroundThornCasterEntity))
            .AddTimeIntervalTrigger(0.3f, LineGroundThornSpawnActionMeta)
            .AddTimeReachTrigger(3, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .SetTrajectory(Trajectories.GoForward, 5)
            .Build();

        CircleGroundThornSpawnActionMeta = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(CircleGroundThornSpawnActionMeta))
            .AppendAction(spawn_circle_ground_thorn)
            .Build();
        CircleGroundThornCasterEntity = SkillEntityMeta.StartBuild(nameof(CircleGroundThornCasterEntity))
            .AddTimeIntervalTrigger(0.3f, CircleGroundThornSpawnActionMeta)
            .AddComponent(new Radius()
            {
                Value = 1
            })
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();

        RandomSpawnGroundThornActionMeta = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnGroundThornActionMeta))
            .AppendAction(random_spawn_ground_thorn)
            .Build();
        GroundThornCasterEntity = SkillEntityMeta.StartBuild(nameof(GroundThornCasterEntity))
            .AddComponent(new SkillTargetObj())
            .AddTimeIntervalTrigger(0.5f, RandomSpawnGroundThornActionMeta)
            .AllowModifier<CastCountModifier, int>(new CastCountModifier(1))
            .AllowModifier<StageModifier, int>(new StageModifier(1))
            .Build();

        StartSingleGroundThorn = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSingleGroundThorn))
            .AppendAction(spawn_single_ground_thorn)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartLineGroundThorn = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartLineGroundThorn))
            .AppendAction(spawn_line_ground_thorn_caster)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartCircleGroundThorn = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartCircleGroundThorn))
            .AppendAction(spawn_circle_ground_thorn_caster)
            .Build();
        StartAllGroundThorn = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllGroundThorn))
            .AppendAction(spawn_ground_thorn_caster)
            .Build();

        starters = [StartSingleGroundThorn.id, StartLineGroundThorn.id, StartCircleGroundThorn.id];
    }

    private void ground_thorn_modifiers_application(Entity entity, Entity modifiers)
    {
        var data = entity.Data;
        var modifiers_data = modifiers.Data;
        var collider_scale = modifiers_data.Get<ScaleModifier>().Value;
        
        data.Get<Scale>().value *= collider_scale;
        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= collider_scale;
        }
    }

    internal static string[] starters;
    [Hotfixable]
    private void random_spawn_ground_thorn(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        string id = starters[Randy.randomInt(0, entity_modifiers.GetComponent<StageModifier>().Value)];
        var data = skill_entity.Data;

        ModClass.I.SkillV2.NewSkillStarter(id,
            data.Get<SkillCaster>().value,
            data.Get<SkillTargetObj>().value,
            data.Get<SkillStrength>().value
        );
        if (context.trigger_times >= entity_modifiers.GetComponent<CastCountModifier>().Value)
        {
            skill_entity.AddTag<TagRecycle>();
        }
    }

    private void spawn_ground_thorn_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var entity = GroundThornCasterEntity.NewEntity();

        var user_ae = context.user;

        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<SkillTargetObj>().value = context.target;

        GroundThornCasterEntity.ApplyModifiers(entity,
            user_ae.GetSkillEntityModifiers(GroundThornCasterEntity.id,
                GroundThornCasterEntity.default_modifier_container));
    }
    [Hotfixable]
    private void spawn_circle_ground_thorn(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var caster_data = skill_entity.Data;

        var salvo_count = entity_modifiers.GetComponent<SalvoCountModifier>().Value;

        var radius = Mathf.Max(1, caster_data.Get<Radius>().Value * context.trigger_times / salvo_count);

        var count = (int)(radius * 2 * Mathf.PI);
        for (int i = 0; i < count; i++)
        {
            var rad = i * 2 * Mathf.PI / count;
            var entity = SingleGroundThornEntity.NewEntity();

            var user_ae = caster_data.Get<SkillCaster>().value;
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = caster_data.Get<SkillStrength>().value;
            data.Get<Position>().value = user_ae.Base.current_position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

            SingleGroundThornEntity.ApplyModifiers(entity,
                user_ae.GetSkillEntityModifiers(SingleGroundThornEntity.id,
                    SingleGroundThornEntity.default_modifier_container));
        }
        if (context.trigger_times >= salvo_count)
        {
            skill_entity.AddTag<TagRecycle>();
        }
    }
    private void spawn_circle_ground_thorn_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var entity = CircleGroundThornCasterEntity.NewEntity();

        var user_ae = context.user;
        var data = entity.Data;

        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Radius>().Value = (user_ae.Base.current_position - context.target.current_position).sqrMagnitude;

        CircleGroundThornCasterEntity.ApplyModifiers(entity,
            user_ae.GetSkillEntityModifiers(CircleGroundThornCasterEntity.id,
                CircleGroundThornCasterEntity.default_modifier_container));
    }
    [Hotfixable]
    private void spawn_single_ground_thorn(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var entity = SingleGroundThornEntity.NewEntity();

        var ground_thorn_caster_data = skill_entity.Data;

        var caster = ground_thorn_caster_data.Get<SkillCaster>().value;
            
        var data = entity.Data;
        data.Get<SkillCaster>().value =caster;
        data.Get<SkillStrength>().value = ground_thorn_caster_data.Get<SkillStrength>().value;
        data.Get<Position>().value = ground_thorn_caster_data.Get<Position>().value;

        SingleGroundThornEntity.ApplyModifiers(entity,
            caster.GetSkillEntityModifiers(SingleGroundThornEntity.id,
                SingleGroundThornEntity.default_modifier_container));
    }

    private void spawn_line_ground_thorn_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var entity = LineGroundThornCasterEntity.NewEntity();

            var user_ae = context.user;
            var data = entity.Data;

            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = user_ae.Base.current_position;

            var dir = (context.target.current_position - user_ae.Base.current_position)
                      + new Vector2(
                          Randy.randomFloat(1-salvo_count, salvo_count-1),
                          Randy.randomFloat(1-salvo_count, salvo_count-1)
                      );
            data.Get<Rotation>().value = dir;

            LineGroundThornCasterEntity.ApplyModifiers(entity,
                user_ae.GetSkillEntityModifiers(GroundThornCasterEntity.id,
                    GroundThornCasterEntity.default_modifier_container));
        }
    }

    private void ground_thorn_apply_force(ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var target = context.obj;
        if (!target.isAlive()) return;
        if (!target.isActor()) return;
        var a = target.a;
        var dp = target.current_position - skill_entity.GetComponent<Position>().v2;

        var dist = dp.sqrMagnitude;
        float force = 1f / Mathf.Exp(dist);
        var norm_dp = dp.normalized;
        a.GetExtend().GetForce(skill_entity.GetComponent<SkillCaster>().AsActor, norm_dp.x * force, norm_dp.y * force,
            force);
    }

    private void spawn_single_ground_thorn(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var entity = SingleGroundThornEntity.NewEntity();

            var user_ae = context.user;
            
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = context.target.current_position;
            if (i != 0)
            {
                var edge = Mathf.Sqrt(salvo_count);
                data.Get<Position>().v2 += Randy.randomPointOnCircle(0, edge);
            }

            SingleGroundThornEntity.ApplyModifiers(entity,
                user_ae.GetSkillEntityModifiers(SingleGroundThornEntity.id,
                    SingleGroundThornEntity.default_modifier_container));
        }
    }
}