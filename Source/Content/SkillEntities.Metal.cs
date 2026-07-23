using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>御使剑形法器飞行追敌，并在接触目标时完成斩击的实体。</summary>
    public static SkillEntityAsset FlyingSword { get; private set; }

    /// <summary>横向展开并向前飞行，可连续扫过敌群的剑气实体。</summary>
    public static SkillEntityAsset SwordQi { get; private set; }

    /// <summary>以细小高速形态飞行，可贯穿沿途目标的金针实体。</summary>
    public static SkillEntityAsset GoldenNeedle { get; private set; }

    /// <summary>在战场上定向展开，用于拦截来袭弹丸并伤害接触者的剑幕实体。</summary>
    public static SkillEntityAsset SwordCurtain { get; private set; }

    private static void ConfigureMetal()
    {
        Configure(
                FlyingSword,
                ElementComposition.Static.Iron,
                Anim(FlyingSword, 0, 0.025f, 18f),
                SkillTrajectories.TowardsTarget,
                SkillImpactProfileLibrary.NormalProjectile,
                SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
                SkillUseProfileLibrary.EnemyObjectOrPoint,
                SkillEntityType.Attack,
                true,
                null,
                SkillSemantics.Element.Iron,
                SkillSemantics.Theme.Metal,
                SkillSemantics.Form.Sword,
                SkillSemantics.Form.Pierce,
                SkillSemantics.Form.Single,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Role.Offensive)
            .SetModifierWeightMultiplier(SkillModifiers.Haste, 1.5f)
            .AddAnimation(RuntimeAnimPath(FlyingSword, 1), 0.1f);

        Configure(
            SwordQi,
            ElementComposition.Static.Iron,
            Anim(SwordQi, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Wave,
            SkillTrajectoryDomain.FlyingWave,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Iron,
            SkillSemantics.Theme.Metal,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(SwordQi, 1), 0.1f);

        Configure(
            GoldenNeedle,
            ElementComposition.Static.Iron,
            Anim(GoldenNeedle, 0, 0.022f, 24f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Iron,
            SkillSemantics.Theme.Metal,
            SkillSemantics.Form.Needle,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            SwordCurtain.TuneImpact(contactDamage: true),
            ElementComposition.Static.Iron,
            Anim(SwordCurtain, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Iron,
            SkillSemantics.Theme.Metal,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);
    }
}
