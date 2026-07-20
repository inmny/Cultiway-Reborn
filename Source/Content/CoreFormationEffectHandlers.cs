using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>核心形成原子使用的被动事件和主动能力实现。</summary>
internal static class CoreFormationEffectHandlers
{
    private const float MinimumHealth = 1f;

    /// <summary>处理金行破甲和对已破甲目标的金行二次伤害。</summary>
    internal static void Iron(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, ref runtime)) return;
        bool alreadyBroken = CombatStatusEffects.HasStatus(target, StatusEffects.ArmorBreak, owner.Base);
        CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, 4f, owner.Base);
        if (alreadyBroken)
            CombatDamageEffects.DealReactionDamage(owner.Base, target, evt.Damage * 0.25f * effect.Potency,
                ElementComposition.Static.Iron);
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>处理木行中毒，并在击杀自身毒伤目标后恢复生命和灵气。</summary>
    internal static void Wood(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (!TryGetActor(evt.Other, out Actor target, evt.Kind == CoreFormationEffectEventKind.Kill)) return;
        if (evt.Kind == CoreFormationEffectEventKind.Kill)
        {
            if (!CombatStatusEffects.HasStatus(target, StatusEffects.Poison, owner.Base)) return;
            CombatResourceEffects.RestoreHealth(owner.Base, owner.Base.stats[S.health] * 0.025f * effect.Potency);
            CombatResourceEffects.RestoreWakan(owner.Base, 6f * effect.Potency);
            Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Trigger);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryProc(effect, ref runtime)) return;
        float totalDamage = evt.Damage * 0.35f * effect.Potency;
        CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Poison, 5f, totalDamage / 5f,
            ElementComposition.Static.Wood, owner.Base);
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>处理水行减速，并把再次命中的同源减速升级为短暂冻结。</summary>
    internal static void Water(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, ref runtime)) return;
        bool slowed = CombatStatusEffects.HasStatus(target, StatusEffects.Slow, owner.Base);
        CombatStatusEffects.ApplyStatus(target, slowed ? StatusEffects.Freeze : StatusEffects.Slow,
            slowed ? 0.75f : 3f, owner.Base);
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>处理火行灼烧，并在再次命中同源灼烧时引爆周围敌人。</summary>
    internal static void Fire(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, ref runtime)) return;
        if (CombatStatusEffects.HasStatus(target, StatusEffects.Burn, owner.Base))
        {
            CombatStatusEffects.RemoveStatus(target, StatusEffects.Burn, owner.Base);
            CombatDamageEffects.DealAreaReactionDamage(owner.Base, target.current_position, 2.5f,
                evt.Damage * 0.35f * effect.Potency, ElementComposition.Static.Fire);
        }
        else
        {
            float totalDamage = evt.Damage * 0.2f * effect.Potency;
            CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Burn, 4f, totalDamage / 4f,
                ElementComposition.Static.Fire, owner.Base);
        }
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>根据输出伤害积累土行护盾，并在最终伤害阶段消耗护盾。</summary>
    internal static void Earth(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (runtime.value <= 0f || evt.Damage <= 0f) return;
            float before = evt.Damage;
            CombatDamageEffects.AbsorbDamage(ref evt.Damage, ref runtime.value);
            if (evt.Damage < before) Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Hit);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryProc(effect, ref runtime)) return;
        float cap = owner.Base.stats[S.health] * 0.18f;
        runtime.value = Mathf.Min(cap, runtime.value + evt.Damage * 0.25f * effect.Potency);
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Charge);
    }

    /// <summary>处理阴行灵气汲取，并对无灵气目标施加衰弱和阴行二次伤害。</summary>
    internal static void Yin(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, ref runtime)) return;
        float drained = CombatResourceEffects.DrainWakan(target, 8f * effect.Potency);
        if (drained > 0f)
        {
            CombatResourceEffects.RestoreWakan(owner.Base, drained);
        }
        else
        {
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Weaken, 4f, owner.Base);
            CombatDamageEffects.DealReactionDamage(owner.Base, target, evt.Damage * 0.25f * effect.Potency,
                Element(ElementIndex.Neg));
        }
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>在角色自行支付的技能完成后按概率净化自身并恢复生命。</summary>
    internal static void Yang(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.SkillCastCompleted ||
            evt.FundingSource != SkillCastFundingSource.CasterResources || !TryProc(effect, ref runtime)) return;
        CombatStatusEffects.CleanseNegativeStatuses(owner.Base, 1);
        float maxHealth = Mathf.Max(MinimumHealth, owner.Base.stats[S.health]);
        float heal = Mathf.Min(maxHealth * 0.075f, maxHealth * 0.03f * effect.Potency);
        CombatResourceEffects.RestoreHealth(owner.Base, heal);
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Trigger);
    }

    /// <summary>对命中目标产生一种随机元素的混沌二次反应伤害。</summary>
    internal static void Chaos(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, ref runtime)) return;
        int element = Randy.randomInt(ElementIndex.Iron, ElementIndex.Entropy + 1);
        CombatDamageEffects.DealReactionDamage(owner.Base, target, evt.Damage * 0.3f * effect.Potency,
            Element(element));
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>跟踪连续承受的主元素，并逐步建立最高三成的对应伤害适应。</summary>
    internal static void Balanced(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming || evt.Damage <= 0f) return;
        int dominant = DominantElement(evt.Composition);
        if (runtime.phase == dominant + 1)
        {
            runtime.counter++;
        }
        else
        {
            runtime.phase = dominant + 1;
            runtime.counter = 1;
            runtime.value = 0f;
        }
        if (runtime.counter >= 2 && TryProc(effect, ref runtime))
        {
            runtime.value = Mathf.Min(0.3f, runtime.value + 0.05f * effect.Potency);
            Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Charge);
        }
        evt.Damage *= 1f - Mathf.Clamp(runtime.value, 0f, 0.3f);
    }

    /// <summary>在技能完成后积累一次凝元蓄力，并由下一次有效命中释放范围爆发。</summary>
    internal static void Condensed(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.SkillCastCompleted)
        {
            if (evt.FundingSource != SkillCastFundingSource.CasterResources || !TryProc(effect, ref runtime)) return;
            runtime.charges = 1;
            Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Charge);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction || runtime.charges <= 0 ||
            !TryGetActor(evt.Other, out Actor target)) return;
        runtime.charges = 0;
        CombatDamageEffects.DealAreaReactionDamage(owner.Base, target.current_position, 2f,
            evt.Damage * 0.35f * effect.Potency, evt.Composition);
        CombatResourceEffects.RestoreWakan(owner.Base, 8f * effect.Potency);
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>储存部分实际承伤，并在三秒未受击后用五秒逐步恢复。</summary>
    internal static void Vital(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.DamageTaken)
        {
            float cap = owner.Base.stats[S.health] * 0.3f;
            runtime.value = Mathf.Min(cap, runtime.value + evt.Damage * 0.35f * effect.Potency);
            runtime.auxiliary_timer = 3f;
            runtime.secondary_value = 0f;
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.Tick || runtime.value <= 0f) return;
        if (runtime.auxiliary_timer > 0f)
        {
            runtime.auxiliary_timer = Mathf.Max(0f, runtime.auxiliary_timer - evt.DeltaTime);
            return;
        }
        if (runtime.secondary_value <= 0f) runtime.secondary_value = 5f;
        float healed = Mathf.Min(runtime.value,
            runtime.value * evt.DeltaTime / Mathf.Max(evt.DeltaTime, runtime.secondary_value));
        runtime.value -= healed;
        runtime.secondary_value = Mathf.Max(0f, runtime.secondary_value - evt.DeltaTime);
        CombatResourceEffects.RestoreHealth(owner.Base, healed);
    }

    /// <summary>在自行支付技能后产生一次预付费单步回响，灵台形态激活时最多回响四次。</summary>
    internal static void Spiritual(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.SkillCastCompleted ||
            evt.FundingSource != SkillCastFundingSource.CasterResources) return;
        bool empowered = effect.Definition.rank >= 2 && runtime.active_remaining > 0f && runtime.charges > 0;
        if (!empowered && !TryProc(effect, ref runtime)) return;
        if (!EchoOneStep(owner, evt.SkillContainer, effect.Potency)) return;
        if (empowered) runtime.charges--;
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Trigger);
    }

    /// <summary>处理剑道二次剑气；剑胎激活时改为至多每秒一次的稳定追击。</summary>
    internal static void Sword(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target)) return;
        bool empowered = effect.Definition.rank >= 2 && runtime.active_remaining > 0f;
        if (empowered)
        {
            if (runtime.cooldown_remaining > 0f) return;
            runtime.cooldown_remaining = 1f;
        }
        else if (!TryProc(effect, ref runtime))
        {
            return;
        }
        CombatDamageEffects.DealReactionDamage(owner.Base, target, evt.Damage * 0.35f * effect.Potency,
            ElementComposition.Static.Iron);
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>在近战承伤后反击并推开攻击者。</summary>
    internal static void Body(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (effect.Definition.rank >= 2 && runtime.active_remaining > 0f)
                evt.Damage = Mathf.Min(evt.Damage, owner.Base.stats[S.health] * 0.15f);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageTaken || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor attacker) || !IsMelee(owner.Base, attacker, evt.AttackType) ||
            !TryProc(effect, ref runtime)) return;
        CombatDamageEffects.DealReactionDamage(owner.Base, attacker, evt.Damage * 0.4f * effect.Potency,
            ElementComposition.Static.Earth);
        CombatForceEffects.ApplyRadialForce(owner.Base, attacker, owner.Base.current_position, 2f * effect.Potency,
            false);
        Emit(effect, owner, attacker, CoreFormationVisualChannel.Hit);
    }

    /// <summary>把超过最大生命八成之一的单次命中化为幻影并短暂隐匿。</summary>
    internal static void Illusion(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming ||
            evt.Damage <= owner.Base.stats[S.health] * 0.08f || !TryProc(effect, ref runtime)) return;
        evt.Damage = 0f;
        CombatStatusEffects.ApplyStatus(owner.Base, StatusEffects.Concealed, 1.5f, owner.Base);
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Trigger);
    }

    /// <summary>在主灵气充盈时从环境积蓄灵气，并在主灵气不足时定速释放。</summary>
    internal static void Reservoir(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.Tick || !owner.HasCultisys<Xian>()) return;
        ref Xian xian = ref owner.GetCultisys<Xian>();
        float maxWakan = Mathf.Max(0f, owner.Base.stats[BaseStatses.MaxWakan.id]);
        if (maxWakan <= 0f) return;
        float cap = 80f * effect.Potency;
        if (xian.wakan >= maxWakan * 0.9f && runtime.value < cap && owner.Base.current_tile != null)
        {
            Vector2Int tile = owner.Base.current_tile.pos;
            float available = Mathf.Max(0f, WakanMap.I.map[tile.x, tile.y]);
            float taken = Mathf.Min(cap - runtime.value, available, 4f * effect.Potency * evt.DeltaTime);
            WakanMap.I.map[tile.x, tile.y] -= taken;
            runtime.value += taken;
        }
        else if (xian.wakan < maxWakan * 0.3f && runtime.value > 0f)
        {
            float released = Mathf.Min(runtime.value, 16f * effect.Potency * evt.DeltaTime,
                maxWakan - xian.wakan);
            runtime.value -= released;
            xian.wakan += released;
        }
    }

    /// <summary>从攻防事件积累龙威，满五层后震慑并推开周围敌人。</summary>
    internal static void Dragon(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt &&
            evt.Kind != CoreFormationEffectEventKind.DamageTaken || evt.IsReaction ||
            runtime.cooldown_remaining > 0f || !Roll(effect)) return;
        runtime.counter++;
        if (runtime.counter < 5) return;
        runtime.counter = 0;
        runtime.cooldown_remaining = effect.Definition.cooldown;
        float force = 2.5f * effect.Potency;
        CombatTargeting.ForEachHostile(owner.Base, owner.Base.current_position, 4f, target =>
        {
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, 0.6f, owner.Base);
            CombatForceEffects.ApplyRadialForce(owner.Base, target, owner.Base.current_position,
                force, false);
        });
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Trigger);
    }

    /// <summary>在致命伤阶段保留生命；混沌归墟覆盖灵胎并附带净化和隐匿。</summary>
    internal static void Survival(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming || runtime.cooldown_remaining > 0f ||
            evt.Damage < owner.Base.data.health || !Roll(effect)) return;
        float leaveRatio = effect.Definition.rank >= 2 ? 0.3f : 0.1f;
        float leaveHealth = Mathf.Max(MinimumHealth, Mathf.Ceil(owner.Base.stats[S.health] * leaveRatio));
        if (owner.Base.data.health < leaveHealth)
            CombatResourceEffects.RestoreHealth(owner.Base, leaveHealth - owner.Base.data.health);
        evt.Damage = Mathf.Max(0f, owner.Base.data.health - leaveHealth);
        runtime.cooldown_remaining = effect.Definition.cooldown;
        if (effect.Definition.rank >= 2)
        {
            CombatStatusEffects.CleanseNegativeStatuses(owner.Base);
            CombatStatusEffects.ApplyStatus(owner.Base, StatusEffects.Concealed, 2f, owner.Base);
        }
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Rebirth);
    }

    /// <summary>推进五相主动形态，并在当前相位上提供减伤与每秒一次的追加伤害。</summary>
    internal static void FivePhase(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        CoreFormationEffectEvent evt)
    {
        if (runtime.active_remaining <= 0f) return;
        if (evt.Kind == CoreFormationEffectEventKind.Tick)
        {
            runtime.auxiliary_timer -= evt.DeltaTime;
            runtime.secondary_value = Mathf.Max(0f, runtime.secondary_value - evt.DeltaTime);
            if (runtime.auxiliary_timer <= 0f)
            {
                runtime.phase = (runtime.phase + 1) % 5;
                runtime.auxiliary_timer += 2f;
            }
            return;
        }
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (DominantElement(evt.Composition) == runtime.phase) evt.Damage *= 0.75f;
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            runtime.secondary_value > 0f || !TryGetActor(evt.Other, out Actor target)) return;
        runtime.secondary_value = 1f;
        CombatDamageEffects.DealReactionDamage(owner.Base, target, evt.Damage * 0.25f * effect.Potency,
            Element(runtime.phase));
        Emit(effect, owner, target, CoreFormationVisualChannel.Hit);
    }

    /// <summary>判断一个以当前战斗目标为环境依据的主动形态是否值得准备。</summary>
    internal static bool PrepareCombatBuff(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        in CoreFormationEffectRuntimeEntry runtime,
        BaseSimObject target)
    {
        return !target.isRekt() && owner.Base.canAttackTarget(target);
    }

    /// <summary>激活剑胎持续形态。</summary>
    internal static bool ActivateSwordEmbryo(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        runtime.active_remaining = effect.Definition.active.duration;
        runtime.cooldown_remaining = 0f;
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Activate);
        return true;
    }

    /// <summary>释放龙相震击，对范围敌人造成伤害、眩晕和击退。</summary>
    internal static bool ActivateDragonAspect(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        Vector2 center = ResolveCenter(owner.Base, target);
        float potency = effect.Potency;
        float damage = owner.Base.stats[S.damage] * 1.2f * potency;
        float radius = effect.Definition.active.radius;
        CombatTargeting.ForEachHostile(owner.Base, center, radius, victim =>
        {
            CombatDamageEffects.DealReactionDamage(owner.Base, victim, damage, ElementComposition.Static.Earth);
            CombatStatusEffects.ApplyStatus(victim, StatusEffects.Daze, 0.8f, owner.Base);
            CombatForceEffects.ApplyRadialForce(owner.Base, victim, center, 3f * potency, false);
        });
        Emit(effect, owner, null, CoreFormationVisualChannel.Activate, center);
        return true;
    }

    /// <summary>激活灵台形态并补充最多四次单步回响次数。</summary>
    internal static bool ActivateSpiritPlatform(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        runtime.active_remaining = effect.Definition.active.duration;
        runtime.charges = 4;
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Activate);
        return true;
    }

    /// <summary>激活真身形态，由最终伤害阶段施加单次伤害上限并由属性缓存提供抗击退。</summary>
    internal static bool ActivatePrimalBody(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        runtime.active_remaining = effect.Definition.active.duration;
        owner.MarkCultiwayStatsDirty();
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Activate);
        return true;
    }

    /// <summary>激活五相轮转并从金相开始按两秒一相推进。</summary>
    internal static bool ActivateFivePhase(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        runtime.active_remaining = effect.Definition.active.duration;
        runtime.phase = 0;
        runtime.auxiliary_timer = 2f;
        runtime.secondary_value = 0f;
        Emit(effect, owner, owner.Base, CoreFormationVisualChannel.Activate);
        return true;
    }

    /// <summary>释放纯阳净域，净化治疗友军并灼烧敌军。</summary>
    internal static bool ActivatePureYang(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        Vector2 center = ResolveCenter(owner.Base, target);
        float radius = effect.Definition.active.radius;
        float potency = effect.Potency;
        CombatTargeting.ForEachFriendly(owner.Base, center, radius, ally =>
        {
            CombatStatusEffects.CleanseNegativeStatuses(ally, 2);
            CombatResourceEffects.RestoreHealth(ally, ally.stats[S.health] * 0.05f * potency);
        });
        float burnPerSecond = owner.Base.stats[S.damage] * 0.05f * potency;
        CombatTargeting.ForEachHostile(owner.Base, center, radius, enemy =>
            CombatStatusEffects.ApplyTickingStatus(enemy, StatusEffects.Burn, 4f, burnPerSecond,
                Element(ElementIndex.Pos), owner.Base));
        Emit(effect, owner, null, CoreFormationVisualChannel.Activate, center);
        return true;
    }

    /// <summary>释放玄阴寒域，冻结、沉默并汲取范围内敌人的灵气。</summary>
    internal static bool ActivateMysteriousYin(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        ref CoreFormationEffectRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        Vector2 center = ResolveCenter(owner.Base, target);
        float radius = effect.Definition.active.radius;
        float drain = 12f * effect.Potency;
        CombatTargeting.ForEachHostile(owner.Base, center, radius, enemy =>
        {
            CombatStatusEffects.ApplyStatus(enemy, StatusEffects.Freeze, 1f, owner.Base);
            CombatStatusEffects.ApplyStatus(enemy, StatusEffects.Silence, 3f, owner.Base);
            float drained = CombatResourceEffects.DrainWakan(enemy, drain);
            CombatResourceEffects.RestoreWakan(owner.Base, drained);
        });
        Emit(effect, owner, null, CoreFormationVisualChannel.Activate, center);
        return true;
    }

    /// <summary>判断概率和普通内部冷却，并在成功时写入定义冷却。</summary>
    private static bool TryProc(
        in CoreFormationResolvedEffect effect,
        ref CoreFormationEffectRuntimeEntry runtime)
    {
        if (runtime.cooldown_remaining > 0f || !Roll(effect)) return false;
        runtime.cooldown_remaining = effect.Definition.cooldown;
        return true;
    }

    /// <summary>只执行效果概率判定，不修改任何运行时计时器。</summary>
    private static bool Roll(in CoreFormationResolvedEffect effect)
    {
        return effect.ProcChance >= 1f || Randy.randomChance(effect.ProcChance);
    }

    /// <summary>安全地把事件关联对象解析为仍然有效的角色。</summary>
    private static bool TryGetActor(BaseSimObject value, out Actor actor, bool allowRekt = false)
    {
        if (value != null && value.isActor() && (allowRekt || !value.isRekt()))
        {
            actor = value.a;
            return true;
        }
        actor = null;
        return false;
    }

    /// <summary>返回伤害构成中权重最高的元素索引。</summary>
    private static int DominantElement(ElementComposition composition)
    {
        int best = ElementIndex.Iron;
        float value = composition[best];
        for (var i = ElementIndex.Wood; i <= ElementIndex.Entropy; i++)
        {
            if (composition[i] <= value) continue;
            best = i;
            value = composition[i];
        }
        return best;
    }

    /// <summary>构造单一元素组成。</summary>
    private static ElementComposition Element(int index)
    {
        var composition = new ElementComposition();
        composition[index] = 1f;
        return composition;
    }

    /// <summary>判断一次武器伤害是否来自近身攻击者。</summary>
    private static bool IsMelee(Actor owner, Actor attacker, AttackType attackType)
    {
        if (attackType != AttackType.Weapon) return false;
        float range = 2.5f + owner.stats[S.size] + attacker.stats[S.size];
        return Toolbox.SquaredDistVec2Float(owner.current_position, attacker.current_position) <= range * range;
    }

    /// <summary>让技能容器以预付费方式对当前目标或自身位置回响一个施放步骤。</summary>
    private static bool EchoOneStep(ActorExtend owner, Entity skill, float strength)
    {
        if (skill.IsNull || !skill.IsAvailable() || !skill.HasComponent<SkillContainer>()) return false;
        SkillCastPlan plan;
        BaseSimObject target = owner.Base.has_attack_target ? owner.Base.attack_target : null;
        if (!target.isRekt())
            plan = SkillCastPlanner.CreatePlan(owner, skill, target, 1);
        else
            plan = SkillCastPlanner.CreatePointPlan(owner, skill, owner.Base.GetSimPos(), 1);
        if (plan.Steps.Count == 0) return false;
        return ModClass.I.SkillV3.StartSkillSequence(owner, skill, plan, strength,
            funding_source: SkillCastFundingSource.Prepaid, attack_kingdom: owner.Base.kingdom);
    }

    /// <summary>根据主动目标模式解析实际作用中心。</summary>
    private static Vector2 ResolveCenter(Actor owner, in ActiveAbilityTarget target)
    {
        if (!target.Object.isRekt()) return target.Object.GetSimPos();
        if (target.Position != Vector3.zero) return target.Position;
        return owner.current_position;
    }

    /// <summary>把一次成功机制结算提交给独立表现队列。</summary>
    private static void Emit(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        Actor target,
        CoreFormationVisualChannel channel,
        Vector2? position = null)
    {
        Vector3? worldPosition = position.HasValue
            ? new Vector3(position.Value.x, position.Value.y, 0f)
            : null;
        CoreFormationEffectVisualSignals.Emit(effect, owner, target, channel, worldPosition);
    }
}
