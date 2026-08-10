using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>把最终伤害与击杀回调转换为对应的修炼触发。</summary>
public static class CultivateMethodTriggers
{
    /// <summary>注册最终伤害和击杀触发桥接。</summary>
    [Hotfixable]
    public static void Init()
    {
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
        ActorExtend.RegisterActionOnKill(OnKill);
    }

    /// <summary>直接结算同步天雷触发，并把涉及两个角色的战斗伤害发布到主逻辑线程。</summary>
    private static void OnDamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        if (target?.Base == null) return;
        Actor victim = target.Base;
        // 原版最终通过 (int)(-damage) 扣血，低于 1 的浮点伤害不会造成真实生命损失。
        float actualDamage = Mathf.Min(Mathf.Floor(Mathf.Max(0f, damage)), Mathf.Max(0f, victim.data.health));
        if (actualDamage <= 0f) return;

        if (attacker == null)
        {
            if (DamageResolutionContext.CurrentSourceScopeId == victim.data.id &&
                actualDamage < victim.data.health)
            {
                var context = new CultivationTriggerContext(
                    target,
                    CultivationTriggerKind.HeavenlyLightningDamage,
                    actualDamage: actualDamage,
                    referenceMaxHealth: Mathf.Max(1f, victim.getMaxHealth()));
                CultivateMethods.TryDispatch(in context);
            }
            return;
        }

        if (attacker.isRekt() || !attacker.isActor()) return;
        Actor source = attacker.a;
        if (source == victim || !source.TryGetExtend(out ActorExtend sourceExtend)) return;
        float attackerPower = sourceExtend.GetPowerLevel();
        EventSystemHub.Publish(new CultivationDamageResolvedEvent(
            source.data.id,
            victim.data.id,
            actualDamage,
            attackerPower,
            target.GetPowerLevel(),
            Mathf.Max(1f, victim.getMaxHealth())));
    }

    /// <summary>以击杀发生时的不可变数据直接结算击杀修炼。</summary>
    private static void OnKill(ActorExtend killer, Actor victim, Kingdom _)
    {
        if (killer?.Base == null || victim == null || killer.Base == victim || victim.current_tile == null) return;
        Vector2Int position = victim.current_tile.pos;
        float victimPower = victim.TryGetExtend(out ActorExtend victimExtend)
            ? victimExtend.GetPowerLevel()
            : 0f;
        var context = new CultivationTriggerContext(
            killer,
            CultivationTriggerKind.Kill,
            practitionerPower: killer.GetPowerLevel(),
            opponentPower: victimPower,
            tileX: position.x,
            tileY: position.y);
        CultivateMethods.TryDispatch(in context);
    }
}
