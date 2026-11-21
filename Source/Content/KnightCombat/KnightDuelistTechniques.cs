using Cultiway.Content.Libraries;
using Cultiway.Content.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>决斗家流派三个战技的条件、权重和执行。</summary>
internal static class KnightDuelistTechniques
{
    public static KnightTechniqueActiveUseProfile CreateCommittedStrikeProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Object,
            TargetRelation = SkillUseTargetRelation.Hostile,
            CastMobility = ActiveAbilityCastMobility.StationaryDuringRecovery,
            UseCondition = IsMeleeTarget,
            ResolveAiWeight = context => KnightTechniqueAiRules.IsIsolated(context.Target) ? 6 : 3,
            ResolveAiDamageMultiplier = context =>
                KnightTechniqueAiRules.IsIsolated(context.Target) ? 1.55f : 1.25f,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                2.2f, 0f, 0f, 0f, 2.2f, 5f, 1f, SkillImpactKind.Wave),
            ResolveRange = context => KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base),
            ResolveEffectRadius = _ => 0f,
            Construction = BodyActionCatalog.KnightCommittedStrike,
        };
    }

    public static KnightTechniqueActiveUseProfile CreateCounterStanceProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Self,
            TargetRelation = SkillUseTargetRelation.Self,
            CastMobility = ActiveAbilityCastMobility.Mobile,
            ResolveAiWeight = context =>
            {
                if (KnightTechniqueStatuses.Has(context.Caster.Base, KnightTechniqueStatuses.CounterStance))
                    return 0;
                int recent = context.Caster.GetRecentAttackersSnapshot().Count;
                if (recent == 0 || context.Caster.Base.getHealthRatio() >= 0.75f) return 0;
                return context.Caster.Base.getHealthRatio() < 0.4f ? 10 : 6;
            },
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                1f, 2.4f, 0f, 0.5f, 2.8f, 45f, 1f, SkillImpactKind.Shield),
            ResolveRange = _ => 0f,
            ResolveEffectRadius = _ => 0f,
            TryUse = UseCounterStance,
        };
    }

    public static KnightTechniqueActiveUseProfile CreateLegendaryFlurryProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Object,
            TargetRelation = SkillUseTargetRelation.Hostile,
            CastMobility = ActiveAbilityCastMobility.StationaryDuringRecovery,
            UseCondition = IsMeleeTarget,
            ResolveAiWeight = context =>
            {
                if (context.Target?.isActor() != true ||
                    !KnightTechniqueAiRules.IsIsolated(context.Target) ||
                    !KnightTechniqueAiRules.IsHighTierTarget(context.Caster, context.Target, 4, 2f)) return 0;
                return context.Target.a.GetExtend().GetPowerLevel() >= context.Caster.GetPowerLevel() ? 10 : 7;
            },
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                4.8f, 0f, 0f, 0f, 4.8f, 360f, 1f, SkillImpactKind.Wave),
            ResolveRange = context => KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base),
            ResolveEffectRadius = _ => 0f,
            Construction = BodyActionCatalog.KnightLegendaryFlurry,
        };
    }

    private static bool UseCounterStance(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.14f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.GuardHold,
            direction,
            3f,
            0.9f,
            -58f,
            8f,
            KnightTrailStyle.None,
            1.08f,
            0.14f);
        KnightTechniqueStatuses.ApplyCounter(context);
        return true;
    }

    private static bool IsMeleeTarget(KnightTechniqueContext context)
    {
        return KnightTechniqueRuntimeService.IsValidGroundTarget(
            context.Caster,
            context.Target,
            0f,
            KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base));
    }
}
