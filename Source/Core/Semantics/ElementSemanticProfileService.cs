using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 将连续元素组成转换为统一的带权语义证据，供技能、材料和兼容接口共享。
/// </summary>
public static class ElementSemanticProfileService
{
    private const string ContributorId = "core.element_composition";

    /// <summary>为一组元素组成构建只包含固有元素证据的语义档案。</summary>
    public static SemanticProfile Build(ElementComposition composition)
    {
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        Contribute(builder, composition, 1f, SemanticScope.Intrinsic,
            new SemanticSourceRef(ContributorId));
        return builder.Build();
    }

    /// <summary>按归一化后的元素比例向现有语义档案写入元素证据。</summary>
    public static void Contribute(
        SemanticProfileBuilder builder,
        ElementComposition composition,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        var total = Mathf.Max(0f, composition.iron)
                    + Mathf.Max(0f, composition.wood)
                    + Mathf.Max(0f, composition.water)
                    + Mathf.Max(0f, composition.fire)
                    + Mathf.Max(0f, composition.earth)
                    + Mathf.Max(0f, composition.neg)
                    + Mathf.Max(0f, composition.pos)
                    + Mathf.Max(0f, composition.entropy);
        if (total <= 0f)
        {
            builder.Add(SkillSemantics.Element.Generic, multiplier, scope, source);
            return;
        }

        Add(builder, SkillSemantics.Element.Iron, composition.iron / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Wood, composition.wood / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Water, composition.water / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Fire, composition.fire / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Earth, composition.earth / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Neg, composition.neg / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Pos, composition.pos / total, multiplier, scope, source);
        Add(builder, SkillSemantics.Element.Entropy, composition.entropy / total, multiplier, scope, source);
    }

    private static void Add(
        SemanticProfileBuilder builder,
        SemanticAsset semantic,
        float value,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        var strength = Mathf.Max(0f, value);
        if (strength > 0f) builder.Add(semantic, strength * multiplier, scope, source);
    }
}
