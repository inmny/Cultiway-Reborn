using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;
using FormTag = Cultiway.Core.SkillLibV3.SkillTags.Form;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 技能等级评估的可组合中间量。具体词条只负责贡献自己拥有的语义。
/// </summary>
public struct SkillEvaluationContext
{
    public float DirectPower { get; private set; }
    public float AdditionalPower { get; private set; }
    public float ExpectedTargets { get; private set; }
    public float Control { get; private set; }
    public float Utility { get; private set; }
    public float Complexity { get; private set; }

    internal SkillEvaluationContext(float expectedTargets)
    {
        DirectPower = 1f;
        AdditionalPower = 0f;
        ExpectedTargets = Mathf.Max(1f, expectedTargets);
        Control = 0f;
        Utility = 0f;
        Complexity = 0f;
    }

    public void MultiplyDirectPower(float multiplier)
    {
        DirectPower *= Mathf.Max(0f, multiplier);
    }

    public void AddAdditionalPower(float value)
    {
        AdditionalPower += Mathf.Max(0f, value);
    }

    public void AtLeastExpectedTargets(float value)
    {
        ExpectedTargets = Mathf.Max(ExpectedTargets, Mathf.Max(1f, value));
    }

    public void MultiplyExpectedTargets(float multiplier)
    {
        ExpectedTargets *= Mathf.Max(0f, multiplier);
    }

    public void AddControl(float value)
    {
        Control += Mathf.Max(0f, value);
    }

    public void AddUtility(float value)
    {
        Utility += Mathf.Max(0f, value);
    }

    internal void AddComplexity(float value)
    {
        Complexity += Mathf.Max(0f, value);
    }
}

public readonly struct SkillEvaluationResult
{
    public readonly ItemLevel ItemLevel;
    public readonly float ResourceDemandPerStep;
    public readonly float PowerScore;
    public readonly float Complexity;
    public readonly float ExpectedTargets;

    public SkillEvaluationResult(ItemLevel itemLevel, float resourceDemandPerStep, float powerScore, float complexity,
        float expectedTargets)
    {
        ItemLevel = itemLevel;
        ResourceDemandPerStep = resourceDemandPerStep;
        PowerScore = powerScore;
        Complexity = complexity;
        ExpectedTargets = expectedTargets;
    }
}

/// <summary>
/// 汇总 SkillEntityAsset 的基础语义与各 modifier asset 的评估委托，并维护容器上的 ItemLevel。
/// </summary>
public static class SkillContainerEvaluator
{
    private const float PowerScoreScale = 6f;
    private const float ResourceDemandScale = 2f;
    private const float ComplexityScale = 1.5f;

    /// <summary>
    /// 重新评估技能容器并写入或覆盖其 ItemLevel 组件。
    /// </summary>
    public static bool Refresh(Entity container)
    {
        if (!TryEvaluate(container, out var result)) return false;

        if (container.HasComponent<ItemLevel>())
        {
            ref var level = ref container.GetComponent<ItemLevel>();
            level = result.ItemLevel;
        }
        else
        {
            container.AddComponent(result.ItemLevel);
        }
        return true;
    }

    /// <summary>
    /// 使用各 modifier asset 注册的委托评估技能容器，不修改实体。
    /// </summary>
    public static bool TryEvaluate(Entity container, out SkillEvaluationResult result)
    {
        result = default;
        if (container.IsNull || !container.HasComponent<SkillContainer>()) return false;

        var asset = container.GetComponent<SkillContainer>().Asset;
        if (asset == null) return false;

        var expectedTargets = asset.SeriesTags.Contains(FormTag.Aoe) ? 3f : 1f;
        var context = new SkillEvaluationContext(expectedTargets);
        foreach (var componentType in container.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(componentType)) continue;
            var modifier = (IModifier)container.GetComponent(componentType);
            var modifierAsset = modifier.ModifierAsset;
            if (modifierAsset == null) continue;

            context.AddComplexity(1f + (int)modifierAsset.Rarity);
            modifierAsset.EvaluateLevel(container, ref context);
        }

        var resourceDemandPerStep = container.HasComponent<SkillCastParameters>()
            ? Mathf.Ceil(Mathf.Max(0f, SkillCastCost.CalculateStepDemand(container)))
            : 1f;
        var powerScore = Mathf.Max(0.1f, context.DirectPower + context.AdditionalPower) *
                         Mathf.Sqrt(Mathf.Max(1f, context.ExpectedTargets)) + context.Control + context.Utility;
        var itemLevelValue = Mathf.RoundToInt(
            Mathf.Log(Mathf.Max(1f, powerScore), 2f) * PowerScoreScale +
            Mathf.Log(Mathf.Max(1f, resourceDemandPerStep), 2f) * ResourceDemandScale +
            Mathf.Sqrt(context.Complexity) * ComplexityScale);

        result = new SkillEvaluationResult(ItemLevel.FromValue(itemLevelValue), resourceDemandPerStep, powerScore,
            context.Complexity, context.ExpectedTargets);
        return true;
    }
}

public static class SkillEvaluationActions
{
    /// <summary>
    /// 显式表示该词条只贡献通用复杂度，没有额外的伤害、控制或效用语义。
    /// </summary>
    public static void None(Entity container, ref SkillEvaluationContext context)
    {
    }
}
