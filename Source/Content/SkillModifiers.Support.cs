using Cultiway.Content.Combat;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class SkillModifiers
{
    public static SkillModifierAsset Healing { get; private set; }
    public static SkillModifierAsset Rejuvenation { get; private set; }
    public static SkillModifierAsset Purification { get; private set; }
    public static SkillModifierAsset BattleBlessing { get; private set; }
    public static SkillModifierAsset GuardBlessing { get; private set; }
    public static SkillModifierAsset HasteBlessing { get; private set; }

    /// <summary>注册辅助法术的必选效果词条、编辑字段、语义和评级委托。</summary>
    private void ConfigureSupportModifiers()
    {
        Setup<HealingModifier>(Healing, SkillModifierRarity.Common);
        SetEditorSkillIcon(Healing, "healing_light");
        Healing.CastDemand = 0f;
        Healing.AddSemantics(SkillSemantics.Effect.Heal, SkillSemantics.Role.Support);
        Healing.AddEffect(new SkillEffectDescriptor
        {
            Id = "support.healing",
            TargetRelation = SkillEffectTargetRelation.Friendly,
            Trigger = SkillEffectTrigger.Impact,
            CanApplyObject = CanHeal,
            ApplyObject = ApplyHealing,
            EvaluateObjectUtility = EvaluateHealingUtility
        });
        Healing.EvaluateLevel = EvaluateHealing;
        ConfigureEditor<HealingModifier>(Healing, "Support",
            Float(nameof(HealingModifier.Amount), "HealingAmount", 35f, 1f, 1000f, 1f, "Value"));

        Setup<RejuvenationModifier>(Rejuvenation, SkillModifierRarity.Common);
        SetEditorSkillIcon(Rejuvenation, "rejuvenation_field");
        Rejuvenation.CastDemand = 0f;
        Rejuvenation.AddSemantics(
            SkillSemantics.Effect.Regeneration,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Role.Support);
        Rejuvenation.AddEffect(new SkillEffectDescriptor
        {
            Id = "support.rejuvenation",
            TargetRelation = SkillEffectTargetRelation.Friendly,
            Trigger = SkillEffectTrigger.Periodic,
            Interval = 1f,
            CanApplyObject = CanRejuvenate,
            ApplyObject = ApplyRejuvenation,
            EvaluateObjectUtility = EvaluateRejuvenationUtility
        });
        Rejuvenation.EvaluateLevel = EvaluateRejuvenation;
        ConfigureEditor<RejuvenationModifier>(Rejuvenation, "Support",
            Float(nameof(RejuvenationModifier.Duration), "Duration", 8f, 1f, 60f, 0.5f, "Seconds"),
            Float(nameof(RejuvenationModifier.HealPerSecond), "HealingPerSecond", 8f, 1f, 200f, 1f, "Value"));

        Setup<PurificationModifier>(Purification, SkillModifierRarity.Common);
        SetEditorSkillIcon(Purification, "purification_wave");
        Purification.CastDemand = 0f;
        Purification.AddSemantics(
            SkillSemantics.Effect.Cleanse,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Role.Support);
        Purification.AddEffect(new SkillEffectDescriptor
        {
            Id = "support.purification",
            TargetRelation = SkillEffectTargetRelation.Friendly,
            Trigger = SkillEffectTrigger.Impact,
            CanApplyObject = CanPurify,
            ApplyObject = ApplyPurification,
            EvaluateObjectUtility = EvaluatePurificationUtility
        });
        Purification.EvaluateLevel = EvaluatePurification;
        ConfigureEditor<PurificationModifier>(Purification, "Support",
            Integer(nameof(PurificationModifier.MaxStatuses), "CleanseCount", 1, 1, 8, 1));

        Setup<BattleBlessingModifier>(BattleBlessing, SkillModifierRarity.Common);
        SetEditorSkillIcon(BattleBlessing, "battle_blessing");
        BattleBlessing.CastDemand = 0f;
        BattleBlessing.AddSemantics(SkillSemantics.Effect.AttackBoost, SkillSemantics.Role.Support);
        BattleBlessing.AddEffect(CreateBlessingEffect(
            "support.battle_blessing",
            ApplyBattleBlessing,
            EvaluateBattleBlessingUtility));
        BattleBlessing.EvaluateLevel = EvaluateBattleBlessing;
        ConfigureEditor<BattleBlessingModifier>(BattleBlessing, "Support",
            Float(nameof(BattleBlessingModifier.Duration), "Duration", 8f, 1f, 60f, 0.5f, "Seconds"),
            Float(nameof(BattleBlessingModifier.DamageBonus), "DamageBonus", 0.15f, 0.01f, 2f, 0.01f,
                "Percent"));

        Setup<GuardBlessingModifier>(GuardBlessing, SkillModifierRarity.Common);
        SetEditorSkillIcon(GuardBlessing, "guard_blessing");
        GuardBlessing.CastDemand = 0f;
        GuardBlessing.AddSemantics(SkillSemantics.Effect.DefenseBoost, SkillSemantics.Role.Support);
        GuardBlessing.AddEffect(CreateBlessingEffect(
            "support.guard_blessing",
            ApplyGuardBlessing,
            EvaluateGuardBlessingUtility));
        GuardBlessing.EvaluateLevel = EvaluateGuardBlessing;
        ConfigureEditor<GuardBlessingModifier>(GuardBlessing, "Support",
            Float(nameof(GuardBlessingModifier.Duration), "Duration", 8f, 1f, 60f, 0.5f, "Seconds"),
            Float(nameof(GuardBlessingModifier.ArmorBonus), "ArmorBonus", 12f, 1f, 500f, 1f, "Value"));

        Setup<HasteBlessingModifier>(HasteBlessing, SkillModifierRarity.Common);
        SetEditorSkillIcon(HasteBlessing, "haste_blessing");
        HasteBlessing.CastDemand = 0f;
        HasteBlessing.AddSemantics(SkillSemantics.Effect.Speed, SkillSemantics.Role.Support);
        HasteBlessing.AddEffect(CreateBlessingEffect(
            "support.haste_blessing",
            ApplyHasteBlessing,
            EvaluateHasteBlessingUtility));
        HasteBlessing.EvaluateLevel = EvaluateHasteBlessing;
        ConfigureEditor<HasteBlessingModifier>(HasteBlessing, "Support",
            Float(nameof(HasteBlessingModifier.Duration), "Duration", 8f, 1f, 60f, 0.5f, "Seconds"),
            Float(nameof(HasteBlessingModifier.MoveSpeedBonus), "MoveSpeedBonus", 0.2f, 0.01f, 2f, 0.01f,
                "Percent"),
            Float(nameof(HasteBlessingModifier.AttackSpeedBonus), "AttackSpeedBonus", 0.1f, 0.01f, 2f, 0.01f,
                "Percent"));
    }

    /// <summary>创建共享友方单体祝福的结构化效果定义。</summary>
    private static SkillEffectDescriptor CreateBlessingEffect(
        string id,
        SkillObjectEffectAction apply,
        SkillObjectEffectUtility utility)
    {
        return new SkillEffectDescriptor
        {
            Id = id,
            TargetRelation = SkillEffectTargetRelation.Friendly,
            Trigger = SkillEffectTrigger.Impact,
            CanApplyObject = IsActorTarget,
            ApplyObject = apply,
            EvaluateObjectUtility = utility
        };
    }

    /// <summary>判断对象是否是仍然存活的单位。</summary>
    private static bool IsActorTarget(in SkillEffectEvaluationContext _, BaseSimObject target)
    {
        return target != null && !target.isRekt() && target.isActor();
    }

    /// <summary>判断目标是否存在可恢复生命。</summary>
    private static bool CanHeal(in SkillEffectEvaluationContext _, BaseSimObject target)
    {
        return target?.isActor() == true && !target.isRekt() &&
               target.a.data.health < target.a.getMaxHealth();
    }

    /// <summary>执行即时治疗。</summary>
    private static SkillEffectResult ApplyHealing(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || target.isRekt()) return default;
        float missing = Mathf.Max(0f, target.a.getMaxHealth() - target.a.data.health);
        if (missing <= 0f) return default;
        float amount = context.SkillContainer.GetComponent<HealingModifier>().Amount * context.Cast.EffectScale;
        CombatResourceEffects.RestoreHealth(target.a, amount);
        return new SkillEffectResult(
            SkillEffectOutcomeFlags.HealthRestored,
            magnitude: Mathf.Min(missing, Mathf.Max(0f, amount)));
    }

    /// <summary>按实际可恢复量评价即时治疗目标，低溢出收益会被显著降权。</summary>
    private static float EvaluateHealingUtility(
        in SkillEffectEvaluationContext context,
        BaseSimObject target)
    {
        if (!CanHeal(in context, target)) return 0f;
        float amount = Mathf.Max(1f, context.SkillContainer.GetComponent<HealingModifier>().Amount);
        float effective = Mathf.Min(amount, target.a.getMaxHealth() - target.a.data.health);
        float ratio = Mathf.Clamp01(effective / amount);
        return effective * Mathf.Lerp(0.2f, 1f, ratio);
    }

    /// <summary>持续恢复只作用于受伤单位。</summary>
    private static bool CanRejuvenate(in SkillEffectEvaluationContext _, BaseSimObject target)
    {
        return target?.isActor() == true && !target.isRekt() &&
               target.a.data.health < target.a.getMaxHealth();
    }

    /// <summary>通过全局最强状态施加持续恢复，避免多个区域叠加治疗。</summary>
    private static SkillEffectResult ApplyRejuvenation(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || target.isRekt() || context.Cast.SourceObj?.isActor() != true)
            return default;
        RejuvenationModifier modifier = context.SkillContainer.GetComponent<RejuvenationModifier>();
        float healPerSecond = modifier.HealPerSecond * context.Cast.EffectScale;
        bool changed = CombatStatusEffects.ApplyStrongestTickingStatus(
            target.a,
            StatusEffects.Rejuvenating,
            1.25f,
            healPerSecond,
            healPerSecond,
            context.Cast.ResolveElement(context.SkillContainer.GetComponent<SkillContainer>().Asset.Element),
            context.Cast.SourceObj.a);
        return changed
            ? new SkillEffectResult(SkillEffectOutcomeFlags.StatusApplied, magnitude: healPerSecond)
            : default;
    }

    /// <summary>只把受伤且没有同强恢复状态的友军视为持续恢复候选。</summary>
    private static float EvaluateRejuvenationUtility(
        in SkillEffectEvaluationContext context,
        BaseSimObject target)
    {
        if (!CanRejuvenate(in context, target)) return 0f;
        float potency = context.SkillContainer.GetComponent<RejuvenationModifier>().HealPerSecond;
        if (CombatStatusEffects.HasEqualOrStrongerStatus(target.a, StatusEffects.Rejuvenating, potency)) return 0f;
        float missingRatio = 1f - target.a.getHealthRatio();
        return potency * (0.25f + missingRatio);
    }

    /// <summary>判断目标是否持有可净化的原版或共享负面状态。</summary>
    private static bool CanPurify(in SkillEffectEvaluationContext _, BaseSimObject target)
    {
        return target?.isActor() == true && !target.isRekt() &&
               CombatStatusEffects.ResolveHighestNegativePriority(target.a) > 0;
    }

    /// <summary>按统一优先级逐项净化目标的原版或共享负面状态。</summary>
    private static SkillEffectResult ApplyPurification(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || target.isRekt()) return default;
        int count = Mathf.Max(1, context.SkillContainer.GetComponent<PurificationModifier>().MaxStatuses);
        int removed = 0;
        for (int i = 0; i < count; i++)
        {
            if (!CombatStatusEffects.CleanseHighestPriorityNegativeStatus(target.a)) break;
            removed++;
        }
        return removed > 0
            ? new SkillEffectResult(SkillEffectOutcomeFlags.StatusRemoved, removed)
            : default;
    }

    /// <summary>按最严重负面状态的优先级评价净化目标。</summary>
    private static float EvaluatePurificationUtility(
        in SkillEffectEvaluationContext _,
        BaseSimObject target)
    {
        return target?.isActor() == true
            ? CombatStatusEffects.ResolveHighestNegativePriority(target.a) * 0.1f
            : 0f;
    }

    /// <summary>施加伤害倍率祝福。</summary>
    private static SkillEffectResult ApplyBattleBlessing(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || context.Cast.SourceObj?.isActor() != true) return default;
        BattleBlessingModifier modifier = context.SkillContainer.GetComponent<BattleBlessingModifier>();
        float bonus = modifier.DamageBonus * context.Cast.EffectScale;
        bool changed = CombatStatusEffects.ApplyStrongestStatus(
            target.a,
            StatusEffects.BattleBlessing,
            modifier.Duration,
            bonus,
            context.Cast.SourceObj.a,
            new BaseStats { [S.multiplier_damage] = bonus });
        return changed
            ? new SkillEffectResult(SkillEffectOutcomeFlags.StatusApplied, magnitude: bonus)
            : default;
    }

    /// <summary>施加固定护甲祝福。</summary>
    private static SkillEffectResult ApplyGuardBlessing(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || context.Cast.SourceObj?.isActor() != true) return default;
        GuardBlessingModifier modifier = context.SkillContainer.GetComponent<GuardBlessingModifier>();
        float bonus = modifier.ArmorBonus * context.Cast.EffectScale;
        bool changed = CombatStatusEffects.ApplyStrongestStatus(
            target.a,
            StatusEffects.GuardBlessing,
            modifier.Duration,
            bonus,
            context.Cast.SourceObj.a,
            new BaseStats { [S.armor] = bonus });
        return changed
            ? new SkillEffectResult(SkillEffectOutcomeFlags.StatusApplied, magnitude: bonus)
            : default;
    }

    /// <summary>施加移动与攻击速度祝福。</summary>
    private static SkillEffectResult ApplyHasteBlessing(in SkillEffectContext context, BaseSimObject target)
    {
        if (target?.isActor() != true || context.Cast.SourceObj?.isActor() != true) return default;
        HasteBlessingModifier modifier = context.SkillContainer.GetComponent<HasteBlessingModifier>();
        float move = modifier.MoveSpeedBonus * context.Cast.EffectScale;
        float attack = modifier.AttackSpeedBonus * context.Cast.EffectScale;
        bool changed = CombatStatusEffects.ApplyStrongestStatus(
            target.a,
            StatusEffects.HasteBlessing,
            modifier.Duration,
            move + attack,
            context.Cast.SourceObj.a,
            new BaseStats
            {
                [S.multiplier_speed] = move,
                [S.multiplier_attack_speed] = attack
            });
        return changed
            ? new SkillEffectResult(SkillEffectOutcomeFlags.StatusApplied, magnitude: move + attack)
            : default;
    }

    /// <summary>战意祝福只把处于战斗且没有同强状态的单位计为 AI 收益。</summary>
    private static float EvaluateBattleBlessingUtility(
        in SkillEffectEvaluationContext context,
        BaseSimObject target)
    {
        if (!IsCombatBuffCandidate(target)) return 0f;
        float potency = context.SkillContainer.GetComponent<BattleBlessingModifier>().DamageBonus;
        return CombatStatusEffects.HasEqualOrStrongerStatus(target.a, StatusEffects.BattleBlessing, potency)
            ? 0f
            : potency * 100f;
    }

    /// <summary>守护祝福只把处于战斗且没有同强状态的单位计为 AI 收益。</summary>
    private static float EvaluateGuardBlessingUtility(
        in SkillEffectEvaluationContext context,
        BaseSimObject target)
    {
        if (!IsCombatBuffCandidate(target)) return 0f;
        float potency = context.SkillContainer.GetComponent<GuardBlessingModifier>().ArmorBonus;
        return CombatStatusEffects.HasEqualOrStrongerStatus(target.a, StatusEffects.GuardBlessing, potency)
            ? 0f
            : potency;
    }

    /// <summary>迅捷祝福只把处于战斗且没有同强状态的单位计为 AI 收益。</summary>
    private static float EvaluateHasteBlessingUtility(
        in SkillEffectEvaluationContext context,
        BaseSimObject target)
    {
        if (!IsCombatBuffCandidate(target)) return 0f;
        HasteBlessingModifier modifier = context.SkillContainer.GetComponent<HasteBlessingModifier>();
        float potency = modifier.MoveSpeedBonus + modifier.AttackSpeedBonus;
        return CombatStatusEffects.HasEqualOrStrongerStatus(target.a, StatusEffects.HasteBlessing, potency)
            ? 0f
            : potency * 50f;
    }

    /// <summary>判断单位是否正在攻击或刚刚受到攻击。</summary>
    private static bool IsCombatBuffCandidate(BaseSimObject target)
    {
        return target?.isActor() == true && !target.isRekt() &&
               (target.a.has_attack_target || target.a.isJustAttacked());
    }

    /// <summary>把即时治疗贡献为纯效用评级。</summary>
    private static void EvaluateHealing(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        context.AddUtility(container.GetComponent<HealingModifier>().Amount / 20f);
    }

    /// <summary>把持续恢复总量贡献为纯效用评级。</summary>
    private static void EvaluateRejuvenation(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        RejuvenationModifier modifier = container.GetComponent<RejuvenationModifier>();
        context.AddUtility(modifier.Duration * modifier.HealPerSecond / 35f);
    }

    /// <summary>把净化数量贡献为纯效用评级。</summary>
    private static void EvaluatePurification(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        context.AddUtility(container.GetComponent<PurificationModifier>().MaxStatuses * 1.5f);
    }

    /// <summary>把战意祝福贡献为纯效用评级。</summary>
    private static void EvaluateBattleBlessing(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        BattleBlessingModifier modifier = container.GetComponent<BattleBlessingModifier>();
        context.AddUtility(modifier.Duration * modifier.DamageBonus * 0.5f);
    }

    /// <summary>把守护祝福贡献为纯效用评级。</summary>
    private static void EvaluateGuardBlessing(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        GuardBlessingModifier modifier = container.GetComponent<GuardBlessingModifier>();
        context.AddUtility(modifier.Duration * modifier.ArmorBonus / 80f);
    }

    /// <summary>把迅捷祝福贡献为纯效用评级。</summary>
    private static void EvaluateHasteBlessing(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        HasteBlessingModifier modifier = container.GetComponent<HasteBlessingModifier>();
        context.AddUtility(modifier.Duration * (modifier.MoveSpeedBonus + modifier.AttackSpeedBonus) * 0.5f);
    }
}
