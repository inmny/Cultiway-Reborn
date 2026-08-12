using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>在最终伤害阶段处理格挡、壁垒与反击。</summary>
internal static class KnightTechniqueStatusBridge
{
    private static readonly Dictionary<long, PendingCounter> PendingCounters = new();

    public static void Init()
    {
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Adaptation, ApplyAdaptation);
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
    }

    public static void ClearWorldState()
    {
        PendingCounters.Clear();
    }

    private static void ApplyAdaptation(
        ActorExtend target,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        long targetId = target.Base.getID();
        PendingCounters.Remove(targetId);
        if (damage <= 0f || attacker == null || attacker.isRekt() ||
            DamageResolutionContext.IsReaction ||
            !SkillTargetRelationResolver.HasHostileRelation(attacker, target.Base)) return;

        if (KnightTechniqueStatuses.Has(target.Base, KnightTechniqueStatuses.GuardianBulwark))
            damage *= 0.75f;
        if (attackType != AttackType.Weapon) return;

        if (KnightTechniqueStatuses.TryGetBound(
                target.Base,
                KnightTechniqueStatuses.GuardStance,
                out _,
                out KnightBoundWeaponStatus guard) &&
            IsBoundWeaponCurrent(target, guard))
        {
            damage *= 0.55f;
            KnightTechniqueStatuses.Remove(target.Base, KnightTechniqueStatuses.GuardStance);
            KnightTechniqueVisuals.StopStance(target.Base);
            PlayGuardResponse(target, attacker, guard, false);
        }

        if (!attacker.isActor() || !IsNearbyMelee(target.Base, attacker.a) ||
            !KnightTechniqueStatuses.TryGetBound(
                target.Base,
                KnightTechniqueStatuses.CounterStance,
                out _,
                out KnightBoundWeaponStatus counter) ||
            !IsBoundWeaponCurrent(target, counter)) return;

        damage *= 0.5f;
        KnightTechniqueStatuses.Remove(target.Base, KnightTechniqueStatuses.CounterStance);
        KnightTechniqueVisuals.StopStance(target.Base);
        PendingCounters[targetId] = new PendingCounter(
            targetId,
            attacker.getID(),
            counter.Weapon,
            counter.Technique);
        PlayGuardResponse(target, attacker, counter, true);
    }

    private static void OnDamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        long targetId = target.Base.getID();
        if (!PendingCounters.TryGetValue(targetId, out PendingCounter pending)) return;
        PendingCounters.Remove(targetId);
        DelayedActionsManager.addAction(() => ExecuteCounter(pending), 0.06f);
    }

    private static void ExecuteCounter(PendingCounter pending)
    {
        Actor caster = World.world.units.get(pending.CasterId);
        Actor attacker = World.world.units.get(pending.AttackerId);
        if (caster == null || attacker == null || caster.isRekt() || attacker.isRekt() ||
            !caster.TryGetExtend(out ActorExtend casterExtend) ||
            !KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(
                casterExtend,
                pending.Technique,
                pending.Weapon) ||
            !KnightTechniqueRuntimeService.IsWithinRange(
                caster,
                attacker,
                KnightTechniqueRuntimeService.ResolveMeleeRange(caster))) return;

        var activeTarget = new ActiveAbilityTarget(attacker, attacker.GetSimPos());
        if (!KnightTechniqueAccessService.TryCreateContext(
                casterExtend,
                pending.Technique,
                pending.Weapon,
                caster.getWeaponAsset(),
                attacker,
                activeTarget,
                out KnightTechniqueContext context)) return;
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(caster, attacker);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.Sweep,
            direction,
            0.18f,
            1.55f,
            55f,
            -72f,
            KnightTrailStyle.Duelist,
            1.12f);
        DelayedActionsManager.addAction(() =>
        {
            if (!KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(
                    casterExtend,
                    pending.Technique,
                    pending.Weapon) ||
                !KnightTechniqueRuntimeService.IsWithinRange(
                    caster,
                    attacker,
                    KnightTechniqueRuntimeService.ResolveMeleeRange(caster))) return;
            KnightWeaponStrikeResolver.TryStrike(
                casterExtend,
                pending.Weapon,
                attacker,
                1f,
                damageOrigin: DamageOrigin.Reaction);
        }, 0.09f);
    }

    private static void PlayGuardResponse(
        ActorExtend target,
        BaseSimObject attacker,
        KnightBoundWeaponStatus bound,
        bool counter)
    {
        var activeTarget = new ActiveAbilityTarget(attacker, attacker.GetSimPos());
        if (!KnightTechniqueAccessService.TryCreateContext(
                target,
                bound.Technique,
                bound.Weapon,
                target.Base.getWeaponAsset(),
                attacker,
                activeTarget,
                out KnightTechniqueContext context)) return;
        Vector3 direction = KnightTechniqueRuntimeService.ResolveDirection(target.Base, attacker);
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.GuardTurn,
            direction,
            0.12f,
            1.05f,
            counter ? -42f : -32f,
            counter ? 18f : 8f,
            counter ? KnightTrailStyle.Duelist : KnightTrailStyle.Guardian,
            1.14f);
    }

    private static bool IsBoundWeaponCurrent(ActorExtend target, KnightBoundWeaponStatus bound)
    {
        return KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(
            target,
            bound.Technique,
            bound.Weapon);
    }

    private static bool IsNearbyMelee(Actor target, Actor attacker)
    {
        float range = 2.5f + target.stats[S.size] + attacker.stats[S.size];
        return Toolbox.SquaredDistVec2Float(target.current_position, attacker.current_position) <= range * range;
    }

    private readonly struct PendingCounter
    {
        public readonly long CasterId;
        public readonly long AttackerId;
        public readonly Item Weapon;
        public readonly KnightTechniqueAsset Technique;

        public PendingCounter(long casterId, long attackerId, Item weapon, KnightTechniqueAsset technique)
        {
            CasterId = casterId;
            AttackerId = attackerId;
            Weapon = weapon;
            Technique = technique;
        }
    }
}
