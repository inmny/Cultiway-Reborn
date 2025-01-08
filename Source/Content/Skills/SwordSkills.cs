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
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartAllGoldSword { get; private set; }
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnGoldSword { get; private set; }
    public void Init()
    {
        UntrajedGoldSwordEntity = SkillEntityMeta.StartBuild(nameof(UntrajedGoldSwordEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/gold_sword"), 0.1f, 0.2f, false)
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
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartSelfSurroundGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundGoldSword))
            .AppendAction(spawn_self_surround_gold_sword)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        RandomSpawnGoldSword = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnGoldSword))
            .AppendAction(random_spawn_gold_sword)
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        GoldSwordCasterEntity = SkillEntityMeta.StartBuild(nameof(GoldSwordCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnGoldSword)
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
            .AppendModifierApplication(gold_sword_caster_modifiers_application)
            .Build();
        StartAllGoldSword = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllGoldSword))
            .AppendAction(spawn_gold_sword_caster)
            .Build();

        starters = [StartForwardGoldSword.id, StartSelfSurroundGoldSword.id];
    }

    private void spawn_gold_sword_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        Entity entity = GoldSwordCasterEntity.NewEntity();

        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;

        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<SkillTargetObj>().value = context.target;

        GoldSwordCasterEntity.ApplyModifiers(entity,
            context.user.GetSkillEntityModifiers(GoldSwordCasterEntity.id,
                GoldSwordCasterEntity.default_modifier_container));
    }

    private static string[] starters;
    private void gold_sword_caster_modifiers_application(Entity entity, Entity modifiers)
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

    private void random_spawn_gold_sword(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        
        string id = starters[Toolbox.randomInt(0, entity_modifiers.GetComponent<StageModifier>().Value)];

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
                radius = Toolbox.DistVec2Float(user.currentPosition, context.target.currentPosition);
            else
                radius = Toolbox.randomFloat(1, user.stats[S.range]);
            entity.AddComponent(new SurroundRadius(radius));

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value =
                user.currentPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
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
            data.Get<Position>().value = user.currentPosition;
            data.Get<Velocity>().scale *= 4;
            data.Get<Trajectory>().meta = Trajectories.GoForward;
            data.Get<Rotation>().Setup(user, context.target ?? user,
                new Vector3(Toolbox.randomFloat(-salvo_count, salvo_count),
                    Toolbox.randomFloat(-salvo_count, salvo_count)));

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