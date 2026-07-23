using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>将水流凝成箭形高速射出，可贯穿沿途目标的实体。</summary>
    public static SkillEntityAsset WaterArrow { get; private set; }

    /// <summary>以凝聚水灵的球形本体飞向目标，并在接触时命中的实体。</summary>
    public static SkillEntityAsset WaterOrb { get; private set; }

    /// <summary>横向展开并向前切过敌群的飞行水刃实体。</summary>
    public static SkillEntityAsset WaterBlade { get; private set; }

    /// <summary>贴近地表持续向前推进，以宽阔浪潮扫过区域的实体。</summary>
    public static SkillEntityAsset TidalWave { get; private set; }

    /// <summary>在敌前升起并保持形态，可截断弹丸与地面单位通路的水幕实体。</summary>
    public static SkillEntityAsset WaterCurtain { get; private set; }

    private static void ConfigureWater()
    {
        Configure(
            WaterArrow,
            ElementComposition.Static.Water,
            Anim(WaterArrow, 0, 0.025f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Water,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(Anim(WaterArrow, 1, 0.025f, 20f))
            .AddAnimation(RuntimeAnimPath(WaterArrow, 2), 0.1f)
            .AddAnimation(RuntimeAnimPath(WaterArrow, 3), 0.025f, 1.2f);

        Configure(
            WaterOrb,
            ElementComposition.Static.Water,
            Anim(WaterOrb, 0, 0.03f, 18f),
            SkillTrajectories.ArcToPosition,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Ballistic | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Water,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(Anim(WaterOrb, 1, 0.03f, 18f))
            .AddAnimation(RuntimeAnimPath(WaterOrb, 2), 0.1f)
            .AddAnimation(RuntimeAnimPath(WaterOrb, 3), 0.025f)
            .AddAnimation(RuntimeAnimPath(WaterOrb, 4), 0.025f, 1.2f);

        Configure(
            WaterBlade,
            ElementComposition.Static.Water,
            Anim(WaterBlade, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Wave,
            SkillTrajectoryDomain.FlyingWave,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Water,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(Anim(WaterBlade, 1, 0.03f, 20f))
            .AddAnimation(RuntimeAnimPath(WaterBlade, 2), 0.1f)
            .AddAnimation(RuntimeAnimPath(WaterBlade, 3), 0.025f);

        Configure(
            TidalWave,
            ElementComposition.Static.Water,
            Anim(TidalWave, 0, 0.045f, 16f),
            SkillTrajectories.GroundCrawl,
            SkillImpactProfileLibrary.GroundWave,
            SkillTrajectoryDomain.GroundTravel,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Water,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Motion.Ground,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);

        Configure(
            WaterCurtain,
            ElementComposition.Static.Water,
            Anim(WaterCurtain, 0, 0.018f, 16f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Water,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
