using Cultiway.Abstract;
using Cultiway.Content.Skills.Modifiers;
using Cultiway.Core;
using Cultiway.Core.SkillLib;
using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.SkillLib.Components.Position;
using Rotation = Cultiway.Core.SkillLib.Components.Rotation;

namespace Cultiway.Content.Skills;

public class SkillTriggerActions : ExtendLibrary<Asset, SkillTriggerActions>
{
    private static DamageComposition fireball_collision_damage_composition = new([0, 0, 0, 100, 0, 0]);
    public static  ActionMeta<StartObjTrigger, BaseSimObject> FireballStarter { get; private set; }
    public static  ActionMeta<TimeIntervalTrigger, float> FireballCaster { get; private set; }
    public static  ActionMeta<ObjCollisionTrigger, BaseSimObject> FireballCollision { get; private set; }
    public static  ActionMeta<ObjCollisionTrigger, BaseSimObject> FireballExplosion { get; private set; }


    public static ActionMeta<TimeReachTrigger, float> TimeReachEntityRecycle    { get; private set; }
    public static ActionMeta<AnimLoopEndTrigger, int> AnimLoopEndEntityRecycle  { get; private set; }
    public static ActionMeta<TimeReachTrigger, float> ObjCollisionTriggerEnable { get; private set; }

    protected override void OnInit()
    {
        FireballStarter =
            new ActionMeta<StartObjTrigger, BaseSimObject>.Builder<StartObjActionContainerInfo>(NewFireballCaster)
                .AllowModifier<CastInterval, float>(new CastInterval()
                {
                    Value = 0.5f
                })
                .AllowModifier<CastNum, int>(new CastNum()
                {
                    Value = 1
                }).Build();
        FireballCaster =
            new ActionMeta<TimeIntervalTrigger, float>.Builder<TimeIntervalActionContainerInfo>(CastFireball).Build();
        FireballCollision =
            new ActionMeta<ObjCollisionTrigger, BaseSimObject>.Builder<ObjCollisionActionContainerInfo>(
                FireballSingleCollision).Build();
        FireballExplosion =
            new ActionMeta<ObjCollisionTrigger, BaseSimObject>.Builder<ObjCollisionActionContainerInfo>(
                FireballSingleExplosionEffect).Build();
        TimeReachEntityRecycle =
            new ActionMeta<TimeReachTrigger, float>.Builder<TimeReachActionContainerInfo>(Recycle).Build();
        AnimLoopEndEntityRecycle =
            new ActionMeta<AnimLoopEndTrigger, int>.Builder<AnimLoopEndActionContainerInfo>(Recycle).Build();
        ObjCollisionTriggerEnable =
            new ActionMeta<TimeReachTrigger, float>.Builder<TimeReachActionContainerInfo>(EnableObjCollisionTrigger)
                .Build();
    }

    [Hotfixable]
    private void FireballSingleExplosionEffect(ref ObjCollisionTrigger trigger, ref Entity skill_entity,
                                               ref Entity              action_entity)
    {
        ref var skill_info = ref skill_entity.GetComponent<SkillInfo>();

        if (trigger.Val.isActor())
        {
            ref var center = ref skill_entity.GetComponent<Position>();
            var a = trigger.Val.a;
            var radius = trigger.radius;
            var delta = a.currentPosition - center.as_v2;
            var dir = delta.normalized;
            var s_dist = delta.sqrMagnitude;

            trigger.Val.a.GetExtend().GetHit(skill_info.energy, ref fireball_collision_damage_composition,
                skill_info.user?.Base);
            trigger.Val.a.addForce(radius * dir.x / (radius + s_dist), radius * dir.y / (radius + s_dist),
                radius                            / (radius + s_dist));
        }
    }

    private void EnableObjCollisionTrigger(ref TimeReachTrigger trigger, ref Entity skill_entity,
                                           ref Entity           action_entity)
    {
        skill_entity.GetComponent<ObjCollisionTrigger>().Enabled = true;
    }

    [Hotfixable]
    private void FireballSingleCollision(ref ObjCollisionTrigger trigger, ref Entity skill_entity,
                                         ref Entity              action_entity)
    {
        ref var skill_info = ref skill_entity.GetComponent<SkillInfo>();
        if (trigger.Val.isActor())
        {
            trigger.Val.a.GetExtend().GetHit(skill_info.energy, ref fireball_collision_damage_composition,
                skill_info.user?.Base);
        }

        if (!skill_entity.Tags.Has<RecycleTag>())
        {
            skill_entity.AddTag<RecycleTag>();
            var explosion_entity = SkillEntities.FireballExplosion.NewEntity(ref skill_info);
            explosion_entity.GetComponent<Position>().value = skill_entity.GetComponent<Position>().value;
            explosion_entity.GetComponent<ObjCollisionTrigger>().ActionContainer =
                skill_info.user.GetSkillActionEntity(nameof(FireballExplosion),
                    FireballExplosion.DefaultActionContainer);
        }
    }

    [Hotfixable]
    private void CastFireball(ref TimeIntervalTrigger trigger, ref Entity skill_entity, ref Entity action_entity)
    {
        ref var skill_info = ref skill_entity.GetComponent<SkillInfo>();
        var fireball_entity = SkillEntities.Fireball.NewEntity(ref skill_info);

        fireball_entity.GetComponent<Position>().value = skill_entity.GetComponent<Position>().value;
        fireball_entity.GetComponent<Rotation>().value = skill_entity.GetComponent<Rotation>().value;

        fireball_entity.GetComponent<ObjCollisionTrigger>().ActionContainer =
            skill_info.user.GetSkillActionEntity(nameof(FireballSingleCollision),
                FireballCollision.DefaultActionContainer);
    }

    private void Recycle(ref TimeReachTrigger trigger, ref Entity skill_entity, ref Entity action_entity)
    {
        skill_entity.AddTag<RecycleTag>();
    }

    private void Recycle(ref AnimLoopEndTrigger trigger, ref Entity skill_entity, ref Entity action_entity)
    {
        skill_entity.AddTag<RecycleTag>();
    }

    private void NewFireballCaster(ref StartObjTrigger pTrigger, ref Entity skill_entity, ref Entity action_entity)
    {
        ref var skill_info = ref skill_entity.GetComponent<SkillInfo>();

        var fireball_caster = SkillEntities.FireballCaster.NewEntity(ref skill_info);

        ref var interval_trigger = ref fireball_caster.GetComponent<TimeIntervalTrigger>();
        float interval_value = interval_trigger.interval;

        if (action_entity.TryGetComponent(out CastInterval interval))
        {
            interval_trigger.interval = interval.Value;
            interval_value = interval.Value;
        }

        if (action_entity.TryGetComponent(out CastNum cast_num))
        {
            fireball_caster.GetComponent<TimeReachTrigger>().target_time = cast_num.Value * interval_value;
        }

        var a = skill_info.user.Base;
        var loc = a.currentPosition;

        ref var pos = ref fireball_caster.GetComponent<Position>();
        pos.x = loc.x;
        pos.y = loc.y;
        pos.z = a.getZ();

        ref var rot = ref fireball_caster.GetComponent<Rotation>();
        rot.value = Quaternion.Euler(0, 0,
            Vector2.SignedAngle(Vector2.right,
                new(skill_info.target_tile.posV.x - pos.x, skill_info.target_tile.posV.y - pos.y)));
    }
}