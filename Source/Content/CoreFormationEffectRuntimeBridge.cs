using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Progression;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>把角色战斗、施法和进阶事件接入核心形成效果运行时。</summary>
internal static class CoreFormationEffectRuntimeBridge
{
    /// <summary>注册全部事件桥、最终伤害阶段和主动形态属性贡献。</summary>
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
        ActorExtend.RegisterCachedStatsBuilder(ContributeActiveStats);
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
        if (!CoreFormationEffectResolver.TryGetFormation(owner, out _))
        {
            if (owner.E.HasComponent<CoreFormationEffectRuntime>())
                owner.E.RemoveComponent<CoreFormationEffectRuntime>();
            return;
        }
        using var effects = new ListPool<CoreFormationResolvedEffect>();
        CoreFormationEffectResolver.Resolve(owner, effects);
        if (!CoreFormationEffectResolver.Synchronize(owner, effects)) return;
        ref CoreFormationEffectRuntime runtime = ref owner.E.GetComponent<CoreFormationEffectRuntime>();
        for (var i = 0; i < runtime.entries.Length; i++)
        {
            ref CoreFormationEffectRuntimeEntry entry = ref runtime.entries[i];
            entry.cooldown_remaining = Mathf.Max(0f, entry.cooldown_remaining - deltaTime);
            entry.active_cooldown_remaining = Mathf.Max(0f, entry.active_cooldown_remaining - deltaTime);
            float previousActive = entry.active_remaining;
            entry.active_remaining = Mathf.Max(0f, entry.active_remaining - deltaTime);

            CoreFormationResolvedEffect effect = effects[i];
            if (previousActive > 0f && entry.active_remaining <= 0f)
            {
                CoreFormationEffectVisualSignals.Emit(effect, owner, owner.Base,
                    CoreFormationVisualChannel.End);
                if (entry.family_id == CoreFormationEffectFamilies.Body) owner.MarkCultiwayStatsDirty();
            }
            if ((effect.Definition.triggers & CoreFormationEffectTrigger.Tick) == 0 ||
                effect.Definition.Handle == null) continue;
            effect.Definition.Handle(effect, owner, ref entry, new CoreFormationEffectEvent
            {
                Kind = CoreFormationEffectEventKind.Tick,
                DeltaTime = deltaTime,
            });
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
        ref CoreFormationEffectRuntime runtime = ref owner.E.GetComponent<CoreFormationEffectRuntime>();
        for (var i = 0; i < effects.Count; i++)
        {
            CoreFormationResolvedEffect effect = effects[i];
            if ((effect.Definition.triggers & trigger) == 0 || effect.Definition.Handle == null) continue;
            if (finalStage.HasValue && effect.Definition.final_damage_stage != finalStage.Value) continue;
            int runtimeIndex = runtime.FindIndex(effect.Definition.family_id);
            if (runtimeIndex < 0) continue;
            effect.Definition.Handle(effect, owner, ref runtime.entries[runtimeIndex], evt);
        }
    }

    /// <summary>让真身主动形态向角色属性缓存贡献抗击退值。</summary>
    private static void ContributeActiveStats(ActorExtend owner, BaseStats stats)
    {
        if (!owner.E.TryGetComponent(out CoreFormationEffectRuntime runtime)) return;
        int index = runtime.FindIndex(CoreFormationEffectFamilies.Body);
        if (index < 0 || runtime.entries[index].rank < 2 || runtime.entries[index].active_remaining <= 0f) return;
        stats[S.knockback_reduction] += 8f;
    }

    /// <summary>角色死亡时结束所有主动形态，防止复用实体时残留状态。</summary>
    private static void InterruptActiveStates(ActorExtend owner)
    {
        if (!owner.E.TryGetComponent(out CoreFormationEffectRuntime runtime) || runtime.entries == null) return;
        bool bodyChanged = false;
        for (var i = 0; i < runtime.entries.Length; i++)
        {
            if (runtime.entries[i].active_remaining <= 0f) continue;
            bodyChanged |= runtime.entries[i].family_id == CoreFormationEffectFamilies.Body;
            runtime.entries[i].active_remaining = 0f;
        }
        owner.E.GetComponent<CoreFormationEffectRuntime>() = runtime;
        if (bodyChanged) owner.MarkCultiwayStatsDirty();
    }

    /// <summary>仙道进阶提交后立即同步形成效果，避免等待下一次逻辑扫描。</summary>
    private static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.Cultisys == Cultisyses.Xian) CoreFormationEffectResolver.Synchronize(evt.Actor);
    }
}
