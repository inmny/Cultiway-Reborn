using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Content.Skills;
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
                    : context.Target?.stats[S.armor] > 0.35f ? 6 : 3,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                1.8f, 0f, 0f, 0.5f, 2f, 5f, 1f, SkillImpactKind.Piercing),
            ResolveRange = _ => 1.9f,
            ResolveEffectRadius = _ => 0f,
            Construction = BodyActionCatalog.KnightArmorPiercingThrust,
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
            ResolveAiWeight = ResolveFormationChargeWeight,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                2.2f, 0.4f, 0f, 1.2f, 2.8f, 45f, 2f, SkillImpactKind.Piercing, 1f),
            ResolveRange = _ => 6f,
            ResolveEffectRadius = _ => 0.9f,
            Construction = BodyActionCatalog.KnightFormationCharge,
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
            Construction = BodyActionCatalog.KnightSkyfallStrike,
        };
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
        if (!KnightTechniqueAiRules.IsHighTierTarget(context.Caster, context.Target, 4, 2f)) return 0;
        if (nearby >= 4) return 10;
        return context.Target.isActor() &&
               context.Target.a.GetExtend().GetPowerLevel() >= context.Caster.GetPowerLevel()
            ? 8
            : 6;
    }

    private static int ResolveFormationChargeWeight(KnightTechniqueContext context)
    {
        if (!KnightTechniqueRuntimeService.IsValidGroundTarget(context.Caster, context.Target, 2.5f, 6f) ||
            !KnightTechniqueRuntimeService.IsStraightPathClear(
                context.Caster.Base.current_position,
                context.ActiveTarget.Position)) return 0;
        float distance = Vector2.Distance(
            context.Caster.Base.current_position,
            context.Target.current_position);
        return distance >= 4f ? 8 : 5;
    }
}
