using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Skills;

public struct HitExplosion(bool value) : IModifier<bool>
{
    public bool Value { get; set; } = value;
}

public struct TagTriggered : ITag;

public class FireballSkills : ICanInit
{
    public static SkillEntityMeta UntrajedFireball { get; private set; }

    public static SkillEntityMeta NewFireballCaster
    {
        get;
        private set;
    }
    public static SkillEntityMeta Explosion { get; private set; }
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> TimeIntervalCast { get; private set; }
    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> FireballCollision { get; private set; }
        = TriggerActions.GetCollisionDamageActionMeta(new ElementComposition([0, 0, 0, 100, 0, 0, 0, 0]),
            nameof(FireballCollision), addition_action_on_collision);

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> ExplosionCollision { get; private set; }
        = TriggerActions.GetCollisionDamageActionMeta(new ElementComposition([0, 0, 0, 100, 0, 0, 0, 0]),
            nameof(ExplosionCollision));
    [Hotfixable]
    private static void addition_action_on_collision(ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        if (entity_modifiers.GetComponent<HitExplosion>().Value && !skill_entity.Tags.Has<TagTriggered>())
        {
            var entity = Explosion.NewEntity();

            var fireball_data = skill_entity.Data;
            var user_ae = fireball_data.Get<SkillCaster>().value;
            
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = fireball_data.Get<SkillStrength>().value;
            data.Get<Position>().value = fireball_data.Get<Position>().value;
            
            Explosion.ApplyModifiers(entity, user_ae.GetSkillEntityModifiers(Explosion.id, Explosion.default_modifier_container));

            skill_entity.AddTag<TagTriggered>();
        }
    }

    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartFireball { get; private set; }
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartFireballCaster { get; private set; }
    public void Init()
    {
        Explosion = SkillEntityMeta.StartBuild(nameof(Explosion))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"), 0.4f, loop: false)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = ExplosionCollision
            }, 2)
            .AddTimeReachTrigger(3, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .Build();
        UntrajedFireball = SkillEntityMeta.StartBuild(nameof(UntrajedFireball))
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball"), 0.2f)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger()
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = FireballCollision
            }, 1)
            .SetTrajectory(Trajectories.GoForward, 20)
            .AllowModifier<HitExplosion, bool>(new HitExplosion(true))
            .AddTimeReachTrigger(5, TriggerActions.GetRecycleActionMeta<TimeReachTrigger, TimeReachContext>())
            .Build();
        TimeIntervalCast = TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext>
            .StartBuild(nameof(TimeIntervalCast))
            .AppendAction(time_interval_cast_fireball)
            .Build();
        NewFireballCaster = SkillEntityMeta.StartBuild(nameof(NewFireballCaster))
            .AddTimeIntervalTrigger(0.3f, TimeIntervalCast)
            .AddComponent(new SkillTargetObj())
            .AllowModifier<CastCountModifier, int>(new CastCountModifier(3))
            .Build();
        StartFireball = TriggerActionMeta<StartSkillTrigger, StartSkillContext>.StartBuild(nameof(StartFireball))
            .AppendAction(cast_fireball)
            .AllowModifier<SalvoCountModifier, int>(new SalvoCountModifier(5))
            .Build();
        StartFireballCaster = TriggerActionMeta<StartSkillTrigger, StartSkillContext>
            .StartBuild(nameof(StartFireballCaster))
            .AppendAction(spawn_fireball_caster)
            .Build();
    }

    private void spawn_fireball_caster(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var entity = NewFireballCaster.NewEntity();
        var user_ae = context.user;
        var data = entity.Data;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillTargetObj>().value = context.target;
        data.Get<SkillStrength>().value = context.strength;
        NewFireballCaster.ApplyModifiers(entity, user_ae.GetSkillEntityModifiers(NewFireballCaster.id, NewFireballCaster.default_modifier_container));
    }
    [Hotfixable]
    private void time_interval_cast_fireball(ref TimeIntervalTrigger trigger, ref TimeIntervalContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var caster_data = skill_entity.Data;
        var user_ae = caster_data.Get<SkillCaster>().value;

        if (!user_ae.CastSkillV2(StartFireball.id, caster_data.Get<SkillTargetObj>().value, true))
        {
            skill_entity.AddTag<TagRecycle>();
        }

        if (context.trigger_times >= entity_modifiers.GetComponent<CastCountModifier>().Value)
        {
            skill_entity.AddTag<TagRecycle>();
        }
    }

    [Hotfixable]
    private void cast_fireball(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity, Entity action_modifiers, Entity entity_modifiers)
    {
        var salvo_count = action_modifiers.GetComponent<SalvoCountModifier>().Value;
        for (int i = 0; i < salvo_count; i++)
        {
            var entity = UntrajedFireball.NewEntity();

            var user_ae = context.user;
        
            var data = entity.Data;
            data.Get<SkillCaster>().value = user_ae;
            data.Get<SkillStrength>().value = context.strength;
            data.Get<Position>().v2 = user_ae.Base.current_position;
            
            // 单发的时候，直接朝向目标
            // 多发的时候，第一发朝向目标，其他的就往目标附近发射，角度范围取决于齐射数量
            if (i > 0)
            {
                var offset_range = salvo_count * salvo_count;
                data.Get<Rotation>().Setup(user_ae.Base, context.target,
                    new Vector3(Randy.randomFloat(-offset_range, offset_range),
                        Randy.randomFloat(-offset_range, offset_range), -context.target.getHeight()));
            }
            else
            {
                data.Get<Rotation>().Setup(user_ae.Base, context.target, new Vector3(0,0,1) * context.target.getHeight());
            }
        
        
            UntrajedFireball.ApplyModifiers(entity, 
                user_ae.GetSkillEntityModifiers(UntrajedFireball.id, UntrajedFireball.default_modifier_container));
        }
    }
}