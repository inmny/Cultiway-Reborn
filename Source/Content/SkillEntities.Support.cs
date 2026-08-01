using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Semantics;
using Cultiway.Content.Components.Skill;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>飞向单个友军并立即恢复生命的辅助法术。</summary>
    public static SkillEntityAsset HealingLight { get; private set; }

    /// <summary>在目标区域维持八秒、周期恢复友军生命的辅助法术。</summary>
    public static SkillEntityAsset RejuvenationField { get; private set; }

    /// <summary>在目标区域按优先级移除友军负面状态的辅助法术。</summary>
    public static SkillEntityAsset PurificationWave { get; private set; }

    /// <summary>为单个友军提高伤害倍率的辅助法术。</summary>
    public static SkillEntityAsset BattleBlessing { get; private set; }

    /// <summary>为单个友军提高固定护甲的辅助法术。</summary>
    public static SkillEntityAsset GuardBlessing { get; private set; }

    /// <summary>为单个友军提高移动速度和攻击速度的辅助法术。</summary>
    public static SkillEntityAsset HasteBlessing { get; private set; }

    /// <summary>注册首批六个辅助法术的实体形态、成本和必选效果。</summary>
    private static void ConfigureSupport()
    {
        var generic = new ElementComposition();

        Configure(
                HealingLight,
                generic,
                Anim(HealingLight, 0, 0.035f, 20f),
                SkillTrajectories.TowardsTarget,
                SkillImpactProfileLibrary.NormalProjectile,
                SkillTrajectoryDomain.FlyingBody,
                SkillUseProfileLibrary.FriendlyObject,
                SkillEntityType.Support,
                true,
                null,
                SkillSemantics.Element.Generic,
                SkillSemantics.Form.Single,
                SkillSemantics.Delivery.Projectile,
                SkillSemantics.Effect.Heal,
                SkillSemantics.Role.Support)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(2f)
            .SetDealsBaseDamage(false)
            .RequireModifier(SkillModifiers.Healing)
            .SetIcon(GetIconResourcePath(HealingLight))
            .SetWorldVisual(SkillWorldVisualProfiles.Healing);

        Configure(
                RejuvenationField,
                generic,
                Anim(RejuvenationField, 0, 0.055f, 18f),
                SkillTrajectories.FieldAtTarget,
                SkillImpactProfileLibrary.Field,
                SkillTrajectoryDomain.StationaryField,
                SkillUseProfileLibrary.FriendlyArea,
                SkillEntityType.Support,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Generic,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Delivery.Field,
                SkillSemantics.Effect.Regeneration,
                SkillSemantics.Role.Support)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(3f)
            .SetAreaCostBaseRadius(2.5f)
            .SetDealsBaseDamage(false)
            .TuneImpact(lifetimeMultiplier: 8f / 2.4f)
            .SetRuntimeLifetime(container => container.GetComponent<RejuvenationModifier>().Duration)
            .RequireModifier(SkillModifiers.Rejuvenation)
            .SetIcon(GetIconResourcePath(RejuvenationField))
            .SetWorldVisual(SkillWorldVisualProfiles.RejuvenationField);

        Configure(
                PurificationWave,
                generic,
                Anim(PurificationWave, 0, 0.06f, 22f),
                SkillTrajectories.AppearAtTarget,
                SkillImpactProfileLibrary.GroundManifest,
                SkillTrajectoryDomain.TargetManifest,
                SkillUseProfileLibrary.FriendlyArea,
                SkillEntityType.Support,
                false,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Generic,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Delivery.Instant,
                SkillSemantics.Effect.Cleanse,
                SkillSemantics.Role.Support)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(3f)
            .SetAreaCostBaseRadius(2.5f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 2.5f / 1.4f)
            .RequireModifier(SkillModifiers.Purification)
            .SetIcon(GetIconResourcePath(PurificationWave))
            .SetWorldVisual(SkillWorldVisualProfiles.PurificationWave);

        ConfigureSingleBlessing(
            BattleBlessing,
            SkillModifiers.BattleBlessing,
            SkillSemantics.Effect.AttackBoost,
            SkillWorldVisualProfiles.BattleBlessing,
            0);
        ConfigureSingleBlessing(
            GuardBlessing,
            SkillModifiers.GuardBlessing,
            SkillSemantics.Effect.DefenseBoost,
            SkillWorldVisualProfiles.GuardBlessing,
            0);
        ConfigureSingleBlessing(
            HasteBlessing,
            SkillModifiers.HasteBlessing,
            SkillSemantics.Effect.Speed,
            SkillWorldVisualProfiles.HasteBlessing,
            0);
    }

    /// <summary>以相同飞行形态注册一种单体友方祝福。</summary>
    private static void ConfigureSingleBlessing(
        SkillEntityAsset asset,
        SkillModifierAsset modifier,
        SemanticAsset effectSemantic,
        Core.SkillLibV3.Visuals.SkillWorldVisualProfile visualProfile,
        int variantIndex)
    {
        Configure(
                asset,
                new ElementComposition(),
                Anim(asset, variantIndex, 0.04f, 20f),
                SkillTrajectories.TowardsTarget,
                SkillImpactProfileLibrary.NormalProjectile,
                SkillTrajectoryDomain.FlyingBody,
                SkillUseProfileLibrary.FriendlyObject,
                SkillEntityType.Support,
                true,
                null,
                SkillSemantics.Element.Generic,
                SkillSemantics.Form.Single,
                SkillSemantics.Delivery.Projectile,
                effectSemantic,
                SkillSemantics.Role.Support)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(2f)
            .SetDealsBaseDamage(false)
            .RequireModifier(modifier)
            .SetIcon(GetIconResourcePath(asset))
            .SetWorldVisual(visualProfile);
    }
}
