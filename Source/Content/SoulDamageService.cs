using Cultiway.Core;
using Cultiway.Content.Components;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

/// <summary>以纯阴属性提交魂系伤害，供元神攻防统一调用。</summary>
public static class SoulDamageService
{
    /// <summary>向一个明确目标提交可伤害无身元神的神魂攻击。</summary>
    /// <param name="source">伤害来源；可为空。</param>
    /// <param name="target">明确指定的受击人物。</param>
    /// <param name="damage">原始伤害。</param>
    /// <param name="origin">主动伤害或二次反应。</param>
    /// <returns>目标有效且伤害已入队时返回真。</returns>
    public static bool Deal(
        BaseSimObject source,
        Actor target,
        float damage,
        DamageOrigin origin = DamageOrigin.Primary)
    {
        if (target == null || target.isRekt() || !target.isAlive() || damage <= 0f) return false;
        float? attackerPowerLevel = null;
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
                attackerPowerLevel = context.Owner.GetPowerLevel();
                if (context.Carrier?.Base != null && !context.Carrier.Base.isRekt())
                    eventSource = context.Carrier.Base;
            }
            else
            {
                attackerPowerLevel = sourceExtend.GetPowerLevel();
            }
        }
        if (damage <= 0f) return false;
        var evt = new GetHitEvent
        {
            TargetID = target.data.id,
            Damage = damage,
            Element = ElementComposition.Static.Neg,
            AttackType = AttackType.Other,
            DamageOrigin = origin,
            IgnoreDamageReduction = false,
            AttackerPowerLevel = attackerPowerLevel.HasValue
                ? attackerPowerLevel.Value
                : null
        };
        evt.BindAttacker(eventSource);
        EventSystemHub.Publish(evt);
        return true;
    }
}
