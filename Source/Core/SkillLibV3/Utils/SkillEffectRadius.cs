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
}
