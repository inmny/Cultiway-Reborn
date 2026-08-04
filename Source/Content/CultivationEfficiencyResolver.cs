using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>一次修炼效率解析的完整分项，供结算和展示共用。</summary>
public readonly struct CultivationEfficiencyResult
{
    /// <summary>八项灵根强度等权饱和后的 0..1 分值。</summary>
    public readonly float Intensity;

    /// <summary>当前组成相对于已判定灵根原型的 0..1 纯度。</summary>
    public readonly float Purity;

    /// <summary>灵根组成和元素语义对主修功法的 0..1 契合度。</summary>
    public readonly float MainCultibookAffinity;

    /// <summary>只由灵根资质产生的倍率，范围为 0.5..3.5；无灵根时为 1。</summary>
    public readonly float AptitudeMultiplier;

    /// <summary>修炼方式根据环境和事件给出的倍率。</summary>
    public readonly float MethodMultiplier;

    /// <summary>资质倍率与修炼方式倍率的乘积。</summary>
    public readonly float FinalMultiplier;

    public CultivationEfficiencyResult(
        float intensity,
        float purity,
        float mainCultibookAffinity,
        float aptitudeMultiplier,
        float methodMultiplier)
    {
        Intensity = intensity;
        Purity = purity;
        MainCultibookAffinity = mainCultibookAffinity;
        AptitudeMultiplier = aptitudeMultiplier;
        MethodMultiplier = methodMultiplier;
        FinalMultiplier = aptitudeMultiplier * methodMultiplier;
    }
}

/// <summary>统一解析灵根资质、主修契合与修炼方式环境倍率。</summary>
public static class CultivationEfficiencyResolver
{
    /// <summary>缺少具体主修功法时采用的中性契合度。</summary>
    public const float NeutralAffinity = 0.5f;

    private const float CommonPurityScale = 0.35f;
    private const float IntensityWeight = 0.35f;
    private const float PurityWeight = 0.3f;
    private const float AffinityWeight = 0.35f;
    private const float MinimumAptitudeMultiplier = 0.5f;
    private const float AptitudeMultiplierRange = 3f;

    /// <summary>按角色当前主修功法和对应修炼方式解析最终倍率。</summary>
    public static CultivationEfficiencyResult Resolve(ActorExtend actor)
    {
        var cultibook = actor.GetMainCultibook();
        var method = cultibook?.GetCultivateMethod() ?? CultivateMethods.Standard;
        return Resolve(actor, cultibook, method);
    }

    /// <summary>使用调用方已经取得的功法和修炼方式解析最终倍率。</summary>
    public static CultivationEfficiencyResult Resolve(
        ActorExtend actor,
        CultibookAsset cultibook,
        CultivateMethodAsset method)
    {
        var methodMultiplier = Mathf.Max(0f, method?.GetMethodMultiplier?.Invoke(actor) ?? 1f);
        if (!actor.HasElementRoot())
            return new CultivationEfficiencyResult(0f, 0f, NeutralAffinity, 1f, methodMultiplier);

        ref var root = ref actor.GetElementRoot();
        var affinity = ElementRootAffinityResolver.Resolve(root, cultibook?.ElementReq,
            cultibook?.Semantics).Combined;
        return Resolve(root, affinity, methodMultiplier);
    }

    /// <summary>
    /// 在没有具体角色上下文时，按给定的主修契合度与修炼方式倍率解析灵根修炼效率。
    /// </summary>
    public static CultivationEfficiencyResult Resolve(
        in ElementRoot root,
        float mainCultibookAffinity,
        float methodMultiplier)
    {
        var intensity = ResolveIntensity(root);
        var purity = ResolvePurity(root);
        var affinity = Mathf.Clamp01(mainCultibookAffinity);
        var aptitude = Mathf.Clamp(
            MinimumAptitudeMultiplier + AptitudeMultiplierRange *
            (intensity * IntensityWeight + purity * PurityWeight + affinity * AffinityWeight),
            MinimumAptitudeMultiplier,
            MinimumAptitudeMultiplier + AptitudeMultiplierRange);
        return new CultivationEfficiencyResult(
            intensity,
            purity,
            affinity,
            aptitude,
            Mathf.Max(0f, methodMultiplier));
    }

    /// <summary>将八项非负强度等权转换为不会爆炸增长的 0..1 分值。</summary>
    private static float ResolveIntensity(in ElementRoot root)
    {
        var total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++) total += Mathf.Max(0f, root[i]);
        return 1f - Mathf.Exp(-total / ElementIndex.Count);
    }

    /// <summary>按命中原型的独立纯度基准归一化；五行灵根的纯度上限固定为 0.35。</summary>
    private static float ResolvePurity(in ElementRoot root)
    {
        var profile = root.Type.Archetype;
        if (root.Type == ModClass.L.ElementRootLibrary.Common)
            return Mathf.Clamp01(root.TypeSimilarity) * CommonPurityScale;

        return Mathf.Clamp01(
            (root.TypeSimilarity - profile.PuritySimilarityBaseline) /
            (1f - profile.PuritySimilarityBaseline));
    }
}

/// <summary>计算灵根与功法或法术目标之间的组成和元素语义契合。</summary>
public static class ElementRootAffinityResolver
{
    private const float NeutralAffinity = 0.5f;
    private const float CompositionWeight = 0.8f;
    private const float SemanticWeight = 0.2f;

    /// <summary>组成契合、元素语义契合以及两者的最终结果。</summary>
    public readonly struct Result
    {
        public readonly float Composition;
        public readonly float Semantic;
        public readonly float Combined;

        public Result(float composition, float semantic, float combined)
        {
            Composition = composition;
            Semantic = semantic;
            Combined = combined;
        }
    }

    /// <summary>解析灵根对目标元素需求和语义描述的契合度。</summary>
    public static Result Resolve(
        in ElementRoot root,
        ElementRequirement? requirement,
        SemanticDescriptor targetSemantics)
    {
        var composition = requirement?.GetCompositionAffinity(root) ?? NeutralAffinity;
        if (!SemanticDescriptorResolver.TryGetAffinity(
                root.Type.Semantics,
                targetSemantics,
                ModClass.L.SemanticLibrary,
                ModClass.L.SemanticFacetLibrary.Element,
                out var semantic))
            return new Result(composition, NeutralAffinity, composition);

        var combined = composition * CompositionWeight + semantic * SemanticWeight;
        return new Result(composition, semantic, combined);
    }

    /// <summary>解析灵根对一本功法的组成与原型契合。</summary>
    public static Result Resolve(in ElementRoot root, CultibookAsset cultibook)
    {
        return Resolve(root, cultibook?.ElementReq, cultibook?.Semantics);
    }

    /// <summary>直接计算灵根八维组成与任意目标组成的余弦相似度，不依赖灵根命名类型。</summary>
    public static float ResolveCompositionSimilarity(
        in ElementRoot root,
        in ElementComposition target)
    {
        float dot = 0f;
        float rootLengthSquared = 0f;
        float targetLengthSquared = 0f;
        for (var i = 0; i < ElementIndex.Count; i++)
        {
            float rootValue = Mathf.Max(0f, root[i]);
            float targetValue = Mathf.Max(0f, target[i]);
            dot += rootValue * targetValue;
            rootLengthSquared += rootValue * rootValue;
            targetLengthSquared += targetValue * targetValue;
        }

        if (rootLengthSquared <= 0f || targetLengthSquared <= 0f) return 0f;
        return Mathf.Clamp01(dot / Mathf.Sqrt(rootLengthSquared * targetLengthSquared));
    }

    /// <summary>把灵根综合强度转换为用于概率和选择权重的连续饱和因子。</summary>
    public static float ResolveStrengthFactor(in ElementRoot root)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0f, root.GetStrength() - 1f));
    }
}
