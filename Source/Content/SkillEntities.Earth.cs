using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>将普通石块作为弹丸沿弧线投向目标的飞石实体。</summary>
    public static SkillEntityAsset RockProjectile { get; private set; }

    /// <summary>直接在目标上方生成并垂直砸向地面的重型落石实体。</summary>
    public static SkillEntityAsset FallingRock { get; private set; }

    /// <summary>将石锥作为高速弹丸正面射出，可贯穿沿途目标的实体。</summary>
    public static SkillEntityAsset StoneCone { get; private set; }

    /// <summary>从目标脚下的地面原地突起，并在出现位置造成范围伤害的地刺实体。</summary>
    public static SkillEntityAsset GroundSpike { get; private set; }

    /// <summary>令连续岩脊贴着地表向前隆起并扫过敌群的实体。</summary>
    public static SkillEntityAsset EarthRidge { get; private set; }

    /// <summary>在敌前筑起并持续存在，可阻挡弹丸与敌方地面单位的土墙实体。</summary>
    public static SkillEntityAsset EarthWall { get; private set; }

    private static void ConfigureEarth()
    {
        Configure(
            RockProjectile,
            ElementComposition.Static.Earth,
            Anim(RockProjectile, 0, 0.035f, 18f),
            SkillTrajectories.ArcToPosition,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Earth,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
                FallingRock,
                ElementComposition.Static.Earth,
                Anim(FallingRock, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement,
                    dissipationFrameRate: 22f),
                SkillTrajectories.FallingStrike,
                SkillImpactProfileLibrary.HeavySkyfall,
                SkillTrajectoryDomain.Skyfall,
                SkillUseProfileLibrary.EnemyPoint,
                SkillEntityType.Attack,
                false,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Earth,
                SkillSemantics.Form.Falling,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Motion.Falling,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AddAnimation(RuntimeAnimPath(FallingRock, 1), 0.1f)
            .AddAnimation(RuntimeAnimPath(FallingRock, 2), 0.025f, 1.2f);

        Configure(
            StoneCone,
            ElementComposition.Static.Earth,
            Anim(StoneCone, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Earth,
            SkillSemantics.Form.Spear,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            GroundSpike,
            ElementComposition.Static.Earth,
            Anim(GroundSpike, 0, 0.04f, 14f, SkillAnimationGameplayFlags.Movement,
                dissipationFrameRate: 20f),
            SkillTrajectories.GroundEruptAtTarget,
            SkillImpactProfileLibrary.GroundManifest,
            SkillTrajectoryDomain.GroundManifest,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Earth,
            SkillSemantics.Form.Spike,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Motion.GroundManifest,
            SkillSemantics.Motion.Ground,
            SkillSemantics.Delivery.Instant,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(GroundSpike, 1), 0.1f)
            .AddAnimation(RuntimeAnimPath(GroundSpike, 2), 0.025f, 1.2f)
            .AddAnimation(RuntimeAnimPath(GroundSpike, 3), 0.025f, 1.2f);

        Configure(
            EarthRidge,
            ElementComposition.Static.Earth,
            Anim(EarthRidge, 0, 0.045f, 18f),
            SkillTrajectories.GroundCrawl,
            SkillImpactProfileLibrary.GroundWave,
            SkillTrajectoryDomain.GroundTravel,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Earth,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Motion.Ground,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);

        Configure(
            EarthWall,
            ElementComposition.Static.Earth,
            Anim(EarthWall, 0, 0.018f, 16f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Earth,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
