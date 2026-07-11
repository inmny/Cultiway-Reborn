using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 统一解析和叠加法术实体的作用半径倍率。
/// </summary>
public static class SkillEffectRadius
{
    public static float Resolve(Entity skillEntity, float baseRadius)
    {
        return baseRadius * skillEntity.GetComponent<EffectRadiusScale>().Value;
    }

    public static void Multiply(Entity skillEntity, float multiplier)
    {
        ref var radiusScale = ref skillEntity.GetComponent<EffectRadiusScale>();
        radiusScale.Value *= multiplier;
    }

    public static float ResolveContainer(Entity skillContainer, float baseRadius)
    {
        var multiplier = 1f;
        foreach (var componentType in skillContainer.GetComponentTypes())
        {
            var modifier = ModClass.I.SkillV3.ModifierLib.GetByComponentType(componentType);
            if (modifier == null || modifier.EffectRadiusMultiplier == null) continue;
            multiplier *= modifier.EffectRadiusMultiplier(skillContainer);
        }
        return baseRadius * multiplier;
    }
}
