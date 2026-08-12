using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Effects;
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
            ResolveAiWeight = context => IsIsolated(context.Target) ? 15 : 7,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                2.2f, 0f, 0f, 0f, 2.2f, 5f, 1f, SkillImpactKind.Wave),
            ResolveRange = context => KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base),
            ResolveEffectRadius = _ => 0f,
            TryUse = UseCommittedStrike,
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
                return context.Caster.Base.getHealthRatio() < 0.4f ? 18 : 9;
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
                if (context.Target?.isActor() != true || !IsIsolated(context.Target)) return 0;
                return context.Target.a.GetExtend().GetPowerLevel() >= context.Caster.GetPowerLevel() ? 20 : 8;
            },
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                4.8f, 0f, 0f, 0f, 4.8f, 360f, 1f, SkillImpactKind.Wave),
            ResolveRange = context => KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base),
            ResolveEffectRadius = _ => 0f,
            TryUse = UseLegendaryFlurry,
        };
    }

    private static bool UseCommittedStrike(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.47f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base, context.Target);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.GuardTurn,
            direction,
            0.22f,
            1.2f,
            -82f,
            -118f,
            KnightTrailStyle.None,
            0.92f);
        KnightActionSequence sequence = KnightTechniqueRuntimeService.CreateSequence(
            context,
            context.Target,
            KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base));
        DelayedActionsManager.addAction(() =>
        {
            if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
            Vector3 activeDirection = KnightTechniqueRuntimeService.ResolveDirection(
                active.Caster.Base, active.Target);
            KnightTechniqueVisuals.SpawnWeaponMotion(
                active,
                EquippedWeaponMotionKind.Sweep,
                activeDirection,
                0.25f,
                2.05f,
                -132f,
                108f,
                KnightTrailStyle.DuelistFinisher,
                1.26f);
            active.Caster.Base.punchTargetAnimation(active.Target.current_position, true, true);
            DelayedActionsManager.addAction(() =>
            {
                if (!sequence.TryContinue(out KnightTechniqueContext strike)) return;
                float multiplier = IsIsolated(strike.Target) ? 1.55f : 1.25f;
                KnightWeaponStrikeResolver.TryStrike(
                    strike.Caster,
                    strike.Weapon,
                    strike.Target,
                    multiplier);
            }, 0.09f);
        }, 0.22f);
        return true;
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

    private static bool UseLegendaryFlurry(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.7f);
        KnightActionSequence sequence = KnightTechniqueRuntimeService.CreateSequence(
            context,
            context.Target,
            KnightTechniqueRuntimeService.ResolveMeleeRange(context.Caster.Base));
        float[] visualTimes = { 0.02f, 0.18f, 0.36f };
        float[] hitTimes = { 0.12f, 0.28f, 0.48f };
        float[] multipliers = { 0.55f, 0.65f, 0.9f };
        float[] starts = { -78f, 72f, -112f };
        float[] ends = { 55f, -86f, 104f };
        for (var i = 0; i < 3; i++)
        {
            int index = i;
            DelayedActionsManager.addAction(() =>
            {
                if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
                Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(
                    active.Caster.Base, active.Target);
                KnightTechniqueVisuals.SpawnWeaponMotion(
                    active,
                    EquippedWeaponMotionKind.Sweep,
                    direction,
                    index == 2 ? 0.2f : 0.16f,
                    index == 2 ? 2.15f : 1.78f + index * 0.12f,
                    starts[index],
                    ends[index],
                    index == 2 ? KnightTrailStyle.DuelistFinisher : KnightTrailStyle.Duelist,
                    index == 2 ? 1.32f : 1.08f + index * 0.06f);
            }, visualTimes[index]);
            DelayedActionsManager.addAction(() =>
            {
                if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
                active.Caster.Base.punchTargetAnimation(active.Target.current_position, true, true);
                KnightWeaponStrikeResolver.TryStrike(
                    active.Caster,
                    active.Weapon,
                    active.Target,
                    multipliers[index]);
            }, hitTimes[index]);
        }
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

    private static bool IsIsolated(BaseSimObject target)
    {
        if (target?.isActor() != true || target.isRekt()) return false;
        bool isolated = true;
        CombatTargeting.ForEachActor(
            target.a,
            target.current_position,
            1.8f,
            CombatTargeting.TargetDisposition.Any,
            candidate =>
            {
                if (candidate != target.a && SkillTargetRelationResolver.IsFriendly(target, candidate))
                    isolated = false;
            });
        return isolated;
    }
}
