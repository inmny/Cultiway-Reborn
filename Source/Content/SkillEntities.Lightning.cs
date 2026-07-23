using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>以球形雷霆本体飞行，并在命中或落点爆发范围伤害的实体。</summary>
    public static SkillEntityAsset ThunderOrb { get; private set; }

    /// <summary>从目标上空垂直降下，并在落点造成重型范围打击的雷霆实体。</summary>
    public static SkillEntityAsset LightningStrike { get; private set; }

    /// <summary>将雷光凝成长枪高速射出，可贯穿沿途目标的实体。</summary>
    public static SkillEntityAsset LightningSpear { get; private set; }

    /// <summary>首次命中后在多个邻近敌人之间依次跃迁的链式雷霆实体。</summary>
    public static SkillEntityAsset ChainLightning { get; private set; }

    /// <summary>环绕施术者并随其移动，用于拦截来袭弹丸的雷盾实体。</summary>
    public static SkillEntityAsset LightningShield { get; private set; }

    private static void ConfigureLightning()
    {
        var lightning = new ElementComposition(pos: 0.5f, entropy: 0.5f, normalize: true);

        Configure(
            ThunderOrb,
            lightning,
            Anim(ThunderOrb, 0, 0.035f, 20f),
            SkillTrajectories.ArcToPosition,
            SkillImpactProfileLibrary.Explosion,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Lightning,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Effect.Blast,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(ThunderOrb, 1), 0.1f)
            .AddAnimation(RuntimeAnimPath(ThunderOrb, 2), 0.025f)
            .AddAnimation(RuntimeAnimPath(ThunderOrb, 3), 0.025f, 1.2f);

        Configure(
            LightningStrike,
            lightning,
            Anim(LightningStrike, 0, 0.045f, 24f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.FallingStrike,
            SkillImpactProfileLibrary.HeavySkyfall,
            SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Lightning,
            SkillSemantics.Form.Falling,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Effect.Blast,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Motion.Falling,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 1), 0.1f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 2), 0.025f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 3), 0.025f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 4), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 5), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 6), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 7), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 8), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 9), 0.025f)
            .AddAnimation(RuntimeAnimPath(LightningStrike, 10), 0.025f);

        Configure(
            LightningSpear,
            lightning,
            Anim(LightningSpear, 0, 0.03f, 24f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Lightning,
            SkillSemantics.Form.Spear,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            ChainLightning,
            lightning,
            Anim(ChainLightning, 0, 0.035f, 24f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ChainTargets,
            SkillImpactProfileLibrary.Chain,
            SkillTrajectoryDomain.Chain,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Lightning,
            SkillSemantics.Form.Beam,
            SkillSemantics.Form.Single,
            SkillSemantics.Effect.Combo,
            SkillSemantics.Delivery.Instant,
            SkillSemantics.Motion.Chain,
            SkillSemantics.Role.Offensive);

        Configure(
            LightningShield,
            lightning,
            Anim(LightningShield, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ShieldOnCaster,
            SkillImpactProfileLibrary.Shield,
            SkillTrajectoryDomain.Aura,
            SkillUseProfileLibrary.CasterSelf,
            SkillEntityType.Defense,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Lightning,
            SkillSemantics.Form.Shield,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive)
            .AddAnimation(RuntimeAnimPath(LightningShield, 1), 0.025f,
                SkillEntityAnimationSettings.Inherit.WithFrameRate(18f));
    }
}
