using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.Triggers;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;

namespace Cultiway.Content.Skills;

internal class CommonWeaponSkills : ICanInit, ICanReload
{
    public static  SkillEntityMeta RotateForwardWeaponEntity;
    public static  TriggerActionMeta<StartSkillTrigger, StartSkillContext> StartWeaponSkill;
    public static  TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> ObjCollisionDamage;
    private static DamageComposition weapon_damage_composition = new([100, 0, 0, 0, 0, 0]);

    public void Init()
    {
        StartWeaponSkill = TriggerActionMeta<StartSkillTrigger, StartSkillContext>.StartBuild(nameof(StartWeaponSkill))
            .AppendAction(spawn_weapon_entity)
            .Build();
        ObjCollisionDamage = TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext>
            .StartBuild($"{nameof(CommonWeaponSkills)}.{nameof(ObjCollisionDamage)}")
            .AppendAction(single_damage)
            .Build();
        RotateForwardWeaponEntity = SkillEntityMeta.StartBuild()
            .AddAnim([SpriteTextureLoader.getSprite("actors/races/items/w_flame_sword_base")], 0.2f, 1f, false)
            .AddComponent(new SkillTargetPos())
            .SetTrajectory(Trajectories.GoTowardsTargetPosWithRotation, 20, Mathf.PI)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                enemy = true,
                TriggerActionMeta = ObjCollisionDamage
            }, 1)
            .Build();
    }

    public void OnReload()
    {
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