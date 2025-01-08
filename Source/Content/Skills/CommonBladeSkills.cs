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
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartSelfSurroundFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartOutSurroundFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartForwardFireBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartAllFireBlade;

    public static SkillEntityMeta UntrajedGoldBladeEntity;
    public static SkillEntityMeta GoldBladeCasterEntity;

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> GoldBladeCollisionActionMeta =
        TriggerActions.GetCollisionDamageActionMeta(new([100, 0, 0, 0, 0, 0, 0, 0]));

    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnGoldBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartSelfSurroundGoldBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartOutSurroundGoldBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartForwardGoldBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartAllGoldBlade;

    public static SkillEntityMeta UntrajedWaterBladeEntity;
    public static SkillEntityMeta WaterBladeCasterEntity;

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> WaterBladeCollisionActionMeta =
        TriggerActions.GetCollisionDamageActionMeta(new([0, 0, 100, 0, 0, 0, 0, 0]));

    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnWaterBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartSelfSurroundWaterBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartOutSurroundWaterBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartForwardWaterBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartAllWaterBlade;

    public static SkillEntityMeta UntrajedWindBladeEntity;
    public static SkillEntityMeta WindBladeCasterEntity;

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> WindBladeCollisionActionMeta =
        TriggerActions.GetCollisionDamageActionMeta(new([0, 20, 40, 40, 0, 0, 0, 0]));

    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> RandomSpawnWindBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartSelfSurroundWindBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartOutSurroundWindBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartForwardWindBlade;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>     StartAllWindBlade;

    public void Init()
    {
        InitFireBlade();
        InitGoldBlade();
        InitWaterBlade();
        InitWindBlade();
    }

    private void InitFireBlade()
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
            .AppendModifierApplication(blade_modifiers_application)
            .Build();
        FireBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger,          ref ObjCollisionContext context, Entity entity,
                Entity                              action_modifiers, Entity                  entity_modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        StartSelfSurroundFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundFireBlade))
            .AppendAction(GetSpawnSelfSurroundBlade(UntrajedFireBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartOutSurroundFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartOutSurroundFireBlade))
            .AppendAction(GetSpawnOutSurroundBladeAction(UntrajedFireBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartForwardFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardFireBlade))
            .AppendAction(GetSpawnForwardBladeAction(UntrajedFireBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        RandomSpawnFireBlade = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnFireBlade))
            .AppendAction(GetRandomSpawnBladesAction(StartForwardFireBlade,StartSelfSurroundFireBlade,StartOutSurroundFireBlade))
            .AppendAction(TriggerActions.cast_count_increase)
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
            .AppendModifierApplication(blade_caster_modifiers_application)
            .Build();
        StartAllFireBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllFireBlade))
            .AppendAction(GetSpawnBladeCasterAction(FireBladeCasterEntity))
            .Build();
    }

    private void InitWaterBlade()
    {
        UntrajedWaterBladeEntity = SkillEntityMeta.StartBuild(nameof(UntrajedWaterBladeEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/water_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = WaterBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(blade_modifiers_application)
            .Build();
        WaterBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger,          ref ObjCollisionContext context, Entity entity,
                Entity                              action_modifiers, Entity                  entity_modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        StartSelfSurroundWaterBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundWaterBlade))
            .AppendAction(GetSpawnSelfSurroundBlade(UntrajedWaterBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartOutSurroundWaterBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartOutSurroundWaterBlade))
            .AppendAction(GetSpawnOutSurroundBladeAction(UntrajedWaterBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartForwardWaterBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardWaterBlade))
            .AppendAction(GetSpawnForwardBladeAction(UntrajedWaterBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        RandomSpawnWaterBlade = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnWaterBlade))
            .AppendAction(GetRandomSpawnBladesAction(StartForwardWaterBlade,StartSelfSurroundWaterBlade,StartOutSurroundWaterBlade))
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        WaterBladeCasterEntity = SkillEntityMeta.StartBuild(nameof(WaterBladeCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnWaterBlade)
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
            .AppendModifierApplication(blade_caster_modifiers_application)
            .Build();
        StartAllWaterBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllWaterBlade))
            .AppendAction(GetSpawnBladeCasterAction(WaterBladeCasterEntity))
            .Build();
    }

    private void InitGoldBlade()
    {
        UntrajedGoldBladeEntity = SkillEntityMeta.StartBuild(nameof(UntrajedGoldBladeEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/gold_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = GoldBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(blade_modifiers_application)
            .Build();
        GoldBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger,          ref ObjCollisionContext context, Entity entity,
                Entity                              action_modifiers, Entity                  entity_modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        StartSelfSurroundGoldBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundGoldBlade))
            .AppendAction(GetSpawnSelfSurroundBlade(UntrajedGoldBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartOutSurroundGoldBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartOutSurroundGoldBlade))
            .AppendAction(GetSpawnOutSurroundBladeAction(UntrajedGoldBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartForwardGoldBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardGoldBlade))
            .AppendAction(GetSpawnForwardBladeAction(UntrajedGoldBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        RandomSpawnGoldBlade = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnGoldBlade))
            .AppendAction(GetRandomSpawnBladesAction(StartForwardGoldBlade,StartSelfSurroundGoldBlade,StartOutSurroundGoldBlade))
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        GoldBladeCasterEntity = SkillEntityMeta.StartBuild(nameof(GoldBladeCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnGoldBlade)
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
            .AppendModifierApplication(blade_caster_modifiers_application)
            .Build();
        StartAllGoldBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllGoldBlade))
            .AppendAction(GetSpawnBladeCasterAction(GoldBladeCasterEntity))
            .Build();
    }

    private void InitWindBlade()
    {
        UntrajedWindBladeEntity = SkillEntityMeta.StartBuild(nameof(UntrajedWindBladeEntity))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/wind_blade"), 0.1f, 0.2f, false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = WindBladeCollisionActionMeta
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20, 360)
            .AddTimeReachTrigger(1, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .AppendModifierApplication(blade_modifiers_application)
            .Build();
        WindBladeCollisionActionMeta.StartModify()
            .AppendAction(((ref ObjCollisionTrigger trigger,          ref ObjCollisionContext context, Entity entity,
                Entity                              action_modifiers, Entity                  entity_modifiers) =>
            {
                var target = context.obj;
                if (!target.isAlive()) return;
                target.addStatusEffect(WorldboxGame.StatusEffects.Burning.id);
            }));
        StartSelfSurroundWindBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSelfSurroundWindBlade))
            .AppendAction(GetSpawnSelfSurroundBlade(UntrajedWindBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartOutSurroundWindBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartOutSurroundWindBlade))
            .AppendAction(GetSpawnOutSurroundBladeAction(UntrajedWindBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        StartForwardWindBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartForwardWindBlade))
            .AppendAction(GetSpawnForwardBladeAction(UntrajedWindBladeEntity))
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(1))
            .Build();
        RandomSpawnWindBlade = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(RandomSpawnWindBlade))
            .AppendAction(GetRandomSpawnBladesAction(StartForwardWindBlade, StartSelfSurroundWindBlade, StartOutSurroundWindBlade))
            .AppendAction(TriggerActions.cast_count_increase)
            .Build();
        WindBladeCasterEntity = SkillEntityMeta.StartBuild(nameof(WindBladeCasterEntity))
            .AddTimeIntervalTrigger(0.5f, RandomSpawnWindBlade)
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
            .AppendModifierApplication(blade_caster_modifiers_application)
            .Build();
        StartAllWindBlade = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartAllWindBlade))
            .AppendAction(GetSpawnBladeCasterAction(WindBladeCasterEntity))
            .Build();
    }

    private TriggerActionMeta<StartSkillTrigger, StartSkillContext>.ActionType GetSpawnOutSurroundBladeAction(
        SkillEntityMeta blade_entity_meta)
    {
        return (ref StartSkillTrigger trigger,      ref StartSkillContext context,
            Entity                    skill_entity, Entity                action_modifiers, Entity entity_modifiers) =>
        {
            var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;

            for (int i = 0; i < salvo_count; i++)
            {
                var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
                Entity entity = blade_entity_meta.NewEntity();

                ActorExtend user_ae = context.user;
                Actor user = user_ae.Base;
                float radius = Toolbox.randomFloat(user.stats[S.range], user.stats[S.range] * 2);
                entity.AddComponent(new OutVelocity(radius));

                var data = entity.Data;
                data.Get<SkillCaster>().value = user_ae;
                data.Get<SkillStrength>().value = context.strength;
                data.Get<Position>().value =
                    user.currentPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Mathf.Min(radius, 1);
                data.Get<Trajectory>().meta = Trajectories.OutSurround;

                foreach (Entity trigger_entity in entity.ChildEntities)
                {
                    if (trigger_entity.HasComponent<TimeReachTrigger>())
                        trigger_entity.GetComponent<TimeReachTrigger>().target_time *= 4;
                }

                blade_entity_meta.ApplyModifiers(entity,
                    context.user.GetSkillEntityModifiers(blade_entity_meta.id,
                        blade_entity_meta.default_modifier_container));
            }
        };
    }

    private void blade_caster_modifiers_application(Entity entity, Entity modifiers)
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

    private void blade_modifiers_application(Entity entity, Entity modifiers)
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
    private TriggerActionMeta<StartSkillTrigger, StartSkillContext>.ActionType GetSpawnBladeCasterAction(SkillEntityMeta blade_caster_entity_meta)
    {
        return (ref StartSkillTrigger trigger,      ref StartSkillContext context,
            Entity                    skill_entity, Entity                modifiers, Entity entity_modifiers) =>
        {
            Entity entity = blade_caster_entity_meta.NewEntity();

            ActorExtend user_ae = context.user;
            Actor user = user_ae.Base;

            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().value = user.currentPosition;
            data.Get<SkillTargetObj>().value = context.target;

            blade_caster_entity_meta.ApplyModifiers(entity,
                context.user.GetSkillEntityModifiers(blade_caster_entity_meta.id,
                    blade_caster_entity_meta.default_modifier_container));
        };
    }

    private TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>.ActionType GetRandomSpawnBladesAction(
        params TriggerActionMeta<StartSkillTrigger, StartSkillContext>[] starters)
    {
        return (ref TimeIntervalTrigger trigger, ref TimeIntervalContext context,
            Entity                      skill_entity,
            Entity                      modifiers, Entity entity_modifiers) =>
        {
            string id = starters[Toolbox.randomInt(0, entity_modifiers.GetComponent<StageModifier>().Value)].id;

            var data = skill_entity.Data;

            ModClass.I.SkillV2.NewSkillStarter(id,
                data.Get<SkillCaster>().value,
                data.Get<SkillTargetObj>().value,
                data.Get<SkillStrength>().value
            );
        };
    }

    private TriggerActionMeta<StartSkillTrigger, StartSkillContext>.ActionType GetSpawnForwardBladeAction(
        SkillEntityMeta blade_entity_meta)
    {
        return (ref StartSkillTrigger trigger,      ref StartSkillContext context,
            Entity                    skill_entity, Entity                modifiers, Entity entity_modifiers) =>
        {
            var salvo_count = modifiers.GetComponent<SalvoCountModifier>().Value;

            for (int i = 0; i < salvo_count; i++)
            {
                Entity entity = blade_entity_meta.NewEntity();

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

                blade_entity_meta.ApplyModifiers(entity,
                    context.user.GetSkillEntityModifiers(blade_entity_meta.id,
                        blade_entity_meta.default_modifier_container));
            }

        };
    }

    private TriggerActionMeta<StartSkillTrigger, StartSkillContext>.ActionType GetSpawnSelfSurroundBlade(
        SkillEntityMeta blade_entity_meta)
    {
        return (
            ref StartSkillTrigger trigger,
            ref StartSkillContext context,
            Entity                starter_entity,
            Entity                modifiers, Entity entity_modifiers) =>
        {
            var salvo_count = modifiers.GetComponent<SalvoCountModifier>().Value;

            for (int i = 0; i < salvo_count; i++)
            {
                var rad = i * 360f / salvo_count * Mathf.Deg2Rad;
                Entity entity = blade_entity_meta.NewEntity();

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

                blade_entity_meta.ApplyModifiers(entity,
                    context.user.GetSkillEntityModifiers(blade_entity_meta.id,
                        blade_entity_meta.default_modifier_container));
            }
        };
    }

    [Hotfixable]
    public void OnReload()
    {
    }
}