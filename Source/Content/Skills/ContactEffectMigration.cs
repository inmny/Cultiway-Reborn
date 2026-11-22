using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;
using strings;

namespace Cultiway.Content.Skills;

/// <summary>原接触词条只展开为普通状态和恢复资料，不保留旧执行实体或运行回调。</summary>
internal static partial class ContactEffectMigration
{
    /// <summary>已经完整接管的原接触附加词条。</summary>
    /// <param name="type">原容器中的词条类型。</param>
    internal static bool Supports(Type type) => SupportsUtilityLanding(type) ||
        type == typeof(ChaosModifier) || type == typeof(GravityModifier);

    /// <summary>可在原地面冲击中使用同一份状态与伤害资料的词条。</summary>
    /// <param name="type">原容器中的词条类型。</param>
    internal static bool SupportsUtilityLanding(Type type) => type == typeof(SlowModifier) || type == typeof(FreezeModifier) ||
        type == typeof(DazeModifier) || type == typeof(WeakenModifier) || type == typeof(ArmorBreakModifier) ||
        type == typeof(SilenceModifier) || type == typeof(BurnModifier) || type == typeof(PoisonModifier) ||
        type == typeof(EternalCurseModifier) || type == typeof(BurnoutModifier) || type == typeof(KnockbackModifier) ||
        type == typeof(SwapModifier) || type == typeof(MercyModifier) || type == typeof(RandomAffixModifier) ||
        type == typeof(DeathSentenceModifier) || type == typeof(ReincarnationTrialModifier) || type == typeof(ExplosionModifier);

    /// <summary>在真实逐发转换时一次确定原设置词条，运行及预测只读取已经展开的数值。</summary>
    /// <param name="skill">真实原容器。</param>
    /// <param name="angle">本次初始朝向的额外偏角，不改写原目标方向。</param>
    /// <param name="speed">本次运动速度倍率，不改变转向速度。</param>
    internal static float SetupStrengthScale(Entity skill, out float angle, out float speed)
    {
        float strength = skill.TryGetComponent(out MercyModifier mercy) ? Mathf.Clamp(mercy.DamageMultiplier, .05f, 1f) : 1f;
        angle = 0f; speed = 1f;
        if (skill.TryGetComponent(out ChaosModifier chaos))
        {
            // 沿用原抽取顺序及下限；没有作用的速度和偏角不会被固定形式强行使用。
            float damageVariance = Mathf.Clamp(chaos.DamageVariance, 0f, 2f);
            float angleVariance = Mathf.Clamp(chaos.AngleVariance, 0f, 90f);
            float speedVariance = Mathf.Clamp(chaos.SpeedVariance, 0f, 2f);
            strength *= Mathf.Max(.05f, 1f + Randy.randomFloat(-damageVariance, damageVariance));
            if (angleVariance > 0f) angle = Randy.randomFloat(-angleVariance, angleVariance);
            if (speedVariance > 0f) speed = Mathf.Max(.1f, 1f + Randy.randomFloat(-speedVariance, speedVariance));
        }
        return strength;
    }

    /// <summary>核对真实接触回调全部来自已映射词条，额外回调继续原入口。</summary>
    /// <param name="skill">实际原容器。</param>
    internal static bool MatchesCallbacks(Entity skill)
    {
        var expected = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!Supports(type)) continue;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnEffectObj != null) expected.AddRange(modifier.ModifierAsset.OnEffectObj.GetInvocationList());
        }
        foreach (Delegate callback in skill.GetComponent<SkillContainer>().OnEffectObj?.GetInvocationList() ?? Array.Empty<Delegate>())
            if (!expected.Remove(callback)) return false;
        return expected.Count == 0;
    }

    /// <summary>完整核对已登记的旅行回调，不接管未知附加行为。</summary>
    /// <param name="skill">真实原容器。</param>
    internal static bool MatchesTravelCallbacks(Entity skill)
    {
        var expected = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!Supports(type)) continue;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnTravel != null) expected.AddRange(modifier.ModifierAsset.OnTravel.GetInvocationList());
        }
        foreach (Delegate callback in skill.GetComponent<SkillContainer>().OnTravel?.GetInvocationList() ?? Array.Empty<Delegate>())
            if (!expected.Remove(callback)) return false;
        return expected.Count == 0;
    }

    /// <summary>展开原落地或接触附带的状态，时机和受影响的人物由实际落点或接触确定。</summary>
    /// <param name="skill">原容器。</param><param name="effectScale">本次固定效果倍率。</param>
    private static List<ConstructionStatus> ControlStatuses(Entity skill, float effectScale)
    {
        var statuses = new List<ConstructionStatus>();
        if (skill.TryGetComponent(out SlowModifier slow))
        {
            float duration = Mathf.Clamp(slow.Duration, 0f, 999f), strength = Mathf.Clamp01(slow.Strength * effectScale);
            if (duration > 0f && strength > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Slow.id, Duration = duration, Application = ConstructionStatusApplication.Stack,
                Stats = new Dictionary<string, float> { [S.multiplier_speed] = -strength }
            });
        }
        if (skill.TryGetComponent(out FreezeModifier freeze))
        {
            float duration = Mathf.Clamp(freeze.Duration * effectScale, 0f, 999f);
            if (duration > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Freeze.id, Duration = duration, Application = ConstructionStatusApplication.Stack
            });
        }
        if (skill.TryGetComponent(out DazeModifier daze))
        {
            float duration = Mathf.Clamp(daze.Duration * effectScale, 0f, 10f);
            if (duration > 0f) statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.Daze.id, Duration = duration, Application = ConstructionStatusApplication.Refresh, WaitForDuration = true
            });
        }
        if (skill.TryGetComponent(out WeakenModifier weaken))
            AddReduction(statuses, StatusEffects.Weaken.id, weaken.Duration, S.damage, weaken.AttackReduction * effectScale);
        if (skill.TryGetComponent(out ArmorBreakModifier armorBreak))
            AddReduction(statuses, StatusEffects.ArmorBreak.id, armorBreak.Duration, S.armor, armorBreak.ArmorReduction * effectScale);
        if (skill.TryGetComponent(out SilenceModifier silence))
            AddReduction(statuses, StatusEffects.Silence.id, silence.Duration, S.damage, silence.DamageReduction * effectScale);
        return statuses;
    }

    /// <summary>按原容器顺序展开会改变位置、生命或世界的词条，并保留原状态刷新顺序。</summary>
    /// <param name="skill">原容器。</param><param name="effectScale">本次固定效果倍率。</param>
    /// <param name="caster">决定爆炸原范围成长的实际施法者。</param><param name="steps">本次落地的实际执行顺序。</param>
    internal static ConstructionImpact UtilityLanding(Entity skill, float effectScale, Core.ActorExtend caster,
        out UtilityLandingStep[] steps)
    {
        List<ConstructionStatus> statuses = ControlStatuses(skill, effectScale);
        var damage = new List<ConstructionContactDamage>();
        var order = new List<UtilityLandingStep>();
        ConstructionKnockback knockback = LandingKnockback(skill, effectScale);
        ConstructionExchange exchange = LandingExchange(skill, effectScale);
        float healing = MercyHealing(skill);
        ConstructionRandomContact random = RandomContact(skill, effectScale, 1f);
        ConstructionConditionalContact[] conditional = ConditionalContacts(skill, effectScale, 1f, statuses);
        ConstructionContactSpread[] spreads = ContactSpreads(skill, caster, 1f);
        foreach (Type type in skill.GetComponentTypes())
        {
            if (type == typeof(BurnoutModifier))
            {
                AddBurnout(skill, 1f, statuses, damage);
                if (damage.Count > 0) order.Add(UtilityLandingStep.AdditionalDamage);
            }
            else if (type == typeof(KnockbackModifier) && knockback != null) order.Add(UtilityLandingStep.Knockback);
            else if (type == typeof(SwapModifier) && exchange != null) order.Add(UtilityLandingStep.Exchange);
            else if (type == typeof(MercyModifier) && healing > 0f) order.Add(UtilityLandingStep.Restoration);
            else if (type == typeof(RandomAffixModifier) && random != null) order.Add(UtilityLandingStep.Random);
            else if (type == typeof(ExplosionModifier) && spreads.Length > 0) order.Add(UtilityLandingStep.Explosion);
            else if (type == typeof(DeathSentenceModifier)) order.Add(UtilityLandingStep.DeathSentence);
            else if (type == typeof(ReincarnationTrialModifier) && Array.Exists(conditional, rule => rule.OnKill != null))
                order.Add(UtilityLandingStep.ReincarnationTrial);
            else AddDamageStatus(skill, type, effectScale, 1f, statuses);
        }
        steps = order.ToArray();
        return statuses.Count == 0 && steps.Length == 0 ? null : new ConstructionImpact
        {
            DamageScale = 0f,
            Statuses = statuses.ToArray(),
            AdditionalDamage = damage.ToArray(),
            Knockback = knockback,
            Exchange = exchange,
            RandomContact = random,
            ConditionalContacts = conditional,
            Spreads = spreads,
            Restoration = healing > 0f ? new ConstructionRestoration
            {
                Target = ConstructionRestorationTarget.Controller,
                HealthPerForce = healing
            } : null
        };
    }

    /// <summary>读取经过原使用倍率限制的击退幅度。</summary>
    /// <param name="skill">原容器。</param><param name="effectScale">本次效果倍率。</param>
    private static ConstructionKnockback LandingKnockback(Entity skill, float effectScale)
    {
        if (!skill.TryGetComponent(out KnockbackModifier push)) return null;
        float distance = Mathf.Clamp(push.Distance * effectScale, 0f, 10f);
        float height = Mathf.Clamp(push.Height * effectScale, 0f, 5f);
        return distance > 0f || height > 0f ? new ConstructionKnockback { Distance = distance, Height = height } : null;
    }

    /// <summary>读取经过原使用倍率限制的实际换位概率。</summary>
    /// <param name="skill">原容器。</param><param name="effectScale">本次效果倍率。</param>
    private static ConstructionExchange LandingExchange(Entity skill, float effectScale)
    {
        if (!skill.TryGetComponent(out SwapModifier swap)) return null;
        float chance = Mathf.Clamp01(swap.Chance * effectScale);
        return chance > 0f ? new ConstructionExchange { Chance = chance } : null;
    }

    /// <summary>读取原回生比例，仍由实际接触的强度和载体决定恢复量。</summary>
    /// <param name="skill">原容器。</param>
    private static float MercyHealing(Entity skill) => skill.TryGetComponent(out MercyModifier mercy)
        ? Mathf.Clamp(mercy.HealRatio, 0f, 5f) : 0f;

    /// <summary>在真实攻击面附加状态和恢复；飞行中的零伤害爆发载体及纯防御面不提前施加。</summary>
    /// <param name="definition">已经转换的普通构造。</param><param name="skill">原容器。</param>
    /// <param name="effectScale">本次固定效果倍率。</param>
    /// <param name="contactForceScale">本体量换回原附加作用强度的比例，不重复乘来源效果倍率。</param>
    /// <param name="includeRoot">本体是否已经是实际作用面，爆发载体在后段作用。</param>
    /// <param name="caster">本次真实施法者，仅在资料展开时读取范围成长。</param>
    /// <param name="runtimeData">本次发射的动态元素，独立附加作用仍用自身声明。</param>
    internal static AbilityDefinition Bind(AbilityDefinition definition, Entity skill, float effectScale, bool includeRoot = true, float contactForceScale = 1f, Core.ActorExtend caster = null, SkillCastRuntimeData runtimeData = default)
    {
        List<ConstructionStatus> statuses = ControlStatuses(skill, effectScale);
        var additionalDamage = new List<ConstructionContactDamage>();
        AddDamageStatus(skill, typeof(BurnModifier), effectScale, contactForceScale, statuses);
        AddDamageStatus(skill, typeof(PoisonModifier), effectScale, contactForceScale, statuses);
        AddDamageStatus(skill, typeof(EternalCurseModifier), effectScale, contactForceScale, statuses);
        float healing = MercyHealing(skill) * contactForceScale;
        ConstructionKnockback knockback = LandingKnockback(skill, effectScale);
        ConstructionExchange exchange = LandingExchange(skill, effectScale);
        AddBurnout(skill, contactForceScale, statuses, additionalDamage);
        ConstructionRandomContact randomContact = RandomContact(skill, effectScale, contactForceScale);
        ConstructionConditionalContact[] conditional = ConditionalContacts(skill, effectScale, contactForceScale, statuses);
        ConstructionContactSpread[] spreads = ContactSpreads(skill, caster, contactForceScale);
        if (statuses.Count > 0 || healing > 0f || knockback != null || exchange != null || additionalDamage.Count > 0 || randomContact != null || conditional.Length > 0 || spreads.Length > 0)
            BindNode(definition.Root, statuses.ToArray(), healing, knockback, exchange, additionalDamage.ToArray(), randomContact, conditional, spreads, includeRoot);
        if (skill.TryGetComponent(out GravityModifier gravity))
        {
            float original = Core.SkillLibV3.Utils.SkillEffectRadius.ResolveContainer(skill, 1f);
            var asset = skill.GetComponent<SkillContainer>().Asset;
            float growth = Core.SkillLibV3.Utils.SkillEffectRadius.ResolveCasterGrowth(caster, skill);
            float basis = Mathf.Max(asset.ImpactProfile.EffectRadius, asset.ImpactProfile.CollisionRadius);
            if (basis > 0f) growth = Mathf.Max(1f, Mathf.Min(growth, 32f / (basis * original)));
            definition.Root.RadialMotion = new ConstructionRadialMotion
            {
                Radius = Mathf.Clamp(gravity.Radius, .5f, 10f) * original * growth,
                Speed = -Mathf.Clamp(gravity.Strength * effectScale, 0f, 10f)
            };
        }
        return NatureConstructionMigration.Bind(MediumContactMigration.Bind(definition, skill, contactForceScale, includeRoot, runtimeData), skill, runtimeData);
    }

    /// <summary>两种旧接触入口共用周期数值、元素和叠层规则。</summary>
    /// <param name="skill">原容器。</param><param name="type">正在展开的词条。</param>
    /// <param name="effectScale">本次效果倍率。</param><param name="forceScale">换回原单位强度的比例。</param>
    /// <param name="statuses">本次声明。</param>
    private static void AddDamageStatus(Entity skill, Type type, float effectScale, float forceScale, List<ConstructionStatus> statuses)
    {
        if (type == typeof(BurnModifier) && skill.TryGetComponent(out BurnModifier burn))
            AddTick(statuses, StatusEffects.Burn.id, burn.Duration, Mathf.Clamp01(burn.DamageRatio) * forceScale, Core.ElementComposition.Static.Fire);
        else if (type == typeof(PoisonModifier) && skill.TryGetComponent(out PoisonModifier poison))
            AddTick(statuses, StatusEffects.Poison.id, poison.Duration, Mathf.Clamp01(poison.DamageRatio) * forceScale,
                Core.ElementComposition.Static.Poison, Mathf.Clamp(poison.MaxStacks, 1, 99));
        else if (type == typeof(EternalCurseModifier) && skill.TryGetComponent(out EternalCurseModifier curse))
        {
            float duration = Mathf.Clamp(curse.Duration, 0f, 999f);
            if (duration <= 0f) return;
            float reduction = Mathf.Clamp01(curse.DebuffRatio * effectScale);
            float damageRatio = Mathf.Clamp(curse.DamageRatio, 0f, 20f) * forceScale;
            statuses.Add(new ConstructionStatus
            {
                Id = StatusEffects.EternalCurse.id, Duration = duration, Application = ConstructionStatusApplication.Refresh,
                TargetStatRatios = reduction > 0f ? new Dictionary<string, float> { [S.damage] = -reduction, [S.armor] = -reduction } : null,
                Tick = damageRatio > 0f ? new ConstructionStatusTick
                {
                    ForcePerSecond = damageRatio / duration,
                    Element = new Core.ElementComposition(neg: .5f, entropy: .5f)
                } : null
            });
        }
    }

    /// <summary>持续伤害沿用原共享状态，真实接触时才按余力写入每秒量。</summary>
    /// <param name="statuses">本次接触声明。</param><param name="id">共享状态编号。</param>
    /// <param name="duration">原持续世界秒。</param><param name="ratio">已经换算本体基准的总伤害比例。</param>
    /// <param name="element">原周期伤害元素。</param><param name="stackLimit">毒伤同源层数，零表示同类刷新。</param>
    private static void AddTick(List<ConstructionStatus> statuses, string id, float duration, float ratio, Core.ElementComposition element, int stackLimit = 0)
    {
        duration = Mathf.Clamp(duration, 0f, 999f);
        if (duration <= 0f || ratio <= 0f) return;
        statuses.Add(new ConstructionStatus
        {
            Id = id, Duration = duration, Application = stackLimit > 0 ? ConstructionStatusApplication.SourceStack : ConstructionStatusApplication.Refresh,
            StackLimit = stackLimit, Tick = new ConstructionStatusTick { ForcePerSecond = ratio / duration, Element = element }
        });
    }

    /// <summary>比例削弱在命中时取目标属性，同类刷新并沿用状态模板中的控制标记。</summary>
    /// <param name="statuses">本次接触声明。</param><param name="id">共享状态编号。</param>
    /// <param name="duration">原持续世界秒。</param><param name="stat">受影响的同名属性。</param>
    /// <param name="reduction">已经包含本次效果倍率的削弱比例。</param>
    private static void AddReduction(List<ConstructionStatus> statuses, string id, float duration, string stat, float reduction)
    {
        duration = Mathf.Clamp(duration, 0f, 999f);
        reduction = Mathf.Clamp01(reduction);
        if (duration <= 0f || reduction <= 0f) return;
        statuses.Add(new ConstructionStatus
        {
            Id = id, Duration = duration, Application = ConstructionStatusApplication.Refresh,
            TargetStatRatios = new Dictionary<string, float> { [stat] = -reduction }
        });
    }

    /// <summary>复用原接触关系、窗口和次数，不另造扩大作用区。</summary>
    /// <param name="node">实际部位。</param><param name="statuses">本次待编译的状态资料。</param>
    /// <param name="healing">本体量换回原恢复基准后的比例。</param>
    /// <param name="knockback">本次接触的击退与腾起幅度。</param>
    /// <param name="exchange">真实接触后的换位概率及停步。</param>
    /// <param name="additionalDamage">同次追加的独立元素伤害。</param>
    /// <param name="randomContact">真实接触时才抽取的普通候选。</param>
    /// <param name="conditional">按真实生命和本条伤害结果执行的有限分支。</param>
    /// <param name="spreads">每次命中后独立分送的普通范围作用。</param>
    /// <param name="include">是否在本部位的接触时点施加。</param>
    private static void BindNode(ConstructionNode node, ConstructionStatus[] statuses, float healing, ConstructionKnockback knockback, ConstructionExchange exchange, ConstructionContactDamage[] additionalDamage, ConstructionRandomContact randomContact, ConstructionConditionalContact[] conditional, ConstructionContactSpread[] spreads, bool include = true)
    {
        if (include && (node.ContactRole == ConstructionContactRole.Attack || node.ContactRole == ConstructionContactRole.Barrier && node.Impact != null))
        {
            if (node.Impact == null) node.Impact = new ConstructionImpact();
            if (statuses.Length > 0) node.Impact.Statuses = statuses;
            if (knockback != null) node.Impact.Knockback = new ConstructionKnockback { Distance = knockback.Distance, Height = knockback.Height };
            if (exchange != null) node.Impact.Exchange = new ConstructionExchange { Chance = exchange.Chance, WaitSeconds = exchange.WaitSeconds };
            if (additionalDamage.Length > 0) node.Impact.AdditionalDamage = additionalDamage;
            if (randomContact != null) node.Impact.RandomContact = randomContact;
            if (conditional.Length > 0) node.Impact.ConditionalContacts = conditional;
            if (spreads.Length > 0) node.Impact.Spreads = spreads;
            if (healing > 0f) node.Impact.Restoration = new ConstructionRestoration
            {
                Target = ConstructionRestorationTarget.Controller,
                // 屏障容量包含耐久倍率，恢复仍按原术强度；余力受损比例继续保留。
                HealthPerForce = healing / node.Strength
            };
        }
        if (node.Children == null) return;
        foreach (ConstructionNode child in node.Children) BindNode(child, statuses, healing, knockback, exchange, additionalDamage, randomContact, conditional, spreads);
    }
}
