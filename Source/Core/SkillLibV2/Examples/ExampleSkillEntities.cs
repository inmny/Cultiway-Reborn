using System;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Components.Triggers;
using Cultiway.Utils.Extension;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Examples;

public static class ExampleSkillEntities
{
    public static SkillEntityMeta FireballCaster { get; private set; }
    public static SkillEntityMeta Fireball       { get; private set; }
    public static SkillEntityMeta Explosion      { get; }

    public static void Init()
    {
        FireballCaster = new SkillEntityMeta.MetaBuilder()
            .AddComponent(new Position())
            .AddComponent(new Rotation())
            .AddComponent(new Scale(0.1f))
            .AddComponent(new SkillCaster())
            .AddComponent(new SkillTargetObj())
            .AddComponent(new AnimData
            {
                frames = SpriteTextureLoader.getSpriteList("cultiway/effect/preparing_fireball")
            })
            .AddComponent(new AnimController
            {
                meta = new AnimControllerMeta
                {
                    frame_interval = 0.1f,
                    loop = true
                }
            })
            .AddComponent(new AnimBindRenderer())
            .NewTrigger(new TimeIntervalTrigger
            {
                interval_time = 0.5f,
                TriggerActionMeta = ExampleTriggerActions.TimeIntervalSpawnFireball
            }, new TimeIntervalContext(), out var time_interval_trigger_id)
            .NewTrigger(new CastCountReachTrigger
            {
                TargetValue = 1,
                ExpectedResult = CompareResult.GreaterThanTarget,
                TriggerActionMeta = ExampleTriggerActions.CastCountReachRecycleFireballCaster
            }, new CastCountReachContext(), out var cast_count_reach_trigger_id)
            .Build();
        Fireball = new SkillEntityMeta.MetaBuilder()
            .AddComponent(new Position())
            .AddComponent(new Rotation())
            .AddComponent(new Scale(0.1f))
            .AddComponent(new Velocity(20))
            .AddComponent(new AnimData
            {
                frames = SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball")
            })
            .AddComponent(new AnimController
            {
                meta = new AnimControllerMeta
                {
                    frame_interval = 0.1f,
                    loop = true
                }
            })
            .AddComponent(new AnimBindRenderer())
            .AddComponent(new Trajectory
            {
                meta = ExampleTrajectories.OnlyTowards
            })
            .NewTrigger(new ObjCollisionTrigger
            {
                actor = true, enemy = true,
                TriggerActionMeta = ExampleTriggerActions.ObjCollisionDamageAndExplosion
            }, new ObjCollisionContext(), out var collision_trigger_id)
            .AddTriggerComponent(collision_trigger_id, new Collider
            {
                type = ColliderType.Sphere
            })
            .AddTriggerComponent(collision_trigger_id, new ColliderSphere
            {
                radius = 2
            })
            .Build();
        Array.Sort(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"),
            (a, b) => a.name.LeaveDigit().ToInt().CompareTo(b.name.LeaveDigit().ToInt()));
    }
}