using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Skills;

public class CommonBladeSkills : ICanInit, ICanReload
{
    public static SkillEntityMeta UntrajedFireBladeEntity;
    public static SkillEntityMeta FireBladeCasterEntity;

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> FireBladeCollisionActionMeta =
        TriggerActions.GetCollisionDamageActionMeta(new([0, 0, 0, 100, 0, 0, 0, 0]));
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSelfSurroundFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartOutSurroundFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartForwardFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllFireBlade;

    public void Init()
    {
        UntrajedFireBladeEntity = SkillEntityMeta.StartBuild(nameof(UntrajedFireBladeEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/fire_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = FireBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(fire_blade_modifiers_application)
            .Build();
        FireBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity entity,
                Entity                              action_modifiers, Entity entity_modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        RandomSpawnFireBlade = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnFireBlade))
            .AppendAction(random_spawn_fire_blades)
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        StartSelfSurroundFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundFireBlade))
            .AppendAction(spawn_self_surround_fire_blade)
            .AllowModifier<SalvoCountModifier,int>(new SalvoCountModifier(1))
            .Build();
        StartOutSurroundFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartOutSurroundFireBlade))
            .AppendAction(spawn_out_surround_fire_blade)
            .AllowModifier<SalvoCountModifier,int>(new SalvoCountModifier(1))
            .Build();
        StartForwardFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardFireBlade))
            .AppendAction(spawn_forward_fire_blade)
            .AllowModifier<SalvoCountModifier,int>(new SalvoCountModifier(1))
            .Build();
        StartAllFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllFireBlade))
            .AppendAction(spawn_fire_blade_caster)
            .Build();
        FireBladeCasterEntity = SkillEntityMeta.StartBuild(nameof(FireBladeCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnFireBlade)
            .AddComponent(new SkillTargetObj())
            .AddComponent(new Position())
            .NewTrigger(new CastCountReachTrigger()
            {
                TargetValue = 1,
                ExpectedResult = CompareResult.GreaterThanTarget,
                TriggerActionMeta = TriggerActions.GetRecycleActionMeta<CastCountReachTrigger, CastCountReachContext>()
            }, out _, new CastCountReachContext())
            .AllowModifier<CastCountModifier, int>(new CastCountModifier(1))
            .AllowModifier<StageModifier, int>(new StageModifier(1))
            .AppendModifierApplication(fire_blade_caster_modifiers_application)
            .Build();
    }
    [Hotfixable]
    private void spawn_out_surround_fire_blade(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;

        for (int i = 0; i < salvo_count; i++)
        {
            var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
            Entity entity = UntrajedFireBladeEntity.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;
            float radius = Toolbox.randomFloat(user.stats[S.range], user.stats[S.range] * 2);
            entity.AddComponent(new OutVelocity(radius));

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = user.currentPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
            data.Get<Trajectory>().meta = Trajectories.OutSurround;

            foreach (Entity trigger_entity in entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<TimeReachTrigger>())
                    trigger_entity.GetComponent<TimeReachTrigger>().target_time *= 4;
            }

            UntrajedFireBladeEntity.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(UntrajedFireBladeEntity.id,
                    UntrajedFireBladeEntity.default_modifier_container));
        }
    }

    private void fire_blade_caster_modifiers_application(Entity entity, Entity modifiers)
    {
        foreach (var trigger in entity.ChildEntities)
        {
            if (trigger.HasComponent<CastCountReachTrigger>())
            {
                trigger.GetComponent<CastCountReachTrigger>().TargetValue =
                    modifiers.GetComponent<CastCountModifier>().Value;
            }
        }
    }
    
    private void fire_blade_modifiers_application(Entity entity, Entity modifiers)
    {
        var data = entity.Data;
        var modifiers_data = modifiers.Data;
        
        var scale_mod = modifiers_data.Get<ScaleModifier>().Value;
        data.Get<Scale>().value *= scale_mod;
        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= scale_mod;
        }
    }
    private void spawn_fire_blade_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity modifiers, Entity entity_modifiers)
    {
        Entity entity = FireBladeCasterEntity.NewEntity();
        
        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<SkillTargetObj>().value = context.target;
        
        FireBladeCasterEntity.ApplyModifiers(entity, context.user.GetSkillEntityModifiers(FireBladeCasterEntity.id, FireBladeCasterEntity.default_modifier_container));
    }

    private void random_spawn_fire_blades(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity,
        Entity modifiers, Entity entity_modifiers)
    {
        string id = "";
        switch (Toolbox.randomInt(0, entity_modifiers.GetComponent<StageModifier>().Value))
        {
            case 0:
                id = StartForwardFireBlade.id;
                break;
            case 1:
                id = StartSelfSurroundFireBlade.id;
                break;
            case 2:
                id = StartOutSurroundFireBlade.id;
                break;
        }

        var data = skill_entity.Data;
        
        ModClass.I.SkillV2.NewSkillStarter(id,
            data.Get<SkillCaster>().value,
            data.Get<SkillTargetObj>().value,
            data.Get<SkillStrength>().value
        );
    }
    [Hotfixable]
    private void spawn_forward_fire_blade(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity modifiers, Entity entity_modifiers)
    {
        var salvo_count = modifiers.GetComponent<SalvoCountModifier>().Value;

        for (int i = 0; i < salvo_count; i++)
        {
            Entity entity = UntrajedFireBladeEntity.NewEntity();
        
            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;
        
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = user.currentPosition;
            data.Get<Trajectory>().meta = Trajectories.GoForward;
            data.Get<Rotation>().Setup(user, context.target ?? user,
                new Vector3(Toolbox.randomFloat(-salvo_count, salvo_count),
                    Toolbox.randomFloat(-salvo_count, salvo_count)));

            foreach (Entity trigger_entity in entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<TimeReachTrigger>())
                    trigger_entity.GetComponent<TimeReachTrigger>().target_time *= 4;
            }
            UntrajedFireBladeEntity.ApplyModifiers(entity, context.user.GetSkillEntityModifiers(UntrajedFireBladeEntity.id, UntrajedFireBladeEntity.default_modifier_container));
        }
    }

    [Hotfixable]
    private void spawn_self_surround_fire_blade(
        ref StartSkillTrigger trigger,
        ref StartSkillContext context,
        Entity                starter_entity,
        Entity                modifiers, Entity entity_modifiers)
    {
        var salvo_count = modifiers.GetComponent<SalvoCountModifier>().Value;

        for (int i = 0; i < salvo_count; i++)
        {
            var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
            Entity entity = UntrajedFireBladeEntity.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;
            float radius;
            if (context.target != null)
                radius = Toolbox.DistVec2Float(user.currentPosition, context.target.currentPosition);
            else
                radius = Toolbox.randomFloat(1, user.stats[S.range]);
            entity.AddComponent(new SurroundRadius(radius));

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = user.currentPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
            data.Get<Trajectory>().meta = Trajectories.SelfSurround;
            data.Get<Velocity>().scale.Scale(Vector3.one * Mathf.Sqrt(radius));

            foreach (Entity trigger_entity in entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<TimeReachTrigger>())
                    trigger_entity.GetComponent<TimeReachTrigger>().target_time *=
                        radius * Mathf.PI * data.Get<Velocity>().scale.magnitude;
            }

            UntrajedFireBladeEntity.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(UntrajedFireBladeEntity.id,
                    UntrajedFireBladeEntity.default_modifier_container));
        }
    }
    [Hotfixable]
    public void OnReload()
    {
    }
}