using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>携带毒性的细针飞行实体，可贯穿并依次命中沿途目标。</summary>
    public static SkillEntityAsset PoisonNeedle { get; private set; }

    /// <summary>缓慢向前推进并持续覆盖、侵蚀区域内敌人的毒雾实体。</summary>
    public static SkillEntityAsset PoisonMist { get; private set; }

    /// <summary>固定存在于指定地面区域，并持续伤害进入者的毒潭实体。</summary>
    public static SkillEntityAsset PoisonPool { get; private set; }

    private static void ConfigurePoison()
    {
        Configure(
            PoisonNeedle,
            ElementComposition.Static.Poison,
            Anim(PoisonNeedle, 0, 0.022f, 24f),
            SkillTrajectories.TowardsDirection,
            SkillImpactProfileLibrary.Piercing,
            SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.Skyfall,
            SkillUseProfileLibrary.EnemyObjectOrPoint,
            SkillEntityType.Attack,
            false,
            null,
            SkillSemantics.Element.Poison,
            SkillSemantics.Form.Needle,
            SkillSemantics.Form.Pierce,
            SkillSemantics.Form.Single,
            SkillSemantics.Delivery.Projectile,
            SkillSemantics.Role.Offensive);

        Configure(
            PoisonMist,
            ElementComposition.Static.Poison,
            Anim(PoisonMist, 0, 0.045f, 16f),
            SkillTrajectories.FieldAdvance,
            SkillImpactProfileLibrary.Field,
            SkillTrajectoryDomain.StationaryField | SkillTrajectoryDomain.MobileField,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Poison,
            SkillSemantics.Form.Mist,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);

        Configure(
            PoisonPool,
            ElementComposition.Static.Poison,
            Anim(PoisonPool, 0, 0.045f, 14f, SkillAnimationGameplayFlags.Movement),
            SkillTrajectories.FieldAtTarget,
            SkillImpactProfileLibrary.Field,
            SkillTrajectoryDomain.StationaryField,
            SkillUseProfileLibrary.EnemyPoint,
            SkillEntityType.Attack,
            true,
            VisualRotation.FixedUpright(),
            SkillSemantics.Element.Poison,
            SkillSemantics.Form.Pool,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Delivery.Field,
            SkillSemantics.Role.Offensive);
    }
}
