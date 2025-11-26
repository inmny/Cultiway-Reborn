using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 修炼方式触发器系统（处理被动修炼触发）
/// </summary>
public static class CultivateMethodTriggers
{
    /// <summary>
    /// 初始化所有被动修炼触发器
    /// </summary>
    [Hotfixable]
    public static void Init()
    {
        ActorExtend.RegisterActionOnKill(OnKillTrigger);
        ActorExtend.RegisterCombatActionOnAttack(OnAttackTrigger);
        ActorExtend.RegisterActionOnBeAttacked(OnBeAttackedTrigger);
    }
    
    /// <summary>
    /// 击杀事件触发
    /// </summary>
    [Hotfixable]
    private static void OnKillTrigger(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        var mainCultibook = killer.GetMainCultibook();
        if (mainCultibook == null) return;
        
        var method = mainCultibook.GetCultivateMethod();
        if (method == null) return;
        
        if (method.TriggerType != CultivateTriggerType.Passive) return;
        if (method.PassiveTriggerEvents == null || 
            !method.PassiveTriggerEvents.Contains(PassiveTriggerEvents.OnKill)) return;
        
        if (method.CanCultivate != null && !method.CanCultivate(killer)) return;
        
        if (!killer.HasCultisys<Xian>()) return;
        ref var xian = ref killer.GetCultisys<Xian>();
        
        // 根据目标强度计算灵力收益
        var victimPower = victim.GetExtend().GetPowerLevel() + 1;
        var efficiency = method.GetEfficiency?.Invoke(killer) ?? 1f;
        var wakanGain = victimPower * efficiency;
        
        var maxWakan = killer.Base.stats[BaseStatses.MaxWakan.id];
        wakanGain = Mathf.Min(wakanGain, maxWakan - xian.wakan);
        if (wakanGain > 0)
        {
            xian.wakan += wakanGain;
            
            method.OnSideEffect?.Invoke(killer, wakanGain);
        }
    }
    
    /// <summary>
    /// 攻击事件触发
    /// </summary>
    [Hotfixable]
    private static void OnAttackTrigger(ActorExtend attacker, BaseSimObject target, 
        ListPool<CombatActionAsset> actionPool)
    {
        var mainCultibook = attacker.GetMainCultibook();
        if (mainCultibook == null) return;
        
        var method = mainCultibook.GetCultivateMethod();
        if (method == null) return;
        
        if (method.TriggerType != CultivateTriggerType.Passive) return;
        if (method.PassiveTriggerEvents == null || 
            !method.PassiveTriggerEvents.Contains(PassiveTriggerEvents.OnAttack)) return;
        
        if (method.CanCultivate != null && !method.CanCultivate(attacker)) return;
        
        // 计算修炼收益（基于攻击伤害，这里简化处理）
        if (!attacker.HasCultisys<Xian>()) return;
        ref var xian = ref attacker.GetCultisys<Xian>();
        
        var efficiency = method.GetEfficiency?.Invoke(attacker) ?? 1f;
        var basePower = attacker.GetPowerLevel() + 1;
        var wakanGain = basePower * 0.1f * efficiency;
        
        var maxWakan = attacker.Base.stats[BaseStatses.MaxWakan.id];
        wakanGain = Mathf.Min(wakanGain, maxWakan - xian.wakan);
        if (wakanGain > 0)
        {
            xian.wakan += wakanGain;
            
            method.OnSideEffect?.Invoke(attacker, wakanGain);
        }
    }
    
    /// <summary>
    /// 被攻击事件触发
    /// </summary>
    [Hotfixable]
    private static void OnBeAttackedTrigger(ActorExtend victim, BaseSimObject attacker, float damage)
    {
        if (!attacker.isActor()) return;
        
        var mainCultibook = victim.GetMainCultibook();
        if (mainCultibook == null) return;
        
        var method = mainCultibook.GetCultivateMethod();
        if (method == null) return;
        
        if (method.TriggerType != CultivateTriggerType.Passive) return;
        if (method.PassiveTriggerEvents == null || 
            !method.PassiveTriggerEvents.Contains(PassiveTriggerEvents.OnBeAttacked)) return;
        
        if (method.CanCultivate != null && !method.CanCultivate(victim)) return;
        
        // 计算修炼收益（基于受到的伤害）
        if (!victim.HasCultisys<Xian>()) return;
        ref var xian = ref victim.GetCultisys<Xian>();
        
        var efficiency = method.GetEfficiency?.Invoke(victim) ?? 1f;
        var attackerPower = !attacker.isRekt() 
            ? attacker.a.GetExtend().GetPowerLevel() + 1 
            : 1f;
        // 根据受到的伤害和攻击者强度计算灵力收益（受伤越重，收益越高）
        var wakanGain = damage * 0.05f * attackerPower * efficiency;
        
        var maxWakan = victim.Base.stats[BaseStatses.MaxWakan.id];
        wakanGain = Mathf.Min(wakanGain, maxWakan - xian.wakan);
        if (wakanGain > 0)
        {
            xian.wakan += wakanGain;
            
            method.OnSideEffect?.Invoke(victim, wakanGain);
        }
    }
}

