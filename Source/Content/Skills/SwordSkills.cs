using System.Linq;
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

public class SwordSkills : ICanInit, ICanReload
{
    public static SkillEntityMeta UntrajedGoldSwordEntity { get; private set; }
    public static SkillEntityMeta GoldSwordCasterEntity { get; private set; }

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GoldSwordCollisionActionMeta { get;
        private set;
    } =
        TriggerActions.GetCollisionDamageActionMeta(new([100, 0, 0, 0, 0, 0, 0, 0]), nameof(UntrajedGoldSwordEntity));

    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartForwardGoldSword { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSelfSurroundGoldSword { get;
        private set;
    }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSpecialGoldSword { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllGoldSword { get; private set; }
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnGoldSword { get; private set; }
    public static TrajectoryMeta SpecialGoldSwordTraj { get; private set; }
    public void Init()
    {
        UntrajedGoldSwordEntity = SkillEntityMeta.StartBuild(nameof(UntrajedGoldSwordEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/gold_sword"), 0.3f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = GoldSwordCollisionActionMeta
            }, 0.5f)
            .AddTimeReachTrigger(3, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AllowModifier<TimeScaleModifier, float>(new TimeScaleModifier(1))
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AppendModifierApplication(gold_sword_modifiers_application)
            .Build();
        StartForwardGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardGoldSword))
            .AppendAction(spawn_forward_gold_sword)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(8))
            .Build();
        StartSelfSurroundGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundGoldSword))
            .AppendAction(spawn_self_surround_gold_sword)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(8))
            .Build();
        StartSpecialGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSpecialGoldSword))
            .AppendAction(spawn_super_gold_sword)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(8))
            .Build();
        RandomSpawnGoldSword = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnGoldSword))
            .AppendAction(random_spawn_gold_sword)
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        GoldSwordCasterEntity = SkillEntityMeta.StartBuild(nameof(GoldSwordCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnGoldSword)
            .AddComponent(new SkillTargetObj())
            .NewTrigger(new CastCountReachTrigger()
            {
                TargetValue = 1,
                ExpectedResult = CompareResult.GreaterThanTarget,
                TriggerActionMeta = TriggerActions.GetRecycleActionMeta<CastCountReachTrigger, CastCountReachContext>()
            }, out _, new CastCountReachContext())
            .AllowModifier<CastCountModifier, int>(new CastCountModifier(1))
            .AllowModifier<StageModifier, int>(new StageModifier(1))
            .AppendModifierApplication(gold_sword_caster_modifiers_application)
            .Build();
        StartAllGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllGoldSword))
            .AppendAction(spawn_gold_sword_caster)
            .Build();
        SpecialGoldSwordTraj = new TrajectoryMeta()
        {
            towards_velocity = true,
            get_delta_position = special_gold_sword_traj
        };

        starters = [StartForwardGoldSword.id, StartSelfSurroundGoldSword.id, StartSpecialGoldSword.id];
    }
    [Hotfixable]
    private Vector3 special_gold_sword_traj(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        var data = skill_entity.Data;
        ref var velo = ref data.Get<Velocity>();
        ref var target_pos = ref data.Get<SkillTargetPos>();
        var delta_pos = target_pos.v2 - pos.v2;
        if (velo.scale2.sqrMagnitude * dt> delta_pos.sqrMagnitude)
        {
            var user = data.Get<SkillCaster>().AsActor;
            var angle_rad = Randy.randomFloat(0, 360)* Mathf.Deg2Rad;
            var radius = Randy.randomFloat(0, data.Get<SurroundRadius>().value);
            target_pos.v2 = user.current_position + new Vector2(Mathf.Cos(angle_rad), Mathf.Sin(angle_rad)) * radius;
            delta_pos = target_pos.v2 - pos.v2;
        }
        //ModClass.LogInfo($"[{skill_entity.Id}]: {velo.scale2}, {delta_pos.normalized}, {delta_pos.normalized * velo.scale2.magnitude * dt}");
        velo.scale2 += delta_pos.normalized * Mathf.Max(delta_pos.magnitude, 40) * dt;
        return velo.scale2 * dt;
    }
    [Hotfixable]
    private void spawn_super_gold_sword(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
            Entity entity = UntrajedGoldSwordEntity.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;
            float radius;
            if (context.target != null)
                radius = Toolbox.DistVec2Float(user.current_position, context.target.current_position);
            else
                radius = Randy.randomFloat(4, user.stats[S.range]);

            var angle_rad = Randy.randomFloat(0, 360)* Mathf.Deg2Rad;
            entity.AddComponent(new SurroundRadius(radius));
            entity.AddComponent(new SkillTargetPos()
            {
                v2 = user.current_position + new Vector2(Mathf.Cos(angle_rad), Mathf.Sin(angle_rad)) * radius
            });
            angle_rad = Randy.randomFloat(0, 360)* Mathf.Deg2Rad;

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value =
                user.current_position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
            data.Get<Trajectory>().meta = SpecialGoldSwordTraj;
            data.Get<Velocity>().scale.Scale(new Vector3(Mathf.Cos(angle_rad), Mathf.Sin(angle_rad)) * Mathf.Sqrt(radius));

            foreach (Entity trigger_entity in entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<TimeReachTrigger>())
                    trigger_entity.GetComponent<TimeReachTrigger>().target_time *=
                        radius * Mathf.PI * data.Get<Velocity>().scale.magnitude;
            }

            UntrajedGoldSwordEntity.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(UntrajedGoldSwordEntity.id,
                    UntrajedGoldSwordEntity.default_modifier_container));
        }
    }

    private void spawn_gold_sword_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        Entity entity = GoldSwordCasterEntity.NewEntity();

        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;

        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<SkillTargetObj>().value = context.target;

        GoldSwordCasterEntity.ApplyModifiers(entity,
            context.user.GetSkillEntityModifiers(GoldSwordCasterEntity.id,
                GoldSwordCasterEntity.default_modifier_container));
    }

    internal static string[] starters;
    [Hotfixable]
    private void gold_sword_caster_modifiers_application(Entity entity, Entity modifiers)
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
    [Hotfixable]
    private void random_spawn_gold_sword(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        
        string id = starters[Randy.randomInt(0, entity_modifiers.GetComponent<StageModifier>().Value)];
        //ModClass.LogInfo($"{starters.ToList().FindIndex(x=>x==id)}/{entity_modifiers.GetComponent<StageModifier>().Value}/{starters.Length}");
        var data = skill_entity.Data;

        ModClass.I.SkillV2.NewSkillStarter(id,
            data.Get<SkillCaster>().value,
            data.Get<SkillTargetObj>().value,
            data.Get<SkillStrength>().value
        );
    }

    private void spawn_self_surround_gold_sword(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;

        for (int i = 0; i < salvo_count; i++)
        {
            var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
            Entity entity = UntrajedGoldSwordEntity.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;
            float radius;
            if (context.target != null)
                radius = Toolbox.DistVec2Float(user.current_position, context.target.current_position);
            else
                radius = Randy.randomFloat(4, user.stats[S.range]);
            entity.AddComponent(new SurroundRadius(radius));

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value =
                user.current_position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
            data.Get<Trajectory>().meta = Trajectories.SelfSurround;
            data.Get<Velocity>().scale.Scale(Vector3.one * Mathf.Sqrt(radius));

            foreach (Entity trigger_entity in entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<TimeReachTrigger>())
                    trigger_entity.GetComponent<TimeReachTrigger>().target_time *=
                        radius * Mathf.PI * data.Get<Velocity>().scale.magnitude;
            }

            UntrajedGoldSwordEntity.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(UntrajedGoldSwordEntity.id,
                    UntrajedGoldSwordEntity.default_modifier_container));
        }
    }

    private void spawn_forward_gold_sword(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;

        for (int i = 0; i < salvo_count; i++)
        {
            Entity entity = UntrajedGoldSwordEntity.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength * 8;
            data.Get<Position>().value = user.current_position;
            data.Get<Velocity>().scale *= 4;
            data.Get<Trajectory>().meta = Trajectories.GoForward;
            data.Get<Rotation>().Setup(user, context.target ?? user,
                new Vector3(Randy.randomFloat(-salvo_count, salvo_count),
                    Randy.randomFloat(-salvo_count, salvo_count)));

            UntrajedGoldSwordEntity.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(UntrajedGoldSwordEntity.id,
                    UntrajedGoldSwordEntity.default_modifier_container));
        }
    }

    private void gold_sword_modifiers_application(Entity entity, Entity modifiers)
    {
        var data = entity.Data;
        var modifiers_data = modifiers.Data;
        var time_scale = modifiers_data.Get<TimeScaleModifier>().Value;
        var collider_scale = modifiers_data.Get<ScaleModifier>().Value;
        
        data.Get<Scale>().value *= collider_scale;
        foreach (Entity trigger_entity in entity.ChildEntities)
        {
            if (trigger_entity.HasComponent<TimeReachTrigger>())
                trigger_entity.GetComponent<TimeReachTrigger>().target_time *= collider_scale * time_scale;
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= collider_scale;
        }
    }

    public void OnReload()
    {
    }
}