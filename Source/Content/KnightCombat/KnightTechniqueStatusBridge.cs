using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content.KnightCombat;

/// <summary>在最终伤害阶段处理格挡、壁垒与反击。</summary>
internal static class KnightTechniqueStatusBridge
{
    private static readonly Dictionary<long, PendingCounter> PendingCounters = new();
    private static long worldGeneration;

    public static void Init()
    {
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Adaptation, ApplyAdaptation);
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
    }

    public static void ClearWorldState()
    {
        PendingCounters.Clear();
        worldGeneration++;
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
            KnightTechniqueStatuses.IsStanceCurrent(target, guard))
        {
            damage *= 0.55f;
            KnightTechniqueStatuses.Remove(target.Base, KnightTechniqueStatuses.GuardStance);
            EndPreparation(target.Base, guard);
            PlayGuardResponse(target, attacker, guard);
        }

        if (!attacker.isActor() || !IsNearbyMelee(target.Base, attacker.a) ||
            !KnightTechniqueStatuses.TryGetBound(
                target.Base,
                KnightTechniqueStatuses.CounterStance,
                out _,
                out KnightBoundWeaponStatus counter) ||
            !KnightTechniqueStatuses.IsStanceCurrent(target, counter)) return;

        damage *= 0.5f;
        KnightTechniqueStatuses.Remove(target.Base, KnightTechniqueStatuses.CounterStance);
        EndPreparation(target.Base, counter);
        PendingCounters[targetId] = new PendingCounter(
            targetId,
            attacker.getID(),
            counter.Weapon,
            counter.Technique);
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
        long generation = worldGeneration;
        DelayedActionsManager.addAction(() =>
        {
            if (generation == worldGeneration) ExecuteCounter(pending);
        }, 0.06f);
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

        var activeTarget = new ActiveAbilityTarget(attacker, attacker.GetSimPos(), runtimeData: ReactionData());
        ConstructedAbilityService.StartPrepaid(casterExtend, BodyActionCatalog.KnightCounterRiposte,
            activeTarget, null, explicitReactionTarget: true, selectionAlreadyChecked: true);
    }

    /// <summary>本次受击消耗架势时，只撤回该架势自己的起势动作。</summary>
    /// <param name="actor">目前实际受击人物。</param><param name="bound">本次被消耗的状态。</param>
    private static void EndPreparation(Actor actor, KnightBoundWeaponStatus bound)
    {
        if (ConstructedAbilityService.IsBodyControlledBy(actor.id, bound.PreparationCastId))
            ConstructedAbilityService.Cancel(bound.PreparationCastId);
    }

    /// <summary>真正减伤才用原持物做出承力动作。</summary>
    /// <param name="target">已经成功格挡的人物。</param><param name="attacker">本次来袭来源。</param>
    /// <param name="bound">已经核对身份的实际武器。</param>
    private static void PlayGuardResponse(
        ActorExtend target,
        BaseSimObject attacker,
        KnightBoundWeaponStatus bound)
    {
        var self = new ActiveAbilityTarget(target.Base, target.Base.GetSimPos(), runtimeData: ReactionData());
        ConstructedAbilityService.StartPrepaid(target, BodyActionCatalog.KnightGuardResponseFor(bound.Weapon.getAsset().group_id),
            self, null, direction: KnightTechniqueRuntimeService.ResolveDirection(target.Base, attacker));
    }

    /// <summary>反应伤害沿原结算，不能触发另一轮架势反击。</summary>
    private static SkillCastRuntimeData ReactionData() => new()
    {
        Origin = ActiveAbilityUseOrigin.Reaction,
        DamageOrigin = DamageOrigin.Reaction,
    };

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
