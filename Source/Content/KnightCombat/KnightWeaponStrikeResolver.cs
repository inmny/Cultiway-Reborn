using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Combat;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>以施放时的真实武器执行原版攻击，并关联正数最终伤害。</summary>
internal static class KnightWeaponStrikeResolver
{
    private static readonly Dictionary<long, PendingDamageCallback> PendingCallbacks = new();
    private static long nextScopeId;

    public static void Init()
    {
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
    }

    public static bool TryStrike(
        ActorExtend caster,
        Item weapon,
        BaseSimObject target,
        float damageMultiplier,
        Kingdom attackKingdom = null,
        DamageOrigin damageOrigin = DamageOrigin.Primary,
        Action<Actor, float> onPositiveActorDamage = null)
    {
        Actor owner = caster?.Base;
        if (!EquippedWeaponVisualService.IsCurrent(owner, weapon) || target == null || target.isRekt() ||
            !owner.canAttackTarget(target)) return false;

        long scopeId = Interlocked.Increment(ref nextScopeId);
        if (onPositiveActorDamage != null && target.isActor())
        {
            PendingCallbacks.Add(scopeId, new PendingDamageCallback(target.getID(), onPositiveActorDamage));
            long cleanupScope = scopeId;
            DelayedActionsManager.addAction(() => PendingCallbacks.Remove(cleanupScope), 1f);
        }

        Vector3 hitPosition = new(target.current_position.x, target.current_position.y, target.getHeight());
        var attack = new AttackData(
            owner,
            target.current_tile,
            hitPosition,
            owner.current_position,
            target,
            attackKingdom ?? owner.kingdom,
            AttackType.Weapon,
            owner.haveMetallicWeapon(),
            true);
        AttackDataResult result;
        float previousMultiplier = AttackDamageScaleContext.Enter(damageMultiplier);
        try
        {
            using (DamageResolutionContext.Enter(damageOrigin, scopeId))
            {
                result = MapBox.checkAttackFor(attack, target);
            }
        }
        finally
        {
            AttackDamageScaleContext.Restore(previousMultiplier);
        }

        if (onPositiveActorDamage != null && result.state != ApplyAttackState.Hit)
            PendingCallbacks.Remove(scopeId);
        return result.state == ApplyAttackState.Hit;
    }

    public static void ClearWorldState()
    {
        PendingCallbacks.Clear();
    }

    private static void OnDamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        long scopeId = DamageResolutionContext.CurrentSourceScopeId;
        if (scopeId == 0 || !PendingCallbacks.TryGetValue(scopeId, out PendingDamageCallback pending)) return;
        PendingCallbacks.Remove(scopeId);
        if (target.Base.getID() == pending.TargetId) pending.Callback(target.Base, damage);
    }

    private readonly struct PendingDamageCallback
    {
        public readonly long TargetId;
        public readonly Action<Actor, float> Callback;

        public PendingDamageCallback(long targetId, Action<Actor, float> callback)
        {
            TargetId = targetId;
            Callback = callback;
        }
    }
}
