using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3;
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
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage);
    }

    /// <summary>处理木行中毒，并在击杀自身毒伤目标后恢复生命和灵气。</summary>
    internal static void Wood(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (!TryGetActor(evt.Other, out Actor target, evt.Kind == CoreFormationEffectEventKind.Kill)) return;
        if (evt.Kind == CoreFormationEffectEventKind.Kill)
        {
            if (!CombatStatusEffects.HasStatus(target, StatusEffects.Poison, owner.Base)) return;
            Trigger(effect, owner, CoreFormationSkills.WoodLifeReturn, owner.Base, SkillContext.DefaultStrength);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage);
    }

    /// <summary>处理水行减速，并把再次命中的同源减速升级为短暂冻结。</summary>
    internal static void Water(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage);
    }

    /// <summary>处理火行灼烧，并在再次命中同源灼烧时引爆周围敌人。</summary>
    internal static void Fire(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, owner)) return;
        Entity skill = CombatStatusEffects.HasStatus(target, StatusEffects.Burn, owner.Base)
            ? CoreFormationSkills.FireEmberBurst
            : CoreFormationSkills.FireBrand;
        Trigger(effect, owner, skill, target, evt.Damage);
    }

    /// <summary>根据输出伤害积累土行护盾，并在最终伤害阶段消耗护盾。</summary>
    internal static void Earth(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (evt.Damage <= 0f ||
                !CoreFormationStateService.TryGet(owner, effect, out Entity status, out CoreFormationEffectState state) ||
                state.value <= 0f) return;
            float before = evt.Damage;
            CombatDamageEffects.AbsorbDamage(ref evt.Damage, ref state.value);
            if (state.value <= 0f)
                CoreFormationStateService.Remove(owner, effect);
            else
                CoreFormationStateService.Save(status, state);
            if (evt.Damage < before)
                Trigger(effect, owner, CoreFormationSkills.EarthWardImpact, owner.Base, before - evt.Damage);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction || evt.Damage <= 0f ||
            !TryProc(effect, owner)) return;
        float cap = owner.Base.stats[S.health] * 0.18f;
        Entity ward = CoreFormationStateService.GetOrCreate(owner, effect, out CoreFormationEffectState wardState);
        if (ward.IsNull) return;
        wardState.value = Mathf.Min(cap, wardState.value + evt.Damage * 0.25f * effect.Potency);
        CoreFormationStateService.Save(ward, wardState);
        Trigger(effect, owner, CoreFormationSkills.EarthWard, owner.Base, evt.Damage);
    }

    /// <summary>处理阴行灵气汲取，并对无灵气目标施加衰弱和阴行二次伤害。</summary>
    internal static void Yin(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage);
    }

    /// <summary>在角色自行支付的技能完成后按概率净化自身并恢复生命。</summary>
    internal static void Yang(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.SkillCastCompleted ||
            evt.FundingSource != SkillCastFundingSource.CasterResources || !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
    }

    /// <summary>对命中目标产生一种随机元素的混沌二次反应伤害。</summary>
    internal static void Chaos(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) || !TryProc(effect, owner)) return;
        int element = Randy.randomInt(ElementIndex.Iron, ElementIndex.Entropy + 1);
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage, Element(element));
    }

    /// <summary>跟踪连续承受的主元素，并逐步建立最高三成的对应伤害适应。</summary>
    internal static void Balanced(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming || evt.Damage <= 0f) return;
        Entity status = CoreFormationStateService.GetOrCreate(
            owner,
            effect,
            out CoreFormationEffectState state);
        if (status.IsNull) return;
        int dominant = DominantElement(evt.Composition);
        if (state.phase == dominant + 1)
        {
            state.counter++;
        }
        else
        {
            state.phase = dominant + 1;
            state.counter = 1;
            state.value = 0f;
        }
        if (state.counter >= 2 && TryProc(effect, owner))
        {
            state.value = Mathf.Min(0.3f, state.value + 0.05f * effect.Potency);
            Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
        }
        evt.Damage *= 1f - Mathf.Clamp(state.value, 0f, 0.3f);
        CoreFormationStateService.Save(status, state);
    }

    /// <summary>在技能完成后积累一次凝元蓄力，并由下一次有效命中释放范围爆发。</summary>
    internal static void Condensed(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.SkillCastCompleted)
        {
            if (evt.FundingSource != SkillCastFundingSource.CasterResources || !TryProc(effect, owner)) return;
            Entity charge = CoreFormationStateService.GetOrCreate(
                owner,
                effect,
                out CoreFormationEffectState chargeState);
            if (charge.IsNull) return;
            chargeState.charges = 1;
            CoreFormationStateService.Save(charge, chargeState);
            Trigger(effect, owner, CoreFormationSkills.ReservoirOrb, owner.Base, SkillContext.DefaultStrength);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target) ||
            !CoreFormationStateService.TryGet(owner, effect, out _, out CoreFormationEffectState state) ||
            state.charges <= 0) return;
        if (!Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage, evt.Composition)) return;
        CoreFormationStateService.Remove(owner, effect);
        CombatResourceEffects.RestoreWakan(owner.Base, 8f * effect.Potency);
    }

    /// <summary>储存部分实际承伤，并在三秒未受击后用五秒逐步恢复。</summary>
    internal static void Vital(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.DamageTaken)
        {
            if (evt.Damage <= 0f) return;
            Entity reserve = CoreFormationStateService.GetOrCreate(
                owner,
                effect,
                out CoreFormationEffectState state);
            if (reserve.IsNull) return;
            float cap = owner.Base.stats[S.health] * 0.3f;
            state.value = Mathf.Min(cap, state.value + evt.Damage * 0.35f * effect.Potency);
            state.auxiliary_timer = 3f;
            state.secondary_value = 0f;
            CoreFormationStateService.Save(reserve, state);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.Tick ||
            !CoreFormationStateService.TryGet(
                owner,
                effect,
                out Entity status,
                out CoreFormationEffectState stored) ||
            stored.value <= 0f) return;
        if (stored.auxiliary_timer > 0f)
        {
            stored.auxiliary_timer = Mathf.Max(0f, stored.auxiliary_timer - evt.DeltaTime);
            CoreFormationStateService.Save(status, stored);
            return;
        }
        if (stored.secondary_value <= 0f) stored.secondary_value = 5f;
        float healed = Mathf.Min(stored.value,
            stored.value * evt.DeltaTime / Mathf.Max(evt.DeltaTime, stored.secondary_value));
        stored.value -= healed;
        stored.secondary_value = Mathf.Max(0f, stored.secondary_value - evt.DeltaTime);
        CombatResourceEffects.RestoreHealth(owner.Base, healed);
        if (stored.value <= 0f)
            CoreFormationStateService.Remove(owner, effect);
        else
            CoreFormationStateService.Save(status, stored);
    }

    /// <summary>在自行支付技能后产生一次预付费单步回响，灵台形态激活时最多回响四次。</summary>
    internal static void Spiritual(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.SkillCastCompleted ||
            evt.FundingSource != SkillCastFundingSource.CasterResources) return;
        Entity status = default;
        CoreFormationEffectState state = default;
        bool empowered = effect.Definition.rank >= 2 &&
                         CoreFormationStateService.TryGet(owner, effect, out status, out state) &&
                         state.charges > 0;
        if (!empowered && !TryProc(effect, owner)) return;
        if (!EchoOneStep(owner, evt.SkillContainer, effect.Potency)) return;
        if (empowered)
        {
            state.charges--;
            CoreFormationStateService.Save(status, state);
        }
        Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
    }

    /// <summary>处理剑道二次剑气；剑胎激活时改为至多每秒一次的稳定追击。</summary>
    internal static void Sword(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor target)) return;
        bool empowered = effect.Definition.rank >= 2 &&
                         CoreFormationStateService.TryGet(owner, effect, out _, out _);
        if (empowered)
        {
            if (!SkillCooldownService.IsReady(owner, effect.Definition.CooldownSkill)) return;
            SkillCooldownService.Start(owner, effect.Definition.CooldownSkill, 1f);
        }
        else if (!TryProc(effect, owner))
        {
            return;
        }
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage);
    }

    /// <summary>在近战承伤后反击并推开攻击者。</summary>
    internal static void Body(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (effect.Definition.rank >= 2 &&
                CoreFormationStateService.TryGet(owner, effect, out _, out _))
                evt.Damage = Mathf.Min(evt.Damage, owner.Base.stats[S.health] * 0.15f);
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageTaken || evt.IsReaction ||
            !TryGetActor(evt.Other, out Actor attacker) || !IsMelee(owner.Base, attacker, evt.AttackType) ||
            !TryProc(effect, owner)) return;
        Trigger(effect, owner, effect.Definition.TriggerSkill, attacker, evt.Damage);
    }

    /// <summary>把超过最大生命八成之一的单次命中化为幻影并短暂隐匿。</summary>
    internal static void Illusion(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming ||
            evt.Damage <= owner.Base.stats[S.health] * 0.08f || !TryProc(effect, owner)) return;
        evt.Damage = 0f;
        CombatStatusEffects.ApplyStatus(owner.Base, StatusEffects.Concealed, 1.5f, owner.Base);
        Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
    }

    /// <summary>在主灵气充盈时从环境积蓄灵气，并在主灵气不足时定速释放。</summary>
    internal static void Reservoir(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.Tick || !owner.HasCultisys<Xian>()) return;
        ref Xian xian = ref owner.GetCultisys<Xian>();
        float maxWakan = Mathf.Max(0f, owner.Base.stats[BaseStatses.MaxWakan.id]);
        if (maxWakan <= 0f) return;
        float cap = 80f * effect.Potency;
        bool hasReserve = CoreFormationStateService.TryGet(
            owner,
            effect,
            out Entity status,
            out CoreFormationEffectState state);
        if (xian.wakan >= maxWakan * 0.9f && (!hasReserve || state.value < cap) &&
            owner.Base.current_tile != null)
        {
            Vector2Int tile = owner.Base.current_tile.pos;
            float available = Mathf.Max(0f, WakanMap.I.map[tile.x, tile.y]);
            float currentReserve = hasReserve ? state.value : 0f;
            float taken = Mathf.Min(cap - currentReserve, available, 4f * effect.Potency * evt.DeltaTime);
            if (taken <= 0f) return;
            if (!hasReserve)
            {
                status = CoreFormationStateService.GetOrCreate(owner, effect, out state);
                if (status.IsNull) return;
            }
            WakanMap.I.map[tile.x, tile.y] -= taken;
            state.value += taken;
            CoreFormationStateService.Save(status, state);
        }
        else if (xian.wakan < maxWakan * 0.3f && hasReserve && state.value > 0f)
        {
            float released = Mathf.Min(state.value, 16f * effect.Potency * evt.DeltaTime,
                maxWakan - xian.wakan);
            state.value -= released;
            xian.wakan += released;
            if (state.value <= 0f)
                CoreFormationStateService.Remove(owner, effect);
            else
                CoreFormationStateService.Save(status, state);
        }
    }

    /// <summary>从攻防事件积累龙威，满五层后震慑并推开周围敌人。</summary>
    internal static void Dragon(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt &&
            evt.Kind != CoreFormationEffectEventKind.DamageTaken || evt.IsReaction ||
            !SkillCooldownService.IsReady(owner, effect.Definition.CooldownSkill) || !Roll(effect)) return;
        Entity status = CoreFormationStateService.GetOrCreate(
            owner,
            effect,
            out CoreFormationEffectState state);
        if (status.IsNull) return;
        state.counter++;
        if (state.counter < 5)
        {
            CoreFormationStateService.Save(status, state);
            return;
        }
        CoreFormationStateService.Remove(owner, effect);
        SkillCooldownService.Start(owner, effect.Definition.CooldownSkill, effect.Definition.cooldown);
        Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
    }

    /// <summary>在致命伤阶段保留生命；混沌归墟覆盖灵胎并附带净化和隐匿。</summary>
    internal static void Survival(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (evt.Kind != CoreFormationEffectEventKind.FinalDamageIncoming ||
            !SkillCooldownService.IsReady(owner, effect.Definition.CooldownSkill) ||
            evt.Damage < owner.Base.data.health || !Roll(effect)) return;
        float leaveRatio = effect.Definition.rank >= 2 ? 0.3f : 0.1f;
        float leaveHealth = Mathf.Max(MinimumHealth, Mathf.Ceil(owner.Base.stats[S.health] * leaveRatio));
        if (owner.Base.data.health < leaveHealth)
            CombatResourceEffects.RestoreHealth(owner.Base, leaveHealth - owner.Base.data.health);
        evt.Damage = Mathf.Max(0f, owner.Base.data.health - leaveHealth);
        SkillCooldownService.Start(owner, effect.Definition.CooldownSkill, effect.Definition.cooldown);
        if (effect.Definition.rank >= 2)
        {
            CombatStatusEffects.CleanseNegativeStatuses(owner.Base);
            CombatStatusEffects.ApplyStatus(owner.Base, StatusEffects.Concealed, 2f, owner.Base);
        }
        Trigger(effect, owner, effect.Definition.TriggerSkill, owner.Base, SkillContext.DefaultStrength);
    }

    /// <summary>推进五相主动形态，并在当前相位上提供减伤与每秒一次的追加伤害。</summary>
    internal static void FivePhase(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        CoreFormationEffectEvent evt)
    {
        if (!CoreFormationStateService.TryGet(
                owner,
                effect,
                out Entity status,
                out CoreFormationEffectState state)) return;
        if (evt.Kind == CoreFormationEffectEventKind.Tick)
        {
            state.auxiliary_timer -= evt.DeltaTime;
            state.secondary_value = Mathf.Max(0f, state.secondary_value - evt.DeltaTime);
            if (state.auxiliary_timer <= 0f)
            {
                state.phase = (state.phase + 1) % 5;
                state.auxiliary_timer += 2f;
            }
            CoreFormationStateService.Save(status, state);
            return;
        }
        if (evt.Kind == CoreFormationEffectEventKind.FinalDamageIncoming)
        {
            if (DominantElement(evt.Composition) == state.phase) evt.Damage *= 0.75f;
            return;
        }
        if (evt.Kind != CoreFormationEffectEventKind.DamageDealt || evt.IsReaction ||
            state.secondary_value > 0f || !TryGetActor(evt.Other, out Actor target)) return;
        state.secondary_value = 1f;
        CoreFormationStateService.Save(status, state);
        Trigger(effect, owner, effect.Definition.TriggerSkill, target, evt.Damage, Element(state.phase));
    }

    /// <summary>判断一个以当前战斗目标为环境依据的主动形态是否值得准备。</summary>
    internal static bool PrepareCombatBuff(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        BaseSimObject target)
    {
        return !target.isRekt() && owner.Base.canAttackTarget(target);
    }

    /// <summary>判断概率和普通内部冷却，并在成功时写入定义冷却。</summary>
    private static bool TryProc(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner)
    {
        if (!SkillCooldownService.IsReady(owner, effect.Definition.CooldownSkill) || !Roll(effect)) return false;
        SkillCooldownService.Start(owner, effect.Definition.CooldownSkill, effect.Definition.cooldown);
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

    /// <summary>把一次被动形成效果提交为跟踪明确目标的标准预付费技能。</summary>
    private static bool Trigger(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        Entity skill,
        BaseSimObject target,
        float strength,
        ElementComposition? element = null)
    {
        SkillCastRuntimeData runtimeData = element.HasValue
            ? SkillCastRuntimeData.Create(effect.Potency, DamageOrigin.Reaction, element.Value)
            : SkillCastRuntimeData.Create(effect.Potency, DamageOrigin.Reaction);
        if (owner?.Base == null || owner.Base.isRekt() || skill.IsNull || target.isRekt()) return false;
        var plan = new SkillCastPlan();
        plan.Steps.Add(new SkillCastStep(target, 0f));
        return ModClass.I.SkillV3.QueueSkillSequence(
            owner,
            skill,
            plan,
            strength,
            owner.GetPowerLevel(),
            SkillCastFundingSource.Prepaid,
            owner.Base.kingdom,
            runtimeData);
    }
}
