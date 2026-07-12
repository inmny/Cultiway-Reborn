using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
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
    private const float BaseStepDemand = 1f;
    private const float ModifierStepDemand = 0.1f;
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
        if (plan == null || plan.Steps.Count == 0) return false;
        if (!SkillCastRequirements.Check(caster, skill, source)) return false;
        if (source == SkillCastFundingSource.Prepaid) return true;

        var binding = SkillCastResourceResolver.Resolve(caster, skill);
        if (binding == null) return false;

        var demand = CalculatePlanDemand(skill, plan);
        var payments = new List<ResourcePayment>(binding.Resources.Count);
        if (binding.Mode == SkillCastResourceRequirementMode.AnyOf)
        {
            foreach (var resource in binding.Resources)
            {
                if (!TryCreatePayment(caster, skill, resource, demand, out var payment)) continue;
                payments.Add(payment);
                break;
            }
            if (payments.Count == 0) return false;
        }
        else
        {
            foreach (var resource in binding.Resources)
            {
                if (!TryCreatePayment(caster, skill, resource, demand, out var payment)) return false;
                payments.Add(payment);
            }
        }

        foreach (var payment in payments)
        {
            payment.Resource.WriteAmount(caster, payment.OriginalAmount - payment.Cost);
        }
        return true;
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

        var limit = int.MaxValue;
        foreach (var resource in binding.Resources)
        {
            limit = Math.Min(limit, CalculateResourceStepLimit(caster, skill, resource, stepDemand));
        }
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

        var modifierCount = CountModifiers(skill);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var demand = BaseStepDemand
                     + modifierCount * ModifierStepDemand
                     + Mathf.Clamp(repeatBias + spreadBias, 0, 20) * LegacyMultiCastStepDemand;
        demand *= skill.GetComponent<SkillCastParameters>().CostMultiplier;
        return demand;
    }

    private static bool CanPayDemand(ActorExtend caster, Entity skill, SkillCastResourceBinding binding,
        float demand)
    {
        if (binding.Mode == SkillCastResourceRequirementMode.AnyOf)
        {
            foreach (var resource in binding.Resources)
            {
                if (CanPayResource(caster, skill, resource, demand)) return true;
            }
            return false;
        }

        foreach (var resource in binding.Resources)
        {
            if (!CanPayResource(caster, skill, resource, demand)) return false;
        }
        return true;
    }

    private static bool CanPayResource(ActorExtend caster, Entity skill, SkillCastResourceAsset resource,
        float demand)
    {
        return resource.ReadAmount(caster) >= Quote(resource, caster, skill, demand);
    }

    private static bool TryCreatePayment(ActorExtend caster, Entity skill, SkillCastResourceAsset resource,
        float demand, out ResourcePayment payment)
    {
        var amount = resource.ReadAmount(caster);
        var cost = Quote(resource, caster, skill, demand);
        if (amount < cost)
        {
            payment = default;
            return false;
        }

        payment = new ResourcePayment(resource, amount, cost);
        return true;
    }

    private static int CalculateResourceStepLimit(ActorExtend caster, Entity skill,
        SkillCastResourceAsset resource, float stepDemand)
    {
        var stepCost = Quote(resource, caster, skill, stepDemand);
        return stepCost <= 0f
            ? int.MaxValue
            : Mathf.Max(0, Mathf.FloorToInt(resource.ReadAmount(caster) / stepCost));
    }

    private static float Quote(SkillCastResourceAsset resource, ActorExtend caster, Entity skill, float demand)
    {
        var quoted = resource.Quote(caster, skill, demand);
        if (float.IsNaN(quoted) || float.IsInfinity(quoted))
        {
            throw new InvalidOperationException($"施法资源 {resource.id} 返回了无效报价");
        }
        return Mathf.Max(0f, quoted);
    }

    private static int CountModifiers(Entity skill)
    {
        var count = 0;
        foreach (var componentType in skill.GetComponentTypes())
        {
            if (typeof(IModifier).IsAssignableFrom(componentType)) count++;
        }
        return count;
    }

    private readonly struct ResourcePayment
    {
        public readonly SkillCastResourceAsset Resource;
        public readonly float OriginalAmount;
        public readonly float Cost;

        public ResourcePayment(SkillCastResourceAsset resource, float originalAmount, float cost)
        {
            Resource = resource;
            OriginalAmount = originalAmount;
            Cost = cost;
        }
    }
}
