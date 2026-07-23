using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>以球形火焰飞行，并在命中时爆炸波及周围目标的实体。</summary>
    public static SkillEntityAsset Fireball { get; private set; }

    /// <summary>横向展开并向前飞行，可持续切过敌群的火焰刃波实体。</summary>
    public static SkillEntityAsset FireBlade { get; private set; }

    /// <summary>从目标上空高速坠落，并在落点产生重型爆炸的炎陨实体。</summary>
    public static SkillEntityAsset FlameMeteor { get; private set; }

    /// <summary>在敌前持续燃烧，可拦截弹丸并灼伤接触者的火墙实体。</summary>
    public static SkillEntityAsset FireWall { get; private set; }

    /// <summary>环绕施术者并随其移动，用于拦截来袭弹丸的炎盾实体。</summary>
    public static SkillEntityAsset FlameShield { get; private set; }

    private static void ConfigureFire()
    {
        Configure(
            Fireball,
            ElementComposition.Static.Fire,
            Anim(Fireball, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Explosion,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Fire,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Effect.Blast,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(Anim(Fireball, 1, 0.03f, 20f))
            .AddAnimation(Anim(Fireball, 2, 0.03f, 20f))
            .AddAnimation(Anim(Fireball, 3, 0.03f, 20f))
            .AddAnimation(Anim(Fireball, 4, 0.03f, 20f))
            .AddAnimation(RuntimeAnimPath(Fireball, 5), 0.1f)
            .AddAnimation(RuntimeAnimPath(Fireball, 6), 0.025f)
            .AddAnimation(RuntimeAnimPath(Fireball, 7), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 8), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 9), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 10), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 11), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 12), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 13), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 14), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 15), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(Fireball, 16), 0.025f)
            .AddAnimation(RuntimeAnimPath(Fireball, 17), 0.025f, 1.2f);

        Configure(
            FireBlade,
            ElementComposition.Static.Fire,
            Anim(FireBlade, 0, 0.035f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Wave,
            SkillTrajectoryDomain.FlyingWave,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Fire,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(Anim(FireBlade, 1, 0.035f, 20f))
            .AddAnimation(RuntimeAnimPath(FireBlade, 2), 0.1f)
            .AddAnimation(RuntimeAnimPath(FireBlade, 3), 0.025f);

        Configure(
                FlameMeteor,
                ElementComposition.Static.Fire,
                Anim(FlameMeteor, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
                SkillTrajectories.FallingStrike,
                SkillImpactProfileLibrary.HeavySkyfall,
                SkillTrajectoryDomain.Skyfall,
                SkillUseProfileLibrary.EnemyPoint,
                SkillEntityType.Attack,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Fire,
                SkillSemantics.Form.Falling,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Effect.Blast,
                SkillSemantics.Motion.Falling,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f);

        Configure(
            FireWall.TuneImpact(contactDamage: true),
            ElementComposition.Static.Fire,
            Anim(FireWall, 0, 0.018f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Fire,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Effect.Burn,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);

        Configure(
            FlameShield,
            ElementComposition.Static.Fire,
            Anim(FlameShield, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ShieldOnCaster,
            SkillImpactProfileLibrary.Shield,
            SkillTrajectoryDomain.Aura,
            SkillUseProfileLibrary.CasterSelf,
            SkillEntityType.Defense,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Fire,
            SkillSemantics.Form.Shield,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive)
            .AddAnimation(RuntimeAnimPath(FlameShield, 1), 0.045f,
                SkillEntityAnimationSettings.Inherit.WithFrameRate(18f));
    }
}
