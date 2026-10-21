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

        return ResolveStrike(caster, target, damageMultiplier, attackKingdom, damageOrigin, onPositiveActorDamage)
            .state == ApplyAttackState.Hit;
    }

    /// <summary>徒手样板复用同一原版近战结算及最终伤害回调，不添加第二次攻击。</summary>
    /// <param name="caster">已完成接触距离检查的出掌者。</param>
    /// <param name="target">实际接触的原目标。</param>
    /// <param name="onPositiveActorDamage">只有产生正数最终伤害时才通知。</param>
    internal static AttackDataResult StrikeUnarmed(ActorExtend caster, BaseSimObject target,
        Action<Actor, float> onPositiveActorDamage, float damageMultiplier = 1f)
    {
        return ResolveStrike(caster, target, damageMultiplier, caster.Base.kingdom, DamageOrigin.Primary, onPositiveActorDamage);
    }

    /// <summary>共用一次原版攻击的范围、来源与最终伤害关联。</summary>
    /// <param name="caster">攻击者。</param>
    /// <param name="target">已检查有效性的目标。</param>
    /// <param name="damageMultiplier">伤害倍率。</param>
    /// <param name="attackKingdom">攻击阵营。</param>
    /// <param name="damageOrigin">伤害来源类别。</param>
    /// <param name="onPositiveActorDamage">实际正数伤害的回调。</param>
    private static AttackDataResult ResolveStrike(ActorExtend caster, BaseSimObject target, float damageMultiplier,
        Kingdom attackKingdom, DamageOrigin damageOrigin, Action<Actor, float> onPositiveActorDamage)
    {
        Actor owner = caster.Base;

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
        if (result.state is ApplyAttackState.Block or ApplyAttackState.Deflect)
            CombatImpactVisuals.OnDefended(target, owner);
        return result;
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
