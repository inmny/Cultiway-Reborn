using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>枪骑流派三个战技的条件、权重和执行。</summary>
internal static class KnightLancerTechniques
{
    public static KnightTechniqueActiveUseProfile CreateArmorPiercingThrustProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Object,
            TargetRelation = SkillUseTargetRelation.Hostile,
            CastMobility = ActiveAbilityCastMobility.BriefStop,
            UseCondition = context => KnightTechniqueRuntimeService.IsValidGroundTarget(
                context.Caster, context.Target, 0f, 1.9f),
            ResolveAiWeight = context =>
                context.Target?.isActor() == true &&
                KnightTechniqueStatuses.Has(context.Target.a, KnightTechniqueStatuses.ArmorBreak)
                    ? 0
                    : context.Target?.stats[S.armor] > 0.35f ? 16 : 8,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                1.8f, 0f, 0f, 0.5f, 2f, 5f, 1f, SkillImpactKind.Piercing),
            ResolveRange = _ => 1.9f,
            ResolveEffectRadius = _ => 0f,
            TryUse = UseArmorPiercingThrust,
        };
    }

    public static KnightTechniqueActiveUseProfile CreateFormationChargeProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Object,
            TargetRelation = SkillUseTargetRelation.Hostile,
            CastMobility = ActiveAbilityCastMobility.StationaryDuringRecovery,
            UseCondition = context => KnightTechniqueRuntimeService.IsValidGroundTarget(
                                       context.Caster, context.Target, 2.5f, 6f) &&
                                   KnightTechniqueRuntimeService.IsStraightPathClear(
                                       context.Caster.Base.current_position,
                                       context.ActiveTarget.Position),
            ResolveAiWeight = _ => 12,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                2.2f, 0.4f, 0f, 1.2f, 2.8f, 45f, 2f, SkillImpactKind.Piercing, 1f),
            ResolveRange = _ => 6f,
            ResolveEffectRadius = _ => 0.9f,
            TryUse = UseFormationCharge,
        };
    }

    public static KnightTechniqueActiveUseProfile CreateSkyfallStrikeProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Object,
            TargetRelation = SkillUseTargetRelation.Hostile,
            CastMobility = ActiveAbilityCastMobility.StationaryDuringRecovery,
            UseCondition = context => KnightTechniqueRuntimeService.IsValidGroundTarget(
                                       context.Caster, context.Target, 4f, 9f) &&
                                   KnightTechniqueRuntimeService.IsStraightPathClear(
                                       context.Caster.Base.current_position,
                                       context.ActiveTarget.Position),
            ResolveAiWeight = ResolveSkyfallWeight,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                4.5f, 0f, 0f, 1.4f, 5.5f, 360f, 4f, SkillImpactKind.HeavySkyfall),
            ResolveRange = _ => 9f,
            ResolveEffectRadius = _ => 2f,
            TryUse = UseSkyfallStrike,
        };
    }

    private static bool UseArmorPiercingThrust(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.34f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base, context.Target);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.Thrust,
            direction,
            0.3f,
            2.25f,
            0f,
            0f,
            KnightTrailStyle.Lancer,
            1.18f);
        context.Caster.Base.punchTargetAnimation(context.Target.current_position, true, true);
        KnightActionSequence sequence = KnightTechniqueRuntimeService.CreateSequence(context, context.Target, 1.9f);
        DelayedActionsManager.addAction(() =>
        {
            if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
            KnightWeaponStrikeResolver.TryStrike(
                active.Caster,
                active.Weapon,
                active.Target,
                1.2f,
                onPositiveActorDamage: (target, _) =>
                {
                    KnightTechniqueStatuses.ApplyArmorBreak(target, active.Caster.Base);
                });
        }, 0.14f);
        return true;
    }

    private static bool UseFormationCharge(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.77f);
        DelayedActionsManager.addAction(() =>
        {
            if (KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(
                    context.Caster, context.Technique, context.Weapon))
                KnightTechniqueMovement.StartCharge(context);
        }, 0.12f);
        return true;
    }

    private static bool UseSkyfallStrike(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.86f);
        DelayedActionsManager.addAction(() =>
        {
            if (KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(
                    context.Caster, context.Technique, context.Weapon))
                KnightTechniqueMovement.StartSkyfall(context);
        }, 0.16f);
        return true;
    }

    private static int ResolveSkyfallWeight(KnightTechniqueContext context)
    {
        if (context.Target == null || context.Target.isRekt()) return 0;
        int nearby = 0;
        CombatTargeting.ForEachHostile(
            context.Caster.Base,
            context.Target.current_position,
            2f,
            actor =>
            {
                if (!actor.isFlying()) nearby++;
            });
        if (nearby >= 4) return 20;
        if (!context.Target.isActor()) return 8;
        return context.Target.a.GetExtend().GetPowerLevel() >= context.Caster.GetPowerLevel() ? 14 : 0;
    }
}
