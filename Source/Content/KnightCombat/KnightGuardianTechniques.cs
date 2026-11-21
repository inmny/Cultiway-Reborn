using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Content.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.KnightCombat;

/// <summary>守卫流派三个战技的条件、权重和执行。</summary>
internal static class KnightGuardianTechniques
{
    public static KnightTechniqueActiveUseProfile CreateGuardStanceProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Self,
            TargetRelation = SkillUseTargetRelation.Self,
            CastMobility = ActiveAbilityCastMobility.Mobile,
            ResolveAiWeight = ResolveGuardWeight,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                0f, 1.8f, 0f, 0f, 1.8f, 5f, 1f, SkillImpactKind.Shield),
            ResolveRange = _ => 0f,
            ResolveEffectRadius = _ => 0f,
            Construction = BodyActionCatalog.KnightGuard,
            ResolveConstruction = item => BodyActionCatalog.KnightGuardFor(item.group_id),
        };
    }

    public static KnightTechniqueActiveUseProfile CreateRepulseProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Self,
            TargetRelation = SkillUseTargetRelation.Self,
            CastMobility = ActiveAbilityCastMobility.StationaryDuringRecovery,
            UseCondition = context => CountGroundHostiles(context.Caster.Base, 2.2f) > 0,
            ResolveAiWeight = context =>
            {
                int count = CountGroundHostiles(context.Caster.Base, 2.2f);
                return count < 2 ? 0 : count >= 3 ? 18 : 8;
            },
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                1.6f, 0.3f, 0f, 1.2f, 2f, 45f, 3f, SkillImpactKind.GroundWave),
            ResolveRange = _ => 0f,
            ResolveEffectRadius = _ => 2.2f,
            Construction = BodyActionCatalog.KnightRepulseFor("hammer"),
            ResolveConstruction = item => BodyActionCatalog.KnightRepulseFor(item.group_id),
        };
    }

    public static KnightTechniqueActiveUseProfile CreateGuardianBulwarkProfile()
    {
        return new KnightTechniqueActiveUseProfile
        {
            TargetMode = ActiveAbilityTargetMode.Self,
            TargetRelation = SkillUseTargetRelation.Self,
            CastMobility = ActiveAbilityCastMobility.BriefStop,
            ResolveAiWeight = ResolveBulwarkWeight,
            ResolveTacticalProfile = _ => new ActiveAbilityTacticalProfile(
                0f, 3.2f, 2.8f, 0f, 4.5f, 360f, 4f, SkillImpactKind.Shield),
            ResolveRange = _ => 0f,
            ResolveEffectRadius = _ => 3.5f,
            Construction = BodyActionCatalog.KnightBulwarkFor("hammer"),
            ResolveConstruction = item => BodyActionCatalog.KnightBulwarkFor(item.group_id),
        };
    }

    private static int ResolveGuardWeight(KnightTechniqueContext context)
    {
        if (KnightTechniqueStatuses.Has(context.Caster.Base, KnightTechniqueStatuses.GuardStance)) return 0;
        float health = context.Caster.Base.getHealthRatio();
        int recent = context.Caster.GetRecentAttackersSnapshot().Count;
        if (health < 0.35f || recent >= 3) return 10;
        return health < 0.7f || recent >= 2 ? 5 : 0;
    }

    private static int ResolveBulwarkWeight(KnightTechniqueContext context)
    {
        Actor caster = context.Caster.Base;
        int wounded = caster.getHealthRatio() < 0.45f ? 1 : 0;
        CombatTargeting.ForEachActor(
            caster,
            caster.current_position,
            3.5f,
            CombatTargeting.TargetDisposition.Any,
            actor =>
            {
                if (actor != caster && SkillTargetRelationResolver.IsFriendly(caster, actor) &&
                    actor.getHealthRatio() < 0.45f) wounded++;
            });
        int enemies = CountGroundHostiles(caster, 3.5f);
        if (wounded == 0 && enemies < 4 && !KnightTechniqueAiRules.IsCritical(context.Caster)) return 0;
        if (!KnightTechniqueAiRules.IsHighTierTarget(context.Caster, context.Target, 4, 3.5f) &&
            !KnightTechniqueAiRules.IsCritical(context.Caster)) return 0;
        return wounded >= 2 || enemies >= 4 ? 10 : 6;
    }

    private static int CountGroundHostiles(Actor caster, float radius)
    {
        int count = 0;
        CombatTargeting.ForEachHostile(caster, caster.current_position, radius, target =>
        {
            if (!target.isFlying()) count++;
        });
        return count;
    }
}
