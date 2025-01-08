using System.Text;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Modifiers;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.Components.Position;

namespace Cultiway.Content.Skills;

internal class CommonWeaponSkills : ICanInit, ICanReload
{
    public static SkillEntityMeta                                               RotateForwardWeaponEntity;
    public static SkillEntityMeta                                               BangWeaponEntity;
    public static TriggerActionMeta<StartSkillTrigger, StartSkillContext>       StartWeaponSkill;
    public static TriggerActionMeta<TimeReachTrigger, TimeReachContext>         TimeReachWeaponReturn;
    public static TriggerActionMeta<PositionReachTrigger, PositionReachContext> PositionReachWeaponEndFall;

    private static ElementComposition weapon_damage_composition = new([100, 0, 0, 0, 0, 0, 0, 0]);

    public void Init()
    {
        StartWeaponSkill = TriggerActionMeta<StartSkillTrigger, StartSkillContext>.StartBuild(nameof(StartWeaponSkill))
            .AppendAction(spawn_weapon_entity)
            .AllowModifier<ScaleModifier, float>(new ScaleModifier(1))
            .Build();
        TimeReachWeaponReturn = TriggerActionMeta<TimeReachTrigger, TimeReachContext>
            .StartBuild(nameof(TimeReachWeaponReturn))
            .AppendAction(switch_trajectory_back)
            .Build();
        PositionReachWeaponEndFall = TriggerActionMeta<PositionReachTrigger, PositionReachContext>
            .StartBuild(nameof(PositionReachWeaponEndFall))
            .AppendAction(bang_tiles)
            .Build();

        RotateForwardWeaponEntity = SkillEntityMeta.StartBuild(nameof(RotateForwardWeaponEntity))
            .AddAnim([SpriteTextureLoader.getSprite("actors/races/items/w_flame_sword_base")], 0.2f, 1f, false)
            .AddComponent(new SkillTargetPos())
            .AddComponent(new SkillTargetObj())
            .SetTrajectory(Trajectories.GoTowardsTargetPosWithRotation, 20, 360)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = TriggerActions.GetCollisionDamageActionMeta(weapon_damage_composition)
            }, 2.5f)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                friend = true,
                Enabled = false,
                TriggerActionMeta = TriggerActions.GetRecycleActionMetaOnCollideCaster()
            }, 3)
            .AddTimeReachTrigger(10, TimeReachWeaponReturn)
            .AddComponent(new AliveTimeLimit()
            {
                value = 20
            })
            .Build();
        BangWeaponEntity = SkillEntityMeta.StartBuild(nameof(BangWeaponEntity))
            .AddAnim([SpriteTextureLoader.getSprite("actors/races/items/w_flame_sword_base")], 0.2f, 1f, false)
            .AddComponent(new SkillTargetPos())
            .SetTrajectory(Trajectories.FallToGround, 80, 1440)
            .AddPositionReachTrigger(1, PositionReachWeaponEndFall)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true,
                building = true,
                enemy = true,
                TriggerActionMeta = TriggerActions.GetCollisionDamageActionMeta(weapon_damage_composition)
            }, 3)
            .Build();
    }

    [Hotfixable]
    public void OnReload()
    {
    }

    private void bang_tiles(ref PositionReachTrigger trigger, ref PositionReachContext context, Entity skill_entity,
                            Entity                   modifier_container, Entity entity_modifiers)
    {
        var pos = skill_entity.GetComponent<Position>();
        WorldTile tile = WorldboxGame.I.GetTile((int)pos.x, (int)pos.y);
        if (tile != null)
        {
            var radius = 3f;
            foreach (Entity trigger_entity in skill_entity.ChildEntities)
            {
                if (trigger_entity.HasComponent<ObjCollisionTrigger>())
                {
                    trigger_entity.GetComponent<ObjCollisionTrigger>().Enabled = true;
                    radius = trigger_entity.GetComponent<ColliderSphere>().radius;
                }
            }
                
            WorldboxGame.I.DamageWorld(tile, (int)radius, WorldboxGame.Terraforms.EarthquakeBurn,
                skill_entity.GetComponent<SkillCaster>().AsActor);
        }
        skill_entity.AddTag<TagRecycle>();
    }

    private void switch_trajectory_back(ref TimeReachTrigger trigger, ref TimeReachContext context, Entity skill_entity,
                                        Entity               modifier_container, Entity entity_modifiers)
    {
        skill_entity.GetComponent<Trajectory>().meta = Trajectories.GoTowardsTargetObjWithRotation;
        skill_entity.GetComponent<SkillTargetObj>().value = skill_entity.GetComponent<SkillCaster>().value.Base;

        foreach (Entity trigger_entity in skill_entity.ChildEntities)
            if (trigger_entity.HasComponent<ObjCollisionTrigger>())
                trigger_entity.GetComponent<ObjCollisionTrigger>().Enabled = true;
    }
    [Hotfixable]
    private void spawn_weapon_entity(ref StartSkillTrigger trigger, ref StartSkillContext context, Entity skill_entity,
                                     Entity                modifier_container, Entity entity_modifiers)
    {
        if (!context.user.Base.hasWeapon()) return;
        var bang_or_rotate = Toolbox.randomBool();
        Entity weapon_entity = bang_or_rotate ? BangWeaponEntity.NewEntity() : RotateForwardWeaponEntity.NewEntity();
        EntityData data = weapon_entity.Data;


        ActorExtend user_ae = context.user;
        Actor user = user_ae.Base;
        BaseSimObject target = context.target;
        data.Get<SkillCaster>().value = user_ae;
        data.Get<SkillTargetPos>().Setup(target, new(0,0,-target.getZ()));
        data.Get<SkillStrength>().value = bang_or_rotate ? context.strength * 8 : context.strength;
        data.Get<Position>().value = bang_or_rotate ? new Vector3(target.currentPosition.x, target.currentPosition.y, target.getZ()+10) : user.currentPosition;
        data.Get<AnimData>().frames = [user.getWeaponAsset().getSprite(user.getWeapon())];//ActorAnimationLoader.getItem(user.getWeaponTextureId());
        // data.Get<Rotation>().Setup(user, target);
        var modifier_data = modifier_container.Data;
        
        var scale = modifier_data.Get<ScaleModifier>().Value;
        data.Get<Scale>().value *= scale;
        
        foreach (Entity trigger_entity in weapon_entity.ChildEntities)
            if (trigger_entity.HasComponent<ColliderSphere>())
                trigger_entity.GetComponent<ColliderSphere>().radius *= scale;
    }
}