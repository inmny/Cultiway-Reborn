using System;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 构建法术容器时汇总具体词条贡献的通用施法参数。
/// </summary>
public static class SkillCastParametersResolver
{
    public static SkillCastParameters Refresh(Entity skill)
    {
        var parameters = SkillCastParameters.Default;
        foreach (var componentType in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(componentType)) continue;
            var modifier = (IModifier)skill.GetComponent(componentType);
            modifier.ModifierAsset.ApplyCastParameters?.Invoke(skill, ref parameters);
        }

        parameters.CostMultiplier = Math.Max(0.1f, parameters.CostMultiplier);
        parameters.SalvoIntervalMultiplier = Math.Max(0.25f, parameters.SalvoIntervalMultiplier);
        if (skill.HasComponent<SkillCastParameters>())
        {
            skill.GetComponent<SkillCastParameters>() = parameters;
        }
        else
        {
            skill.AddComponent(parameters);
        }
        return parameters;
    }
}
