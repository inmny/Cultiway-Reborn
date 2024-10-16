using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.Triggers;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Examples;

public static class ExampleTriggerActions
{
    private static DamageComposition fireball_damage_composition = new([0, 0, 0, 100, 0, 0]);
    public static  TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartSkillFireball { get; private set; }

    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> TimeIntervalSpawnFireball
    {
        get;
        private set;
    }

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> ObjCollisionDamageAndExplosion
    {
        get;
        private set;
    }

    public static void Init()
    {
        StartSkillFireball = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartSkillFireball))
            .AppendAction(spawn_fireball_caster)
            .AllowModifier<CastSpeedModifier, float>(new CastSpeedModifier(1))
            .AllowModifier<CastNumberModifier, int>(new CastNumberModifier(1))
            .Build();
        TimeIntervalSpawnFireball = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(TimeIntervalSpawnFireball))
            .AppendAction(spawn_fireball)
            .AddCastCountIncrease()
            .AllowModifier<AutoAimModifier, bool>(new AutoAimModifier(false))
            .Build();
        ObjCollisionDamageAndExplosion = TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>
            .StartBuild(nameof(ObjCollisionDamageAndExplosion))
            .AppendAction(damage_and_explosion)
            .Build();
        ModClass.I.SkillV2.RegisterCustomValueReachSystem<CastCountReachTrigger, CastCountReachContext, int>();
    }

    private static void damage_and_explosion(ref ObjCollisionTrigger trigger,      ref ObjCollisionContext context,
                                             Entity                  skill_entity, Entity modifier_container)
    {
        if (context.obj.isActor())
        {
            ActorExtend target = context.obj.a.GetExtend();
            target.GetHit(skill_entity.GetComponent<SkillStrength>().value, ref fireball_damage_composition,
                skill_entity.GetComponent<SkillCaster>().value.Base);
        }

        if (context.JustTriggered) skill_entity.AddTag<TagRecycle>();
    }

    private static void spawn_fireball(ref TimeIntervalTrigger trigger,      ref TimeIntervalContext context,
                                       Entity                  skill_entity, Entity                  modifier_container)
    {
        Entity fireball = ExampleSkillEntities.Fireball.NewEntity();
        EntityData fireball_data = fireball.Data;

        EntityData caster_data = skill_entity.Data;
        ActorExtend user_ae = caster_data.Get<SkillCaster>().value;
        Actor user = user_ae.Base;
        BaseSimObject target = caster_data.Get<SkillTargetObj>().value;

        EntityData modifier_data = modifier_container.Data;
        ref Rotation caster_rot = ref caster_data.Get<Rotation>();

        var auto_aim = modifier_data.Get<AutoAimModifier>().Value;
        if (auto_aim)
        {
            caster_rot.Setup(user, target);
        }

        fireball_data.Get<Rotation>().value = caster_rot.value;
        fireball_data.Get<Position>().value = caster_data.Get<Position>().value;
        fireball_data.Get<SkillCaster>().value = user_ae;
        fireball_data.Get<SkillStrength>().value = caster_data.Get<SkillStrength>().value;

        foreach (Entity trigger_entity in skill_entity.ChildEntities)
            if (trigger_entity.HasComponent<CastCountReachContext>())
                trigger_entity.GetComponent<CastCountReachContext>().Value++;
    }

    private static void spawn_fireball_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity _,
                                              Entity                modifier_container)
    {
        Entity fireball_caster = ExampleSkillEntities.FireballCaster.NewEntity();
        EntityData data = fireball_caster.Data;

        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        BaseSimObject target = context.target;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillTargetObj>().value = target;
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<Rotation>().Setup(user, target);
        EntityData modifier_data = modifier_container.Data;
        ChildEntities triggers = fireball_caster.ChildEntities;
        foreach (Entity trigger_entity in triggers)
            if (trigger_entity.HasComponent<CastCountReachTrigger>())
                trigger_entity.GetComponent<CastCountReachTrigger>().TargetValue =
                    modifier_data.Get<CastNumberModifier>().Value;
            else if (trigger_entity.HasComponent<TimeIntervalTrigger>())
                trigger_entity.GetComponent<TimeIntervalTrigger>().interval_time /=
                    modifier_data.Get<CastSpeedModifier>().Value;
    }
}