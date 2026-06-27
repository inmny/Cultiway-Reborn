using Cultiway.Content.Components;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public enum SkillCastCostSource
{
    CasterWakan,
    Prepaid
}

public static class SkillCastCost
{
    private const float BaseStepCost = 1f;
    private const float ModifierStepCost = 0.1f;
    private const float LegacyMultiCastStepCost = 0.03f;

    public static bool CanPay(ActorExtend caster, Entity skill, SkillCastPlan plan,
        SkillCastCostSource source = SkillCastCostSource.CasterWakan)
    {
        if (plan == null || plan.Steps.Count == 0) return false;
        if (source == SkillCastCostSource.Prepaid) return true;
        if (caster == null || !caster.HasCultisys<Xian>()) return true;

        return caster.GetCultisys<Xian>().wakan >= CalculatePlanWakanCost(caster, skill, plan);
    }

    public static bool TryPay(ActorExtend caster, Entity skill, SkillCastPlan plan,
        SkillCastCostSource source = SkillCastCostSource.CasterWakan)
    {
        if (plan == null || plan.Steps.Count == 0) return false;
        if (source == SkillCastCostSource.Prepaid) return true;
        if (caster == null || !caster.HasCultisys<Xian>()) return true;

        var cost = CalculatePlanWakanCost(caster, skill, plan);
        ref var xian = ref caster.GetCultisys<Xian>();
        if (xian.wakan < cost) return false;

        xian.wakan -= cost;
        return true;
    }

    public static int GetAffordableStepLimit(ActorExtend caster, Entity skill,
        SkillCastCostSource source = SkillCastCostSource.CasterWakan)
    {
        if (source == SkillCastCostSource.Prepaid) return int.MaxValue;
        if (caster == null || !caster.HasCultisys<Xian>()) return int.MaxValue;

        var stepCost = CalculateStepWakanCost(skill);
        if (stepCost <= 0f) return int.MaxValue;

        return Mathf.Max(0, Mathf.FloorToInt(caster.GetCultisys<Xian>().wakan / stepCost));
    }

    public static float CalculatePlanWakanCost(ActorExtend caster, Entity skill, SkillCastPlan plan)
    {
        if (caster == null || !caster.HasCultisys<Xian>()) return 0f;
        if (plan == null || plan.Steps.Count == 0) return 0f;

        return CalculateStepWakanCost(skill) * plan.Steps.Count;
    }

    public static float CalculateStepWakanCost(Entity skill)
    {
        if (skill.IsNull) return 0f;

        var modifierCount = CountModifiers(skill);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var cost = BaseStepCost
                   + modifierCount * ModifierStepCost
                   + Mathf.Clamp(repeatBias + spreadBias, 0, 20) * LegacyMultiCastStepCost;
        if (skill.TryGetComponent(out ProficiencyModifier proficiency))
        {
            cost *= Mathf.Clamp(1f - proficiency.CostReduction, 0.1f, 1f);
        }
        return cost;
    }

    private static int CountModifiers(Entity skill)
    {
        if (skill.IsNull) return 0;

        var count = 0;
        foreach (var componentType in skill.GetComponentTypes())
        {
            if (typeof(IModifier).IsAssignableFrom(componentType))
            {
                count++;
            }
        }

        return count;
    }
}
