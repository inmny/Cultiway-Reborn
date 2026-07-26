using Cultiway.Core.Combat;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 一次施法序列向所有执行体传递的类型化运行参数。
/// 它只描述本次释放，不写入可持久化的技能容器。
/// </summary>
public struct SkillCastRuntimeData
{
    /// <summary>伤害、状态强度和击退共同使用的效果倍率；非正数按 1 处理。</summary>
    public float EffectScale;

    /// <summary>是否以 <see cref="ElementOverride"/> 覆盖技能资产的固定元素。</summary>
    public bool HasElementOverride;

    /// <summary>五相轮转、混沌回响等技能本次释放采用的动态元素。</summary>
    public ElementComposition ElementOverride;

    /// <summary>该执行体造成的伤害在后续结算中采用的来源语义。</summary>
    public DamageOrigin DamageOrigin;

    /// <summary>返回规范化后的效果倍率。</summary>
    public readonly float ResolveEffectScale()
    {
        return EffectScale > 0f ? EffectScale : 1f;
    }

    /// <summary>存在动态覆盖时返回覆盖元素，否则返回技能资产的默认元素。</summary>
    public readonly ElementComposition ResolveElement(ElementComposition fallback)
    {
        return HasElementOverride ? ElementOverride : fallback;
    }

    /// <summary>构造一份带效果倍率和伤害来源的运行参数。</summary>
    public static SkillCastRuntimeData Create(float effectScale, DamageOrigin damageOrigin)
    {
        return new SkillCastRuntimeData
        {
            EffectScale = effectScale,
            DamageOrigin = damageOrigin,
        };
    }

    /// <summary>构造一份同时覆盖元素的运行参数。</summary>
    public static SkillCastRuntimeData Create(
        float effectScale,
        DamageOrigin damageOrigin,
        ElementComposition element)
    {
        return new SkillCastRuntimeData
        {
            EffectScale = effectScale,
            DamageOrigin = damageOrigin,
            HasElementOverride = true,
            ElementOverride = element,
        };
    }
}
