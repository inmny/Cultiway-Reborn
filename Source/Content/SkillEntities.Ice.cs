using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>凝成短促锐利的冰晶本体，直接飞向并命中单个目标的实体。</summary>
    public static SkillEntityAsset IceShard { get; private set; }

    /// <summary>凝成长枪形态高速飞行，可贯穿沿途目标的冰系实体。</summary>
    public static SkillEntityAsset IceLance { get; private set; }

    /// <summary>在目标区域上空生成并成群坠落的冰棱天降实体。</summary>
    public static SkillEntityAsset Icefall { get; private set; }

    /// <summary>在敌前凝结成形，可阻挡来袭弹丸与敌方地面单位的冰墙实体。</summary>
    public static SkillEntityAsset IceWall { get; private set; }

    private static void ConfigureIce()
    {
        var ice = new ElementComposition(water: 1f);
        Configure(
            IceShard,
            ice,
            Anim(IceShard, 0, 0.025f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Ice,
            SkillSemantics.Form.Spike,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            IceLance,
            ice,
            Anim(IceLance, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Ice,
            SkillSemantics.Form.Spear,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            Icefall,
            ice,
            Anim(Icefall, 0, 0.04f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.RainFall,
            SkillImpactProfileLibrary.HeavySkyfall,
            SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Ice,
            SkillSemantics.Form.Falling,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Motion.Rain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            IceWall,
            ice,
            Anim(IceWall, 0, 0.018f, 16f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Ice,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
