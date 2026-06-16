using Cultiway.Content.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public static class SkillCastCost
{
    private const float BaseStepCost = 1f;
    private const float ModifierStepCost = 0.2f;
    private const float LegacyMultiCastStepCost = 0.1f;

    public static bool TryConsumeStepWakan(ActorExtend caster, Entity skill)
    {
        if (caster == null || !caster.HasCultisys<Xian>()) return true;

        var cost = CalculateStepWakanCost(caster, skill);
        ref var xian = ref caster.GetCultisys<Xian>();
        if (xian.wakan < cost) return false;

        xian.wakan -= cost;
        return true;
    }

    public static float CalculateStepWakanCost(ActorExtend caster, Entity skill)
    {
        if (caster == null || !caster.HasCultisys<Xian>()) return 0f;

        var modifierCount = CountModifiers(skill);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        return BaseStepCost
               + modifierCount * ModifierStepCost
               + Mathf.Clamp(repeatBias + spreadBias, 0, 20) * LegacyMultiCastStepCost;
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
