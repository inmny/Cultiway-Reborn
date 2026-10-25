using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public enum SkillCastFundingSource
{
    CasterResources,
    Prepaid
}

/// <summary>
/// 计算无量纲施法需求，并通过已解析的资源通道完成报价和原子扣除。
/// </summary>
public static class SkillCastCost
{
    private const float LegacyMultiCastStepDemand = 0.03f;

    public static bool CanPay(ActorExtend caster, Entity skill, SkillCastPlan plan,
        SkillCastFundingSource source = SkillCastFundingSource.CasterResources)
    {
        if (plan == null || plan.Steps.Count == 0) return false;
        if (!SkillCastRequirements.Check(caster, skill, source)) return false;
        if (source == SkillCastFundingSource.Prepaid) return true;

        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        if (binding == null) return false;
        return CanPayDemand(caster, skill, binding, CalculatePlanDemand(skill, plan));
    }

    public static bool TryPay(ActorExtend caster, Entity skill, SkillCastPlan plan,
        SkillCastFundingSource source = SkillCastFundingSource.CasterResources)
    {
        using SkillCastPayment payment = TryBegin(caster, skill, plan, source);
        if (payment == null) return false;
        payment.Commit(); return true;
    }

    /// <summary>检查施法者是否能支付技能的一个实际发射步骤。</summary>
    public static bool CanPayStep(ActorExtend caster, Entity skill,
        SkillCastFundingSource source = SkillCastFundingSource.CasterResources)
    {
        if (!SkillCastRequirements.Check(caster, skill, source)) return false;
        if (source == SkillCastFundingSource.Prepaid) return true;

        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        return binding != null && CanPayDemand(caster, skill, binding, CalculateStepDemand(skill));
    }

    /// <summary>在一个步骤实际发射前原子支付该步骤的资源需求。</summary>
    public static bool TryPayStep(ActorExtend caster, Entity skill,
        SkillCastFundingSource source = SkillCastFundingSource.CasterResources)
    {
        using SkillCastPayment payment = TryBeginStep(caster, skill, source);
        if (payment == null) return false;
        payment.Commit(); return true;
    }

    /// <summary>准备序列的一次实际支付，创建失败时由调用方释放并撤销。</summary>
    /// <param name="caster">原支付者。</param><param name="skill">实际容器。</param><param name="plan">完整计划。</param><param name="source">费用来源。</param>
    internal static SkillCastPayment TryBegin(ActorExtend caster, Entity skill, SkillCastPlan plan, SkillCastFundingSource source)
    {
        if (plan == null || plan.Steps.Count == 0) return null;
        return BeginDemand(caster, skill, CalculatePlanDemand(skill, plan), source);
    }

    /// <summary>步骤真正进入执行入口时才扣费，拒绝、跳过和等待均不收费。</summary>
    /// <param name="caster">原支付者。</param><param name="skill">实际容器。</param><param name="source">费用来源。</param>
    internal static SkillCastPayment TryBeginStep(ActorExtend caster, Entity skill, SkillCastFundingSource source) =>
        BeginDemand(caster, skill, CalculateStepDemand(skill), source);

    /// <summary>预付只检查来源条件，不再次向人物收费；本次仍有明确受理边界。</summary>
    /// <param name="caster">原支付者。</param><param name="skill">实际容器。</param><param name="demand">当前需求。</param><param name="source">费用来源。</param>
    private static SkillCastPayment BeginDemand(ActorExtend caster, Entity skill, float demand, SkillCastFundingSource source)
    {
        if (!SkillCastRequirements.Check(caster, skill, source)) return null;
        if (source == SkillCastFundingSource.Prepaid)
            return SkillCastPayment.TryBegin(caster, Array.Empty<SkillCastResourceAsset>(), SkillCastResourceRequirementMode.Single, 0);
        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        if (binding == null) return null;
        demand *= Mathf.Sqrt(SkillEffectRadius.ResolveCasterGrowth(caster, skill));
        return SkillCastPayment.TryBegin(caster, binding.Resources, binding.Mode, demand);
    }

    public static int GetAffordableStepLimit(ActorExtend caster, Entity skill,
        SkillCastFundingSource source = SkillCastFundingSource.CasterResources)
    {
        if (!SkillCastRequirements.Check(caster, skill, source)) return 0;
        if (source == SkillCastFundingSource.Prepaid) return int.MaxValue;

        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        if (binding == null) return 0;

        var stepDemand = CalculateStepDemand(skill);
        if (stepDemand <= 0f) return int.MaxValue;

        if (binding.Mode == SkillCastResourceRequirementMode.AnyOf)
        {
            var alternativeLimit = 0;
            foreach (var resource in binding.Resources)
            {
                alternativeLimit = Math.Max(alternativeLimit,
                    CalculateResourceStepLimit(caster, skill, resource, stepDemand));
            }

            return alternativeLimit;
        }

        var costs = new Dictionary<string, (SkillCastResourceAsset Resource, double Cost)>(StringComparer.Ordinal);
        foreach (var resource in binding.Resources)
        {
            double cost = Quote(resource, caster, skill, stepDemand);
            if (costs.TryGetValue(resource.BalanceId, out var previous)) costs[resource.BalanceId] = (previous.Resource, previous.Cost + cost);
            else costs.Add(resource.BalanceId, (resource, cost));
        }
        var limit = int.MaxValue;
        foreach (var row in costs.Values)
            if (row.Cost > 0) limit = Math.Min(limit, (int)Math.Max(0d, Math.Min(int.MaxValue, Math.Floor(row.Resource.ReadAmount(caster) / row.Cost))));
        return limit;
    }

    public static float CalculatePlanDemand(Entity skill, SkillCastPlan plan)
    {
        if (plan == null || plan.Steps.Count == 0) return 0f;
        return CalculateStepDemand(skill) * plan.Steps.Count;
    }

    public static float CalculateStepDemand(Entity skill)
    {
        if (skill.IsNull) return 0f;

        var modifierDemand = CalculateModifierDemand(skill);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var container = skill.GetComponent<SkillContainer>();
        var demand = container.Asset.BaseCastDemand
                     + modifierDemand
                     + Mathf.Clamp(repeatBias + spreadBias, 0, 20) * LegacyMultiCastStepDemand;
        float baseRadius = container.Asset.AreaCostBaseRadius > 0f
            ? container.Asset.AreaCostBaseRadius
            : container.Asset.UseProfile.AreaCostBaseRadius;
        if (baseRadius > 0f)
        {
            float actualRadius = SkillEffectRadius.ResolveContainer(
                skill,
                container.Asset.ImpactProfile.EffectRadius *
                container.Asset.ImpactTuning.EffectRadiusMultiplier);
            float ratio = Mathf.Max(1f, actualRadius / baseRadius);
            demand *= ratio * ratio;
        }

        demand *= skill.GetComponent<SkillCastParameters>().CostMultiplier;
        if (skill.TryGetComponent(out Settings.SkillSettings settings))
        {
            demand *= settings.PowerMultiplier *
                      (baseRadius > 0f ? 1f : Mathf.Max(1f, settings.AreaMultiplier * settings.AreaMultiplier));
            if (settings.Duration > 0f)
                demand *= Mathf.Max(1f, settings.Duration / Mathf.Max(.05f, container.Asset.ImpactProfile.Lifetime));
            if (settings.ConductiveWater) demand *= 1.5f;
        }

        return demand;
    }

    private static bool CanPayDemand(ActorExtend caster, Entity skill, SkillCastResourceBinding binding,
        float demand)
    {
        demand *= Mathf.Sqrt(SkillEffectRadius.ResolveCasterGrowth(caster, skill));
        return SkillCastPayment.CanPay(caster, binding.Resources, binding.Mode, demand);
    }

    private static int CalculateResourceStepLimit(ActorExtend caster, Entity skill,
        SkillCastResourceAsset resource, float stepDemand)
    {
        var stepCost = Quote(resource, caster, skill, stepDemand);
        return stepCost <= 0f
            ? int.MaxValue
            : (int)Math.Max(0d, Math.Min(int.MaxValue, Math.Floor(resource.ReadAmount(caster) / stepCost)));
    }

    private static float Quote(SkillCastResourceAsset resource, ActorExtend caster, Entity skill, float demand)
    {
        // 境界带来的额外覆盖同时增加消耗，报价与实际执行使用同一成长来源。
        demand *= Mathf.Sqrt(SkillEffectRadius.ResolveCasterGrowth(caster, skill));
        return SkillCastPayment.Quote(caster, resource, demand);
    }

    private static float CalculateModifierDemand(Entity skill)
    {
        float demand = 0f;
        foreach (var componentType in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(componentType)) continue;
            var modifier = (IModifier)skill.GetComponent(componentType);
            demand += Mathf.Max(0f, modifier.ModifierAsset?.CastDemand ?? 0f);
        }

        return demand;
    }

}
