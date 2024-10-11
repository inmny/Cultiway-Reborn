using System;
using Cultiway.Abstract;
using Cultiway.Core.SkillLib;
using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Skills;

[Dependency(typeof(SkillTriggerActions), typeof(Trajectories))]
public class SkillEntities : ExtendLibrary<SkillEntityAsset, SkillEntities>
{
    public static SkillEntityAsset Fireball          { get; private set; }
    public static SkillEntityAsset FireballCaster    { get; private set; }
    public static SkillEntityAsset FireballExplosion { get; private set; }


    public static SkillEntityAsset SingleWeapon { get; private set; }
    public static SkillEntityAsset FallWeapon   { get; }
    public static SkillEntityAsset RotateWeapon { get; }

    protected override void OnInit()
    {
        #region 火球术

        FireballCaster = Add(
            new SkillEntityAsset.Builder(nameof(FireballCaster))
                .SetAnimation(SpriteTextureLoader.getSpriteList("cultiway/effect/preparing_fireball"))
                .NewTrigger<TimeReachTrigger, float>(new()
                {
                    target_time = 1,
                    ActionContainer = SkillTriggerActions.TimeReachEntityRecycle.DefaultActionContainer
                })
                .NewTrigger<TimeIntervalTrigger, float>(new()
                {
                    interval = 0.5f,
                    ActionContainer = SkillTriggerActions.FireballCaster.DefaultActionContainer
                })
                .AppendComponent(new Scale(0.1f))
                .AppendComponent(new Rotation(0, 0, 0, 1))
                .Build()
        );
        Fireball = Add(
            new SkillEntityAsset.Builder(nameof(Fireball))
                .SetAnimation(SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball"))
                .NewTrigger<ObjCollisionTrigger, BaseSimObject>(new()
                {
                    collision_flag = ObjCollisionFlag.Actor | ObjCollisionFlag.Enemy,
                    radius = 2,
                    ActionContainer = SkillTriggerActions.FireballCollision.DefaultActionContainer
                })
                .SetTrajectory(Trajectories.TorwardTargetPos)
                .AppendComponent(new Velocity(20))
                .AppendComponent(new Scale(0.1f))
                .AppendComponent(new Rotation(0, 0, 0, 1))
                .Build());

        Array.Sort(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"),
            (a, b) => a.name.LeaveDigit().ToInt().CompareTo(b.name.LeaveDigit().ToInt()));
        FireballExplosion = Add(
            new SkillEntityAsset.Builder(nameof(FireballExplosion))
                .SetAnimation(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"), loop: false)
                .NewTrigger<AnimLoopEndTrigger, int>(new()
                {
                    target_loop_times = 1,
                    ActionContainer = SkillTriggerActions.AnimLoopEndEntityRecycle.DefaultActionContainer
                })
                .NewTrigger<TimeReachTrigger, float>(new()
                {
                    target_time = 0.1f * 4,
                    ActionContainer = SkillTriggerActions.ObjCollisionTriggerEnable.DefaultActionContainer
                })
                .NewTrigger<ObjCollisionTrigger, BaseSimObject>(new()
                {
                    Enabled = false,
                    collision_flag = ObjCollisionFlag.Actor | ObjCollisionFlag.Building | ObjCollisionFlag.Enemy,
                    radius = 3,
                    ActionContainer = SkillTriggerActions.FireballExplosion.DefaultActionContainer
                })
                .AppendComponent(new Scale(0.1f))
                .Build()
        );

        #endregion

        #region 御物三件套

        SingleWeapon = Add(
            new SkillEntityAsset.Builder(nameof(SingleWeapon))
                .SetAnimation(null)
                .NewTrigger<ObjCollisionTrigger, BaseSimObject>(new ObjCollisionTrigger
                {
                    collision_flag = ObjCollisionFlag.Actor | ObjCollisionFlag.Enemy,
                    radius = 1,
                    ActionContainer = SkillTriggerActions.FireballExplosion.DefaultActionContainer
                })
                .Build()
        );

        #endregion
    }

    [Hotfixable]
    public override void OnReload()
    {
        Array.Sort(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"),
            [Hotfixable](a, b) => a.name.LeaveDigit().ToInt().CompareTo(b.name.LeaveDigit().ToInt()));

        new SkillEntityAsset.Builder(FireballExplosion)
            .SetAnimation(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"), loop: false)
            .NewTrigger<AnimLoopEndTrigger, int>(new()
            {
                target_loop_times = 1,
                ActionContainer = SkillTriggerActions.AnimLoopEndEntityRecycle.DefaultActionContainer
            });
    }
}