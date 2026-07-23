using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>不偏向特定元素、以球形灵力飞向并命中目标的通用弹丸实体。</summary>
    public static SkillEntityAsset SpiritBolt { get; private set; }

    /// <summary>不偏向特定元素、瞬间连接起点与目标并沿线命中的通用光束实体。</summary>
    public static SkillEntityAsset SpiritBeam { get; private set; }

    /// <summary>不偏向特定元素、环绕施术者并拦截来袭弹丸的通用护盾实体。</summary>
    public static SkillEntityAsset SpiritShield { get; private set; }

    private static void ConfigureGeneric()
    {
        var generic = new ElementComposition(normalize: true);

        Configure(
            SpiritBolt,
            generic,
            Anim(SpiritBolt, 0, 0.03f, 18f),
            SkillTrajectories.TowardsTarget,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Generic,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            SpiritBeam,
            generic,
            Anim(SpiritBeam, 0, 0.04f, 24f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.InstantBeam,
            SkillImpactProfileLibrary.PulseBeam,
            SkillTrajectoryDomain.Beam,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Generic,
            SkillSemantics.Form.Beam,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Instant,
            SkillSemantics.Role.Offensive);

        Configure(
            SpiritShield,
            generic,
            Anim(SpiritShield, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ShieldOnCaster,
            SkillImpactProfileLibrary.Shield,
            SkillTrajectoryDomain.Aura,
            SkillUseProfileLibrary.CasterSelf,
            SkillEntityType.Defense,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Generic,
            SkillSemantics.Form.Shield,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
