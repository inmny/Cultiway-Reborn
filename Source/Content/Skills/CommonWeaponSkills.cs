using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using Position = Cultiway.Core.SkillLibV2.Components.Position;

namespace Cultiway.Content.Skills;

internal class CommonWeaponSkills : ICanInit, ICanReload
{
    public static  SkillEntityMeta RotateForwardWeaponEntity;
    public static  TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartWeaponSkill;
    public static TriggerActionMeta<TimeReachTrigger, TimeReachContext> TimeReachWeaponReturn;
    public static  TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> ObjCollisionDamage;
    private static DamageComposition weapon_damage_composition = new([100, 0, 0, 0, 0, 0]);

    public void Init()
    {
        StartWeaponSkill = TriggerActionMeta<StartSkillTrigger, StartSkillContext>.StartBuild(nameof(StartWeaponSkill))
            .AppendAction(spawn_weapon_entity)
            .Build();
        TimeReachWeaponReturn = TriggerActionMeta<TimeReachTrigger, TimeReachContext>
            .StartBuild(nameof(TimeReachWeaponReturn))
            .AppendAction(switch_trajectory_back)
            .Build();
        ObjCollisionDamage = TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>
            .StartBuild($"{nameof(CommonWeaponSkills)}.{nameof(ObjCollisionDamage)}")
            .AppendAction(single_damage)
            .Build();

        RotateForwardWeaponEntity = SkillEntityMeta.StartBuild()
            .AddAnim([SpriteTextureLoader.getSprite("actors/races/items/w_flame_sword_base")], 0.2f, 1f, false)
            .AddComponent(new SkillTargetPos())
            .AddComponent(new SkillTargetObj())
            .SetTrajectory(Trajectories.GoTowardsTargetPosWithRotation, 20, 1440)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                enemy = true,
                TriggerActionMeta = ObjCollisionDamage
            }, 1)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                friend = true,
                Enabled = false,
                TriggerActionMeta = TriggerActions.GetRecycleActionMetaOnCollideCaster()
            }, 1)
            .AddTimeReachTrigger(10, TimeReachWeaponReturn)
            .Build();
    }

    [Hotfixable]
    public void OnReload()
    {
    }

    private void switch_trajectory_back(ref TimeReachTrigger trigger, ref TimeReachContext context, Entity skill_entity,
                                        Entity               modifier_container)
    {
        skill_entity.GetComponent<Trajectory>().meta = Trajectories.GoTowardsTargetObj;
        skill_entity.GetComponent<SkillTargetObj>().value = skill_entity.GetComponent<SkillCaster>().value.Base;

        foreach (Entity trigger_entity in skill_entity.ChildEntities)
            if (trigger_entity.HasComponent<ObjCollisionTrigger>())
                trigger_entity.GetComponent<ObjCollisionTrigger>().Enabled = true;
    }

    private void single_damage(ref ObjCollisionTrigger trigger, ref ObjCollisionContext context, Entity skill_entity,
                               Entity                  modifier_container)
    {
        if (context.obj.isActor())
        {
            ActorExtend target = context.obj.a.GetExtend();
            target.GetHit(skill_entity.GetComponent<SkillStrength>().value, ref weapon_damage_composition,
                skill_entity.GetComponent<SkillCaster>().value.Base);
        }
    }

    private void spawn_weapon_entity(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity,
                                     Entity                modifier_container)
    {
        if (!context.user.Base.hasWeapon()) return;
        Entity weapon_entity = RotateForwardWeaponEntity.NewEntity();
        EntityData data = weapon_entity.Data;


        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        BaseSimObject target = context.target;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillTargetPos>().Setup(target);
        data.Get<SkillStrength>().value = context.strength;
        data.Get<Position>().value = user.currentPosition;
        data.Get<AnimData>().frames[0] = ActorAnimationLoader.getItem(user.getWeaponTextureId());
        // data.Get<Rotation>().Setup(user, target);
    }
}