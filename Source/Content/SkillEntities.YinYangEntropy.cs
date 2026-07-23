using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>以凝聚阴煞之力的球形本体飞向并命中目标的实体。</summary>
    public static SkillEntityAsset YinBolt { get; private set; }

    /// <summary>横向展开并向前飞行，可贯穿敌群的阴寒刃波实体。</summary>
    public static SkillEntityAsset YinBlade { get; private set; }

    /// <summary>固定或移动覆盖一片区域，并持续侵蚀其中敌人的玄阴场域实体。</summary>
    public static SkillEntityAsset YinField { get; private set; }

    /// <summary>以凝聚纯阳之力的球形本体飞向并命中目标的实体。</summary>
    public static SkillEntityAsset YangBolt { get; private set; }

    /// <summary>连接施术者与目标并持续追踪、多次造成伤害的曜阳光束实体。</summary>
    public static SkillEntityAsset SolarBeam { get; private set; }

    /// <summary>环绕施术者并随其移动，用于拦截来袭弹丸的阳罡护盾实体。</summary>
    public static SkillEntityAsset YangShield { get; private set; }

    /// <summary>以球形混沌力量飞行，并在命中或落点爆发范围伤害的实体。</summary>
    public static SkillEntityAsset ChaosOrb { get; private set; }

    /// <summary>直接在目标位置撕开，并于原地爆发范围伤害的短暂裂隙实体。</summary>
    public static SkillEntityAsset Rift { get; private set; }

    /// <summary>追随目标移动并持续对覆盖区域施加熵变伤害的场域实体。</summary>
    public static SkillEntityAsset EntropyField { get; private set; }

    private static void ConfigureYinYangEntropy()
    {
        var yin = new ElementComposition(neg: 1f);
        var yang = new ElementComposition(pos: 1f);
        var entropy = new ElementComposition(entropy: 1f);

        Configure(
            YinBolt,
            yin,
            Anim(YinBolt, 0, 0.03f, 18f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Neg,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            YinBlade,
            yin,
            Anim(YinBlade, 0, 0.035f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Wave,
            SkillTrajectoryDomain.FlyingWave,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Neg,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            YinField,
            yin,
            Anim(YinField, 0, 0.05f, 16f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.FieldAtTarget,
            SkillImpactProfileLibrary.Field,
            SkillTrajectoryDomain.StationaryField | SkillTrajectoryDomain.MobileField,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Neg,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);

        Configure(
            YangBolt,
            yang,
            Anim(YangBolt, 0, 0.03f, 18f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Pos,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            SolarBeam,
            yang,
            Anim(SolarBeam, 0, 0.025f, 20f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.TrackingBeam,
            SkillImpactProfileLibrary.ChannelBeam,
            SkillTrajectoryDomain.Beam,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Pos,
            SkillSemantics.Form.Beam,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Instant,
            SkillSemantics.Role.Offensive);

        Configure(
            YangShield,
            yang,
            Anim(YangShield, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ShieldOnCaster,
            SkillImpactProfileLibrary.Shield,
            SkillTrajectoryDomain.Aura,
            SkillUseProfileLibrary.CasterSelf,
            SkillEntityType.Defense,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Pos,
            SkillSemantics.Form.Shield,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);

        Configure(
            ChaosOrb,
            entropy,
            Anim(ChaosOrb, 0, 0.035f, 18f),
            SkillTrajectories.ArcToPosition,
            SkillImpactProfileLibrary.Explosion,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Entropy,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Effect.Random,
            SkillSemantics.Effect.Blast,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            Rift,
            entropy,
            Anim(Rift, 0, 0.05f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.AppearAtTarget,
            SkillImpactProfileLibrary.Explosion,
            SkillTrajectoryDomain.TargetManifest,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Entropy,
            SkillSemantics.Form.Rift,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Effect.Random,
            SkillSemantics.Delivery.Instant,
            SkillSemantics.Role.Offensive);

        Configure(
            EntropyField,
            entropy,
            Anim(EntropyField, 0, 0.05f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.FieldFollowTarget,
            SkillImpactProfileLibrary.Field,
            SkillTrajectoryDomain.StationaryField | SkillTrajectoryDomain.MobileField,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Entropy,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Effect.Random,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);
    }
}
