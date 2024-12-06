using System;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.SkillLibV2.Examples;

public static class ExampleSkillEntities
{
    public static SkillEntityMeta FireballCaster { get; private set; }
    public static SkillEntityMeta Fireball       { get; private set; }
    public static SkillEntityMeta Explosion      { get; }

    public static void Init()
    {
        FireballCaster = new SkillEntityMeta.MetaBuilder()
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/preparing_fireball"), 0.1f)
            .AddComponent(new SkillTargetObj())
            .NewTrigger<TimeIntervalTrigger, TimeIntervalContext>(new TimeIntervalTrigger
            {
                interval_time = 0.5f,
                TriggerActionMeta = ExampleTriggerActions.TimeIntervalSpawnFireball
            }, out _)
            .NewTrigger<CastCountReachTrigger, CastCountReachContext>(new CastCountReachTrigger
            {
                TargetValue = 1,
                ExpectedResult = CompareResult.GreaterThanTarget,
                TriggerActionMeta = TriggerActions.GetRecycleActionMeta<CastCountReachTrigger, CastCountReachContext>()
            }, out _)
            .Build();
        Fireball = new SkillEntityMeta.MetaBuilder()
            .AddAnim(SpriteTextureLoader.getSpriteList("cultiway/effect/flying_fireball"), 0.1f)
            .SetTrajectory(Trajectories.GoForward, 20)
            .AddSphereObjCollisionTrigger(new ObjCollisionTrigger
            {
                actor = true, enemy = true,
                TriggerActionMeta = ExampleTriggerActions.ObjCollisionDamageAndExplosion
            }, 2)
            .Build();
        Array.Sort(SpriteTextureLoader.getSpriteList("cultiway/effect/explosion_fireball"),
            (a, b) => a.name.LeaveDigit().ToInt().CompareTo(b.name.LeaveDigit().ToInt()));
    }
}