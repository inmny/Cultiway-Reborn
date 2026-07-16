using System;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;/// <summary>法器能力共用的直接伤害、反应伤害与护盾结算原语。</summary>
public static class ArtifactDamageEffects
{
    [ThreadStatic]
    private static int retaliationDepth;

    /// <summary>当前同步伤害链是否由反射、反击或转移伤害发起。</summary>
    public static bool IsResolvingRetaliation => retaliationDepth > 0;

    public static void DealDamage(
        Actor source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        if (damage <= 0f || target == null || target.isRekt()) return;
        EventSystemHub.Publish(new GetHitEvent
        {
            TargetID = target.data.id,
            Damage = damage,
            Element = composition,
            Attacker = source,
            AttackerPowerLevel = source.GetExtend().GetPowerLevel(),
            IgnoreDamageReduction = ignoreDamageReduction,
        });
    }

    public static void DealAreaDamage(
        Actor source,
        Vector2 center,
        float radius,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        ArtifactTargeting.ForEachHostile(source, center, radius, target =>
            DealDamage(source, target, damage, composition, ignoreDamageReduction));
    }

    /// <summary>
    /// 通过标准伤害入口结算反应伤害，并在同步事件链中附带递归截断标记。
    /// </summary>
    public static void DealRetaliationDamage(
        Actor source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false)
    {
        retaliationDepth++;
        try
        {
            DealDamage(source, target, damage, composition, ignoreDamageReduction);
        }
        finally
        {
            retaliationDepth--;
        }
    }

    /// <summary>用护盾池吸收伤害，并返回本次实际吸收值。</summary>
    public static float AbsorbDamage(ArtifactIncomingDamageEvent evt, ref float shield)
    {
        float absorbed = Mathf.Min(Mathf.Max(0f, shield), Mathf.Max(0f, evt.Damage));
        evt.Damage -= absorbed;
        shield -= absorbed;
        return absorbed;
    }
}
