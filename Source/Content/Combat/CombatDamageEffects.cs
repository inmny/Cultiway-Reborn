using System;
using Cultiway.Core;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的直接伤害、反应伤害与护盾结算原语。</summary>
public static class CombatDamageEffects
{
    [ThreadStatic]
    private static int reactionDepth;

    /// <summary>当前同步伤害链是否由反射、反击、持续伤害或其他二次反应发起。</summary>
    public static bool IsResolvingReaction => reactionDepth > 0;

    /// <summary>通过标准受击事件对单个目标结算伤害。</summary>
    public static void DealDamage(
        BaseSimObject source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        float? attackerPowerLevel = null)
    {
        if (damage <= 0f || target == null || target.isRekt()) return;
        float powerLevel = attackerPowerLevel ??
                           (source != null && source.isActor() && !source.isRekt()
                               ? source.a.GetExtend().GetPowerLevel()
                               : 0f);
        EventSystemHub.Publish(new GetHitEvent
        {
            TargetID = target.data.id,
            Damage = damage,
            Element = composition,
            Attacker = source,
            AttackerPowerLevel = powerLevel,
            IgnoreDamageReduction = ignoreDamageReduction,
        });
    }

    /// <summary>通过标准受击事件对范围内的所有敌对单位结算伤害。</summary>
    public static void DealAreaDamage(
        Actor source,
        Vector2 center,
        float radius,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        CombatTargeting.ForEachHostile(source, center, radius, target =>
            DealDamage(source, target, damage, composition, ignoreDamageReduction));
    }

    /// <summary>在递归截断标记内通过标准受击入口结算一次二次反应伤害。</summary>
    public static void DealReactionDamage(
        BaseSimObject source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        float? attackerPowerLevel = null)
    {
        reactionDepth++;
        try
        {
            DealDamage(source, target, damage, composition, ignoreDamageReduction, attackerPowerLevel);
        }
        finally
        {
            reactionDepth--;
        }
    }

    /// <summary>在递归截断标记内对范围内的所有敌对单位结算二次反应伤害。</summary>
    public static void DealAreaReactionDamage(
        Actor source,
        Vector2 center,
        float radius,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        reactionDepth++;
        try
        {
            DealAreaDamage(source, center, radius, damage, composition, ignoreDamageReduction);
        }
        finally
        {
            reactionDepth--;
        }
    }

    /// <summary>兼容既有反击调用的语义化别名。</summary>
    public static void DealRetaliationDamage(
        Actor source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        DealReactionDamage(source, target, damage, composition, ignoreDamageReduction);
    }

    /// <summary>用护盾池吸收最终伤害，并返回本次实际吸收值。</summary>
    public static float AbsorbDamage(ref float damage, ref float shield)
    {
        float absorbed = Mathf.Min(Mathf.Max(0f, shield), Mathf.Max(0f, damage));
        damage -= absorbed;
        shield -= absorbed;
        return absorbed;
    }
}
