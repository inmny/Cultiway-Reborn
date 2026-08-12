using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 骑士战斗相关钩子：
/// - 通过战斗积累斗气：普通攻击、杀敌（按对手战力缩放、弱敌不给）与被击中。
/// - 闪避：受击前按 KnightEvasion 几率判定，命中则清零伤害并播放闪避特效。
/// </summary>
public static class KnightCombatTriggers
{
    [Hotfixable]
    public static void Init()
    {
        ActorExtend.RegisterActionOnKill(OnKillTrigger);
        ActorExtend.RegisterActionOnBeAttacked(OnBeAttackedTrigger);
        ActorExtend.RegisterActionOnDamageResolved(OnDamageResolved);
        ActorExtend.RegisterActionBeforeBeAttacked(KnightEvasionBeforeBeAttacked);
    }

    /// <summary>普通攻击造成正伤害后恢复斗气，但不能借此超过斗气上限的 60%。</summary>
    [Hotfixable]
    private static void OnDamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        if (attackType != AttackType.Weapon || DamageResolutionContext.IsReaction ||
            DamageResolutionContext.CurrentSourceScopeId != 0 || attacker?.isActor() != true ||
            attacker == target.Base || !attacker.a.TryGetExtend(out ActorExtend source) ||
            !source.HasCultisys<Knight>()) return;

        ref Knight knight = ref source.GetCultisys<Knight>();
        float maxVigor = source.Base.stats[BaseStatses.MaxVigor.id];
        float recoveryLimit = maxVigor * KnightSetting.BasicAttackVigorRecoveryLimitRatio;
        if (knight.vigor >= recoveryLimit) return;

        float targetPower = target.GetPowerLevel() + 1f;
        float gain = damage * KnightSetting.BasicAttackVigorGainRatio * targetPower;
        knight.vigor = Mathf.Min(knight.vigor + gain, recoveryLimit);
    }

    /// <summary>杀敌触发：按对手战力缩放给斗气，弱敌不给，只计敌对击杀。</summary>
    [Hotfixable]
    private static void OnKillTrigger(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        if (!killer.HasCultisys<Knight>()) return;
        // 按原版外交关系判断敌对目标；和平或同盟王国的击杀不计入斗气。
        if (killer.Base.kingdom?.isEnemy(victimKingdom) == false) return;

        ref var knight = ref killer.GetCultisys<Knight>();
        var maxVigor = killer.Base.stats[BaseStatses.MaxVigor.id];
        if (maxVigor <= 0f) return;

        var killerPower = killer.GetPowerLevel() + 1;
        var victimPower = victim.GetExtend().GetPowerLevel() + 1;

        // 弱敌不给斗气（防刷弱怪）
        if (victimPower < killerPower * KnightSetting.WeakFoePowerRatio) return;

        var ratio = Mathf.Min(victimPower / killerPower, 2f);
        var gain = maxVigor * KnightSetting.KillVigorGainRatio * ratio;
        gain = Mathf.Min(gain, maxVigor - knight.vigor);
        if (gain > 0f) knight.vigor += gain;
    }

    /// <summary>被击中触发：按伤害与攻击者战力给少量斗气（血战成长）。</summary>
    [Hotfixable]
    private static void OnBeAttackedTrigger(ActorExtend victim, BaseSimObject attacker, float damage)
    {
        if (!victim.HasCultisys<Knight>()) return;

        ref var knight = ref victim.GetCultisys<Knight>();
        var maxVigor = victim.Base.stats[BaseStatses.MaxVigor.id];
        if (maxVigor <= 0f) return;

        var attackerPower = !attacker.isRekt() && attacker.isActor()
            ? attacker.a.GetExtend().GetPowerLevel() + 1
            : 1f;

        var gain = damage * KnightSetting.BeAttackedVigorGainRatio * attackerPower;
        gain = Mathf.Min(gain, maxVigor - knight.vigor);
        if (gain > 0f) knight.vigor += gain;
    }

    /// <summary>受击前判定闪避：按 KnightEvasion 几率清零本次伤害并播放闪避特效。</summary>
    [Hotfixable]
    private static void KnightEvasionBeforeBeAttacked(
        ActorExtend self, BaseSimObject attacker,
        ref ElementComposition damageComposition, ref AttackType attackType,
        ref float damage, ref bool ignoreDamageReduction)
    {
        if (!self.HasCultisys<Knight>()) return;
        var evasion = self.Base.stats[BaseStatses.KnightEvasion.id];
        if (evasion <= 0f || !Randy.randomChance(evasion)) return;

        damage = 0f;
        if (self.Base.is_visible)
        {
            var fx = EffectsLibrary.spawnAt("fx_dodge", self.Base.current_position, self.Base.actor_scale);
            fx?.attachTo(self.Base.a);
        }
    }
}
