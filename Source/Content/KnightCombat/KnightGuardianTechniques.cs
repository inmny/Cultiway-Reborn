using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

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
            TryUse = UseGuardStance,
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
            TryUse = UseRepulse,
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
            TryUse = UseGuardianBulwark,
        };
    }

    private static bool UseGuardStance(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.14f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.GuardHold,
            direction,
            2.5f,
            0.92f,
            -72f,
            -8f,
            KnightTrailStyle.None,
            1.12f,
            0.14f);
        KnightTechniqueStatuses.ApplyGuard(context);
        return true;
    }

    private static bool UseRepulse(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.43f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base);
        EquippedWeaponMotionKind motion = context.WeaponAsset.group_id == "hammer"
            ? EquippedWeaponMotionKind.Crush
            : EquippedWeaponMotionKind.Sweep;
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            motion,
            direction,
            motion == EquippedWeaponMotionKind.Crush ? 0.36f : 0.32f,
            2.2f,
            -122f,
            122f,
            KnightTrailStyle.Guardian,
            1.22f);
        KnightActionSequence sequence = KnightTechniqueRuntimeService.CreateSequence(context, null, 0f);
        DelayedActionsManager.addAction(() =>
        {
            if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
            Actor caster = active.Caster.Base;
            Vector2 center = caster.current_position;
            var targets = new List<Actor>();
            CombatTargeting.ForEachHostile(caster, center, 2.2f, target =>
            {
                if (!target.isFlying()) targets.Add(target);
            });
            for (var i = 0; i < targets.Count; i++)
            {
                Actor target = targets[i];
                KnightWeaponStrikeResolver.TryStrike(
                    active.Caster,
                    active.Weapon,
                    target,
                    0.65f,
                    onPositiveActorDamage: (damaged, _) =>
                    {
                        CombatForceEffects.ApplyRadialForce(caster, damaged, center, 1.3f, false);
                        CombatStatusEffects.ApplyStatus(damaged, StatusEffects.Daze, 0.35f, caster);
                    });
            }
        }, 0.18f);
        return true;
    }

    private static bool UseGuardianBulwark(KnightTechniqueContext context, ActiveAbilityUseOrigin origin)
    {
        KnightTechniqueRuntimeService.BeginAction(context.Caster, 0.68f);
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(context.Caster.Base);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.Sweep,
            direction,
            0.4f,
            2.65f,
            -168f,
            168f,
            KnightTrailStyle.Guardian,
            1.28f);
        KnightActionSequence sequence = KnightTechniqueRuntimeService.CreateSequence(context, null, 0f);
        DelayedActionsManager.addAction(() =>
        {
            if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
            Vector3 activeDirection = KnightTechniqueRuntimeService.ResolveDirection(active.Caster.Base);
            KnightTechniqueVisuals.SpawnWeaponMotion(
                active,
                EquippedWeaponMotionKind.Crush,
                activeDirection,
                0.3f,
                2.15f,
                -116f,
                22f,
                KnightTrailStyle.Guardian,
                1.42f);
        }, 0.34f);
        DelayedActionsManager.addAction(() =>
        {
            if (!sequence.TryContinue(out KnightTechniqueContext active)) return;
            Actor caster = active.Caster.Base;
            KnightTechniqueStatuses.ApplyBulwark(caster, caster);
            var allies = new List<Actor>();
            CombatTargeting.ForEachActor(
                caster,
                caster.current_position,
                3.5f,
                CombatTargeting.TargetDisposition.Any,
                candidate =>
                {
                    if (candidate != caster && SkillTargetRelationResolver.IsFriendly(caster, candidate))
                        allies.Add(candidate);
                });
            allies.Sort((left, right) =>
            {
                int health = left.getHealthRatio().CompareTo(right.getHealthRatio());
                if (health != 0) return health;
                int distance = Toolbox.SquaredDistVec2Float(caster.current_position, left.current_position)
                    .CompareTo(Toolbox.SquaredDistVec2Float(caster.current_position, right.current_position));
                return distance != 0 ? distance : left.data.id.CompareTo(right.data.id);
            });
            for (var i = 0; i < allies.Count && i < 3; i++)
            {
                KnightTechniqueStatuses.ApplyBulwark(allies[i], caster);
            }
        }, 0.52f);
        return true;
    }

    private static int ResolveGuardWeight(KnightTechniqueContext context)
    {
        if (KnightTechniqueStatuses.Has(context.Caster.Base, KnightTechniqueStatuses.GuardStance)) return 0;
        float health = context.Caster.Base.getHealthRatio();
        int recent = context.Caster.GetRecentAttackersSnapshot().Count;
        if (health < 0.35f || recent >= 3) return 22;
        return health < 0.7f || recent >= 2 ? 9 : 0;
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
        return wounded >= 2 || enemies >= 4 ? 20 : wounded == 1 ? 9 : 0;
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
