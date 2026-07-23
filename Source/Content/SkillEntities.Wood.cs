using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>将木灵凝成长枪向前射出，可贯穿沿途目标的实体。</summary>
    public static SkillEntityAsset WoodSpear { get; private set; }

    /// <summary>从目标上方坠落，以沉重木体砸击地面的天降实体。</summary>
    public static SkillEntityAsset FallingTimber { get; private set; }

    /// <summary>沿地表生长蔓延，并持续扫过前方敌群的灵藤实体。</summary>
    public static SkillEntityAsset SpiritVine { get; private set; }

    /// <summary>在敌前生长成形，可阻挡来袭弹丸与敌方地面单位的木壁实体。</summary>
    public static SkillEntityAsset WoodWall { get; private set; }

    private static void ConfigureWood()
    {
        Configure(
            WoodSpear,
            ElementComposition.Static.Wood,
            Anim(WoodSpear, 0, 0.025f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Wood,
            SkillSemantics.Form.Spear,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(WoodSpear, 1), 0.1f);

        Configure(
                FallingTimber,
                ElementComposition.Static.Wood,
                Anim(FallingTimber, 0, 0.04f, 18f, SkillAnimationGameplayFlags.Movement),
                SkillTrajectories.FallingStrike,
                SkillImpactProfileLibrary.HeavySkyfall,
                SkillTrajectoryDomain.Ballistic | SkillTrajectoryDomain.Skyfall,
                SkillUseProfileLibrary.EnemyPoint,
                SkillEntityType.Attack,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Wood,
                SkillSemantics.Form.Pierce,
                SkillSemantics.Form.Falling,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AddAnimation(RuntimeAnimPath(FallingTimber, 1), 0.1f);

        Configure(
            SpiritVine,
            ElementComposition.Static.Wood,
            Anim(SpiritVine, 0, 0.04f, 18f),
            SkillTrajectories.GroundCrawl,
            SkillImpactProfileLibrary.GroundWave,
            SkillTrajectoryDomain.GroundTravel | SkillTrajectoryDomain.GroundManifest,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Wood,
            SkillSemantics.Form.Spike,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Motion.Ground,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);

        Configure(
            WoodWall,
            ElementComposition.Static.Wood,
            Anim(WoodWall, 0, 0.018f, 16f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Wood,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
