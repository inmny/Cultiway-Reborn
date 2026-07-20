using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 语义证据对对象的作用范围。查询可以只观察其中一部分。
/// </summary>
[Flags]
public enum SemanticScope
{
    None       = 0,
    Intrinsic  = 1 << 0,
    Learned    = 1 << 1,
    Equipped   = 1 << 2,
    Historical = 1 << 3,
    Contextual = 1 << 4,
    All        = Intrinsic | Learned | Equipped | Historical | Contextual
}

/// <summary>
/// 语义证据的方向。负向证据表示排斥或反证，不等同于语义本身不存在。
/// </summary>
public enum SemanticPolarity : sbyte
{
    Negative = -1,
    Positive = 1
}

/// <summary>
/// 语义证据的来源定位。贡献器 ID 表示来源系统，资产和实体用于追溯具体对象。
/// </summary>
public readonly struct SemanticSourceRef
{
    public readonly string contributor_id;
    public readonly string asset_id;
    public readonly Entity entity;
    public readonly bool has_entity;

    public SemanticSourceRef(string contributorId, string assetId = null)
    {
        contributor_id = contributorId;
        asset_id = assetId;
        entity = default;
        has_entity = false;
    }

    public SemanticSourceRef(string contributorId, Entity entity, string assetId = null)
    {
        contributor_id = contributorId;
        asset_id = assetId;
        this.entity = entity;
        has_entity = true;
    }
}

/// <summary>
/// 资产声明的一条语义贡献。序列化时仅保存规范 ID，运行时由语义库解析。
/// </summary>
public struct SemanticContribution
{
    public string semantic_id;
    public float strength;
    public float confidence;
    public SemanticPolarity polarity;

    public SemanticContribution(
        SemanticAsset semantic,
        float strength = 1f,
        float confidence = 1f,
        SemanticPolarity polarity = SemanticPolarity.Positive)
        : this(semantic.id, strength, confidence, polarity)
    {
    }

    public SemanticContribution(
        string semanticId,
        float strength = 1f,
        float confidence = 1f,
        SemanticPolarity polarity = SemanticPolarity.Positive)
    {
        semantic_id = semanticId;
        this.strength = strength;
        this.confidence = confidence;
        this.polarity = polarity;
    }
}

/// <summary>
/// 可挂到任意资产上的语义描述，不携带具体来源和作用范围。
/// </summary>
public sealed class SemanticDescriptor
{
    public SemanticContribution[] contributions = Array.Empty<SemanticContribution>();

    public static SemanticDescriptor Of(params SemanticAsset[] semantics)
    {
        return new SemanticDescriptor
        {
            contributions = semantics.Select(x => new SemanticContribution(x)).ToArray()
        };
    }

    public static SemanticDescriptor Weighted(params SemanticContribution[] contributions)
    {
        return new SemanticDescriptor { contributions = contributions ?? Array.Empty<SemanticContribution>() };
    }

    public IEnumerable<SemanticAsset> Resolve(SemanticLibrary library)
    {
        for (var i = 0; i < contributions.Length; i++)
        {
            if (library.TryResolve(contributions[i].semantic_id, out var semantic)) yield return semantic;
        }
    }

    /// <summary>
    /// 将直接语义及其父级、蕴含语义加入目标集合。
    /// </summary>
    public void CollectExpanded(SemanticLibrary library, ISet<SemanticAsset> result)
    {
        for (var i = 0; i < contributions.Length; i++)
        {
            if (contributions[i].polarity != SemanticPolarity.Positive) continue;
            var expansion = library.Expand(library.Resolve(contributions[i].semantic_id));
            for (var j = 0; j < expansion.Count; j++) result.Add(expansion[j].semantic);
        }
    }

    /// <summary>
    /// 判断直接语义及其父级、蕴含语义中是否包含目标。
    /// </summary>
    public bool ContainsExpanded(SemanticLibrary library, SemanticAsset target)
    {
        for (var i = 0; i < contributions.Length; i++)
        {
            if (contributions[i].polarity != SemanticPolarity.Positive) continue;
            var expansion = library.Expand(library.Resolve(contributions[i].semantic_id));
            for (var j = 0; j < expansion.Count; j++)
            {
                if (expansion[j].semantic == target) return true;
            }
        }
        return false;
    }
}

/// <summary>
/// 组合多个资产语义时使用的确定性构建器。相同语义与方向的强度会累加，输出始终按规范 ID 排序。
/// </summary>
public sealed class SemanticDescriptorBuilder
{
    private readonly List<SemanticContribution> contributions = new();

    public SemanticDescriptorBuilder Add(
        SemanticAsset semantic,
        float strength = 1f,
        float confidence = 1f,
        SemanticPolarity polarity = SemanticPolarity.Positive)
    {
        contributions.Add(new SemanticContribution(semantic, strength, confidence, polarity));
        return this;
    }

    public SemanticDescriptorBuilder Add(SemanticContribution contribution, float multiplier = 1f)
    {
        contribution.strength *= multiplier;
        contributions.Add(contribution);
        return this;
    }

    public SemanticDescriptorBuilder Add(SemanticDescriptor descriptor, float multiplier = 1f)
    {
        for (var i = 0; i < descriptor.contributions.Length; i++)
            Add(descriptor.contributions[i], multiplier);
        return this;
    }

    public SemanticDescriptor Build()
    {
        return SemanticDescriptor.Weighted(contributions
            .Where(x => !string.IsNullOrEmpty(x.semantic_id) && x.strength > 0f && x.confidence > 0f)
            .GroupBy(x => (x.semantic_id, x.polarity))
            .Select(group =>
            {
                var strength = group.Sum(x => x.strength);
                var confidence = group.Sum(x => x.strength * x.confidence) / strength;
                return new SemanticContribution(
                    group.Key.semantic_id,
                    strength,
                    confidence,
                    group.Key.polarity);
            })
            .OrderBy(x => x.semantic_id, StringComparer.Ordinal)
            .ThenBy(x => x.polarity)
            .ToArray());
    }
}

/// <summary>
/// 由全有、任一和全无三组条件组成的语义表达式，可复用于选择、冲突和协同规则。
/// </summary>
public sealed class SemanticQueryExpression
{
    public string[] all = Array.Empty<string>();
    public string[] any = Array.Empty<string>();
    public string[] none = Array.Empty<string>();

    public static SemanticQueryExpression Has(SemanticAsset semantic)
    {
        return All(semantic);
    }

    public static SemanticQueryExpression All(params SemanticAsset[] semantics)
    {
        return new SemanticQueryExpression { all = semantics.Select(x => x.id).ToArray() };
    }

    public static SemanticQueryExpression Any(params SemanticAsset[] semantics)
    {
        return new SemanticQueryExpression { any = semantics.Select(x => x.id).ToArray() };
    }

    public static SemanticQueryExpression None(params SemanticAsset[] semantics)
    {
        return new SemanticQueryExpression { none = semantics.Select(x => x.id).ToArray() };
    }

    public bool Matches(ISet<SemanticAsset> semantics, SemanticLibrary library)
    {
        for (var i = 0; i < all.Length; i++)
        {
            if (!library.TryResolve(all[i], out var semantic) || !semantics.Contains(semantic)) return false;
        }

        if (any.Length > 0)
        {
            var matched = false;
            for (var i = 0; i < any.Length; i++)
            {
                if (library.TryResolve(any[i], out var semantic) && semantics.Contains(semantic))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched) return false;
        }

        for (var i = 0; i < none.Length; i++)
        {
            if (library.TryResolve(none[i], out var semantic) && semantics.Contains(semantic)) return false;
        }

        return true;
    }
}
