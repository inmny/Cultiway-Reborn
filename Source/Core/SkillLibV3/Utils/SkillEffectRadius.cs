using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 统一解析和叠加法术实体的作用半径倍率。
/// </summary>
public static class SkillEffectRadius
{
    /// <summary>由内容层提供的真实境界范围成长，技能、报价和预览共用同一规则。</summary>
    public static System.Func<ActorExtend, Entity, float> CasterGrowth;

    /// <summary>读取当前施法者对应的范围倍率；没有内容规则时保持原范围。</summary>
    /// <param name="caster">实际支付者。</param><param name="skill">真实技能容器。</param>
    public static float ResolveCasterGrowth(ActorExtend caster, Entity skill) => CasterGrowth?.Invoke(caster,skill) ?? 1f;

    /// <summary>解析词条和境界共同决定的范围，供战术选择与范围预览使用。</summary>
    /// <param name="caster">实际施法者。</param><param name="skill">技能容器。</param><param name="baseRadius">原始作用半径。</param>
    public static float ResolveForCaster(ActorExtend caster, Entity skill, float baseRadius) =>
        ResolveContainer(skill,baseRadius)*ResolveCasterGrowth(caster,skill);

    /// <summary>真实范围结算与外观共同读取已经应用法术调整的半径。</summary>
    /// <param name="entity">真实执行体。</param>
    public static float Effect(Entity entity)
    {
        var asset = entity.GetComponent<SkillEntity>().Asset;
        return Resolve(entity, asset.ImpactProfile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
    }

    /// <summary>真实碰撞体的半径，不混用落点范围。</summary>
    /// <param name="entity">真实执行体。</param>
    public static float Collision(Entity entity) => entity.TryGetComponent(out ColliderSphere collider) ? Resolve(entity, collider.Radius) : 0f;

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
        if(skillContainer.TryGetComponent(out Settings.SkillSettings settings))multiplier*=settings.AreaMultiplier;
        return baseRadius * multiplier;
    }
}
