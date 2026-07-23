using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>横向展开并高速向前切过敌群的风系刃波实体。</summary>
    public static SkillEntityAsset WindBlade { get; private set; }

    /// <summary>将压缩气流凝成球形并直射单个目标的风弹实体。</summary>
    public static SkillEntityAsset WindBullet { get; private set; }

    /// <summary>向前移动并持续卷袭覆盖区域内敌人的龙卷场域实体。</summary>
    public static SkillEntityAsset Tornado { get; private set; }

    /// <summary>在敌前形成，可拦截弹丸并推开接触者的定向风墙实体。</summary>
    public static SkillEntityAsset WindWall { get; private set; }

    /// <summary>环绕施术者并随其移动，用于拦截来袭弹丸的风盾实体。</summary>
    public static SkillEntityAsset WindShield { get; private set; }

    private static void ConfigureWind()
    {
        var wind = new ElementComposition(water: 0.5f, wood: 0.5f);
        Configure(
            WindBlade,
            wind,
            Anim(WindBlade, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Wave,
            SkillTrajectoryDomain.FlyingWave,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Wind,
            SkillSemantics.Form.Wave,
            SkillSemantics.Form.Slash,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(WindBlade, 1), 0.1f);

        Configure(
            WindBullet,
            wind,
            Anim(WindBullet, 0, 0.03f, 20f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.NormalProjectile,
            SkillTrajectoryDomain.FlyingBody,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            true,
            null,
            SkillSemantics.Element.Wind,
            SkillSemantics.Form.Ball,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive)
            .AddAnimation(RuntimeAnimPath(WindBullet, 1), 0.1f);

        Configure(
                Tornado,
                wind,
                Anim(Tornado, 0, 0.05f, 16f),
                SkillTrajectories.FieldAdvance,
                SkillImpactProfileLibrary.Field,
                SkillTrajectoryDomain.StationaryField | SkillTrajectoryDomain.MobileField,
                SkillUseProfileLibrary.EnemyPoint,
                SkillEntityType.Attack,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Wind,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Motion.Vortex,
                SkillSemantics.Delivery.Field,
                SkillSemantics.Role.Offensive)
            .SetModifierWeightMultiplier(SkillModifiers.Huge, 4f)
            .AddAnimation(Anim(Tornado, 1, 0.055f, 16f))
            .AddAnimation(RuntimeAnimPath(Tornado, 2), 0.1f)
            .AddAnimation(RuntimeAnimPath(Tornado, 3), 0.025f);

        Configure(
            WindWall.TuneImpact(contactForce: 2f),
            wind,
            Anim(WindWall, 0, 0.018f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.WallBetweenCasterAndTarget,
            SkillImpactProfileLibrary.Wall,
            SkillTrajectoryDomain.Barrier,
            SkillUseProfileLibrary.BetweenCasterAndEnemy,
            SkillEntityType.Defense,
            true,
            null,
            SkillSemantics.Element.Wind,
            SkillSemantics.Form.Wall,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Effect.Displace,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive);

        Configure(
            WindShield,
            wind,
            Anim(WindShield, 0, 0.045f, 18f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.ShieldOnCaster,
            SkillImpactProfileLibrary.Shield,
            SkillTrajectoryDomain.Aura,
            SkillUseProfileLibrary.CasterSelf,
            SkillEntityType.Defense,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Wind,
            SkillSemantics.Form.Shield,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Defensive)
            .AddAnimation(RuntimeAnimPath(WindShield, 1), 0.045f,
                SkillEntityAnimationSettings.Inherit.WithFrameRate(18f));
    }
}
