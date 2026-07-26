using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Progression;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>把角色战斗、施法和进阶事件接入形成来源授予 Skill。</summary>
internal static class CoreFormationSkillBridge
{
    /// <summary>注册全部事件桥和最终伤害阶段。</summary>
    internal static void Init()
    {
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Avoidance, ApplyAvoidance);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Adaptation, ApplyAdaptation);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Shield, ApplyShield);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Cap, ApplyCap);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Survival, ApplySurvival);
        ActorExtend.RegisterActionOnDamageResolved(DamageResolved);
        ActorExtend.RegisterActionOnKill(Killed);
        ActorExtend.RegisterActionOnSkillCastCompleted(SkillCastCompleted);
        ActorExtend.RegisterActionOnDeath(InterruptActiveStates);
        ProgressionLifecycle.RegisterCommitted(OnProgressionCommitted);
    }

    /// <summary>执行闪避阶段的核心形成最终伤害规则。</summary>
    private static void ApplyAvoidance(
        ActorExtend self,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        DispatchFinal(self, attacker, composition, attackType, FinalDamageStage.Avoidance, ref damage);
    }

    /// <summary>执行适应阶段的核心形成最终伤害规则。</summary>
    private static void ApplyAdaptation(
        ActorExtend self,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        DispatchFinal(self, attacker, composition, attackType, FinalDamageStage.Adaptation, ref damage);
    }

    /// <summary>执行护盾阶段的核心形成最终伤害规则。</summary>
    private static void ApplyShield(
        ActorExtend self,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        DispatchFinal(self, attacker, composition, attackType, FinalDamageStage.Shield, ref damage);
    }

    /// <summary>执行伤害上限阶段的核心形成最终伤害规则。</summary>
    private static void ApplyCap(
        ActorExtend self,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        DispatchFinal(self, attacker, composition, attackType, FinalDamageStage.Cap, ref damage);
    }

    /// <summary>执行致命伤保护阶段的核心形成最终伤害规则。</summary>
    private static void ApplySurvival(
        ActorExtend self,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        DispatchFinal(self, attacker, composition, attackType, FinalDamageStage.Survival, ref damage);
    }

    /// <summary>按固定阶段派发最终伤害事件，并把处理后的伤害写回原始结算。</summary>
    private static void DispatchFinal(
        ActorExtend owner,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        FinalDamageStage stage,
        ref float damage)
    {
        var evt = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.FinalDamageIncoming,
            Other = attacker,
            Damage = damage,
            Composition = composition,
            AttackType = attackType,
            IsReaction = CombatDamageEffects.IsResolvingReaction,
        };
        Dispatch(owner, CoreFormationEffectTrigger.FinalDamageIncoming, evt, stage);
        damage = Mathf.Max(0f, evt.Damage);
    }

    /// <summary>在最终伤害确定后分别通知受击者和攻击者。</summary>
    private static void DamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        float actualDamage = Mathf.Min(Mathf.Max(0f, damage), Mathf.Max(0f, target.Base.data.health));
        var taken = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.DamageTaken,
            Other = attacker,
            Damage = actualDamage,
            Composition = composition,
            AttackType = attackType,
            IsReaction = CombatDamageEffects.IsResolvingReaction,
        };
        Dispatch(target, CoreFormationEffectTrigger.DamageTaken, taken);

        if (attacker.isRekt() || !attacker.isActor() || attacker.a == target.Base) return;
        var dealt = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.DamageDealt,
            Other = target.Base,
            Damage = actualDamage,
            Composition = composition,
            AttackType = attackType,
            IsReaction = CombatDamageEffects.IsResolvingReaction,
        };
        Dispatch(attacker.a.GetExtend(), CoreFormationEffectTrigger.DamageDealt, dealt);
    }

    /// <summary>把击杀事件派发给击杀者的形成效果。</summary>
    private static void Killed(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        var evt = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.Kill,
            Other = victim,
            IsReaction = CombatDamageEffects.IsResolvingReaction,
        };
        Dispatch(killer, CoreFormationEffectTrigger.Kill, evt);
    }

    /// <summary>把技能完成事件连同出资方式派发给施法者的形成效果。</summary>
    private static void SkillCastCompleted(
        ActorExtend caster,
        Entity skillContainer,
        int emittedCount,
        SkillCastFundingSource fundingSource)
    {
        var evt = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.SkillCastCompleted,
            SkillContainer = skillContainer,
            EmittedCount = emittedCount,
            FundingSource = fundingSource,
        };
        Dispatch(caster, CoreFormationEffectTrigger.SkillCastCompleted, evt);
    }

    /// <summary>推进一个角色全部形成效果的计时器、资源池和持续形态。</summary>
    internal static void Advance(ActorExtend owner, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        if (!CoreFormationEffectResolver.TryGetFormation(
                owner,
                out CoreFormationEffectResolver.FormationSource source))
        {
            CoreFormationEffectResolver.Synchronize(owner);
            return;
        }
        using var effects = new ListPool<CoreFormationResolvedEffect>();
        CoreFormationEffectResolver.Resolve(source, effects);
        if (!CoreFormationEffectResolver.Synchronize(owner, source, effects)) return;
        var tickEvent = new CoreFormationEffectEvent
        {
            Kind = CoreFormationEffectEventKind.Tick,
            DeltaTime = deltaTime,
        };
        for (var i = 0; i < effects.Count; i++)
        {
            CoreFormationResolvedEffect effect = effects[i];
            if ((effect.Definition.triggers & CoreFormationEffectTrigger.Tick) == 0) continue;
            effect.Definition.Handle?.Invoke(effect, owner, tickEvent);
        }
    }

    /// <summary>统一解析、同步并派发一个非最终伤害事件。</summary>
    private static void Dispatch(
        ActorExtend owner,
        CoreFormationEffectTrigger trigger,
        CoreFormationEffectEvent evt,
        FinalDamageStage? finalStage = null)
    {
        if (owner?.Base == null || owner.Base.isRekt()) return;
        using var effects = new ListPool<CoreFormationResolvedEffect>();
        CoreFormationEffectResolver.Resolve(owner, effects);
        if (!CoreFormationEffectResolver.Synchronize(owner, effects)) return;
        for (var i = 0; i < effects.Count; i++)
        {
            CoreFormationResolvedEffect effect = effects[i];
            if ((effect.Definition.triggers & trigger) == 0) continue;
            if (finalStage.HasValue && effect.Definition.final_damage_stage != finalStage.Value) continue;
            effect.Definition.Handle?.Invoke(effect, owner, evt);
        }
    }

    /// <summary>角色死亡时清除自身形成状态和形成技能冷却。</summary>
    private static void InterruptActiveStates(ActorExtend owner)
    {
        CoreFormationEffectResolver.ClearGrantedState(owner);
    }

    /// <summary>仙道进阶提交后立即同步形成效果，避免等待下一次逻辑扫描。</summary>
    private static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.Cultisys == Cultisyses.Xian) CoreFormationEffectResolver.Synchronize(evt.Actor);
    }
}
