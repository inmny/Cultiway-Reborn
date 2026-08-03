using System;
using Cultiway.Const;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 将连续元素组成转换为统一的带权语义证据，供技能、材料和兼容接口共享。
/// </summary>
public static class ElementSemanticProfileService
{
    private const string ContributorId = "core.element_composition";

    /// <summary>为连续元素组成和可选的具名元素描述构建固有语义档案。</summary>
    public static SemanticProfile Build(
        ElementComposition composition,
        SemanticDescriptor descriptor = null)
    {
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        if (descriptor != null)
            builder.Add(descriptor, 1f, SemanticScope.Intrinsic, new SemanticSourceRef(ContributorId));
        Contribute(builder, composition, 1f, SemanticScope.Intrinsic,
            new SemanticSourceRef(ContributorId));
        return builder.Build();
    }

    /// <summary>为灵根的具名语义与连续元素组成构建统一档案。</summary>
    public static SemanticProfile Build(in ElementRoot root)
    {
        return Build(ToComposition(root), root.Type.Semantics);
    }

    /// <summary>从元素组成和可选的具名语义中解析得分最高的元素语义。</summary>
    public static SemanticAsset ResolveDominant(
        ElementComposition composition,
        SemanticDescriptor descriptor = null)
    {
        var ranked = Build(composition, descriptor).GetRanked(
            SemanticQueryPolicy.Default,
            ModClass.L.SemanticFacetLibrary.Element);
        return ranked.Count > 0 ? ranked[0].semantic : SkillSemantics.Element.Generic;
    }

    /// <summary>从灵根的具名语义与连续组成中解析得分最高的元素语义。</summary>
    public static SemanticAsset ResolveDominant(in ElementRoot root)
    {
        var ranked = Build(root).GetRanked(
            SemanticQueryPolicy.Default,
            ModClass.L.SemanticFacetLibrary.Element);
        return ranked.Count > 0 ? ranked[0].semantic : SkillSemantics.Element.Generic;
    }

    /// <summary>
    /// 尝试把描述中八种规范元素语义的净权重还原为连续元素组成。
    /// 描述没有任何正向元素证据时返回 false，不凭空补入通用元素。
    /// </summary>
    public static bool TryResolveComposition(
        SemanticDescriptor descriptor,
        out ElementComposition composition)
    {
        composition = default;
        if (descriptor == null) return false;

        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        builder.Add(descriptor, 1f, SemanticScope.Intrinsic,
            new SemanticSourceRef(ContributorId));
        SemanticProfile profile = builder.Build();
        var total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++)
        {
            float value = Mathf.Max(0f,
                profile.GetScore(GetIndexedSemantic(i), SemanticQueryPolicy.Default).Net);
            composition[i] = value;
            total += value;
        }

        if (total <= 0.0001f)
        {
            composition = default;
            return false;
        }

        for (var i = 0; i < ElementIndex.Count; i++) composition[i] /= total;
        return true;
    }

    /// <summary>返回八维元素组成中指定索引对应的规范元素语义。</summary>
    public static SemanticAsset GetIndexedSemantic(int index)
    {
        return index switch
        {
            ElementIndex.Iron    => SkillSemantics.Element.Iron,
            ElementIndex.Wood    => SkillSemantics.Element.Wood,
            ElementIndex.Water   => SkillSemantics.Element.Water,
            ElementIndex.Fire    => SkillSemantics.Element.Fire,
            ElementIndex.Earth   => SkillSemantics.Element.Earth,
            ElementIndex.Neg     => SkillSemantics.Element.Neg,
            ElementIndex.Pos     => SkillSemantics.Element.Pos,
            ElementIndex.Entropy => SkillSemantics.Element.Entropy,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
        };
    }

    /// <summary>按归一化后的元素比例向现有语义档案写入元素证据。</summary>
    public static void Contribute(
        SemanticProfileBuilder builder,
        ElementComposition composition,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        var total = 0f;
        for (var i = 0; i < ElementIndex.Count; i++) total += Mathf.Max(0f, composition[i]);
        if (total <= 0f)
        {
            builder.Add(SkillSemantics.Element.Generic, multiplier, scope, source);
            return;
        }

        for (var i = 0; i < ElementIndex.Count; i++)
            Add(builder, GetIndexedSemantic(i), composition[i] / total, multiplier, scope, source);
    }

    /// <summary>将灵根的具名语义和连续元素组成一并写入现有语义档案。</summary>
    public static void Contribute(
        SemanticProfileBuilder builder,
        in ElementRoot root,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        builder.Add(root.Type.Semantics, multiplier, scope, source);
        Contribute(builder, ToComposition(root), multiplier, scope, source);
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

    /// <summary>将灵根组件转换为通用八维元素组成。</summary>
    public static ElementComposition ToComposition(in ElementRoot root)
    {
        return new ElementComposition(
            root.Iron,
            root.Wood,
            root.Water,
            root.Fire,
            root.Earth,
            root.Neg,
            root.Pos,
            root.Entropy);
    }
}
