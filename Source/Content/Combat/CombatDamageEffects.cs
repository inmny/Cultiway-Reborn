using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>内容系统共用的直接伤害、反应伤害与护盾结算原语。</summary>
public static class CombatDamageEffects
{
    /// <summary>当前实际伤害结算是否由反射、反击、持续伤害或其他二次反应发起。</summary>
    public static bool IsResolvingReaction => DamageResolutionContext.IsReaction;

    /// <summary>通过标准受击事件对单个目标排队结算伤害。</summary>
    /// <param name="source">实际作用来源。</param>
    /// <param name="target">实际受击人物。</param>
    /// <param name="damage">尚待正常抗性及防御处理的作用量。</param>
    /// <param name="composition">实际元素组成。</param>
    /// <param name="ignoreDamageReduction">是否由调用方明确跳过减伤。</param>
    /// <param name="attackerPowerLevel">可选的既定来源层次。</param>
    /// <param name="damageOrigin">主作用或二次反应。</param>
    /// <param name="attackType">交给原版的攻击类别。</param>
    public static void DealDamage(
        BaseSimObject source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        float? attackerPowerLevel = null,
        DamageOrigin damageOrigin = DamageOrigin.Primary,
        AttackType attackType = AttackType.Other)
    {
        if (damage <= 0f || target == null || target.isRekt()) return;
        GetHitEvent evt = PrepareDamage(source, damage, composition, ignoreDamageReduction, attackerPowerLevel, damageOrigin, attackType);
        evt.TargetID = target.data.id;
        EventSystemHub.Publish(evt);
    }

    /// <summary>主线程确定实际来源、身份归属及缩放，构造交锋和后续受击共用同一作用量。</summary>
    /// <param name="source">实际来源或临时载体。</param>
    /// <param name="damage">来源给出的基础作用量。</param>
    /// <param name="composition">实际材料组成。</param>
    /// <param name="ignoreDamageReduction">正常减伤是否由调用方明确跳过。</param>
    /// <param name="attackerPowerLevel">可选的既定来源层次。</param>
    /// <param name="damageOrigin">主作用或二次反应。</param>
    /// <param name="attackType">原版攻击类别。</param>
    internal static GetHitEvent PrepareDamage(BaseSimObject source, float damage, ElementComposition composition,
        bool ignoreDamageReduction = false, float? attackerPowerLevel = null,
        DamageOrigin damageOrigin = DamageOrigin.Primary, AttackType attackType = AttackType.Other)
    {
        ActorExtend powerOwner = null;
        BaseSimObject eventSource = source;
        if (source?.isActor() == true && !source.isRekt())
        {
            ActorExtend sourceExtend = source.a.GetExtend();
            bool hasContext = SkillCasterContextService.TryGetCurrent(
                sourceExtend,
                out SkillCasterContext context);
            if (!hasContext && sourceExtend.HasComponent<YuanshenSoulCarrierState>())
            {
                context = SkillCasterContextService.Resolve(sourceExtend);
                hasContext = context.IsValid;
            }
            if (hasContext)
            {
                damage *= context.EffectScale;
                powerOwner = context.Owner;
                if (context.Carrier?.Base != null && !context.Carrier.Base.isRekt())
                    eventSource = context.Carrier.Base;
            }
            else
            {
                powerOwner = sourceExtend;
            }
        }
        float powerLevel = attackerPowerLevel ??
                           (powerOwner != null
                               ? powerOwner.GetPowerLevel()
                               : 0f);
        var evt = new GetHitEvent
        {
            Damage = damage,
            Element = composition,
            AttackType = attackType,
            AttackerPowerLevel = powerLevel,
            IgnoreDamageReduction = ignoreDamageReduction,
            DamageOrigin = damageOrigin,
            SourceScopeId = DamageResolutionContext.CurrentSourceScopeId,
        };
        evt.BindAttacker(eventSource);
        return evt;
    }

    /// <summary>通过标准受击事件对范围内的所有敌对单位结算伤害。</summary>
    public static void DealAreaDamage(
        Actor source,
        Vector2 center,
        float radius,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        DamageOrigin damageOrigin = DamageOrigin.Primary,
        AttackType attackType = AttackType.Other)
    {
        CombatTargeting.ForEachHostile(source, center, radius, target =>
            DealDamage(source, target, damage, composition, ignoreDamageReduction,
                damageOrigin: damageOrigin,
                attackType: attackType));
    }

    /// <summary>在递归截断标记内通过标准受击入口结算一次二次反应伤害。</summary>
    public static void DealReactionDamage(
        BaseSimObject source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        float? attackerPowerLevel = null,
        AttackType attackType = AttackType.Other)
    {
        DealDamage(source, target, damage, composition, ignoreDamageReduction, attackerPowerLevel,
            DamageOrigin.Reaction, attackType);
    }

    /// <summary>在递归截断标记内对范围内的所有敌对单位结算二次反应伤害。</summary>
    public static void DealAreaReactionDamage(
        Actor source,
        Vector2 center,
        float radius,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        AttackType attackType = AttackType.Other)
    {
        DealAreaDamage(source, center, radius, damage, composition, ignoreDamageReduction,
            DamageOrigin.Reaction, attackType);
    }

    /// <summary>兼容既有反击调用的语义化别名。</summary>
    public static void DealRetaliationDamage(
        Actor source,
        Actor target,
        float damage,
        ElementComposition composition,
        bool ignoreDamageReduction = false,
        AttackType attackType = AttackType.Other)
    {
        DealReactionDamage(source, target, damage, composition, ignoreDamageReduction,
            attackType: attackType);
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
