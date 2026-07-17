using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 一条已经归属到角色的语义证据，完整保留来源和推导信息。
/// </summary>
public readonly struct SemanticEvidence
{
    public readonly SemanticAsset semantic;
    public readonly SemanticAsset origin;
    public readonly float strength;
    public readonly float confidence;
    public readonly SemanticScope scope;
    public readonly SemanticPolarity polarity;
    public readonly SemanticSourceRef source;
    public readonly bool inferred;

    public SemanticEvidence(
        SemanticAsset semantic,
        SemanticAsset origin,
        float strength,
        float confidence,
        SemanticScope scope,
        SemanticPolarity polarity,
        SemanticSourceRef source,
        bool inferred)
    {
        this.semantic = semantic;
        this.origin = origin;
        this.strength = strength;
        this.confidence = confidence;
        this.scope = scope;
        this.polarity = polarity;
        this.source = source;
        this.inferred = inferred;
    }
}

/// <summary>
/// 聚合后的语义分值。正向和负向证据分别保存，避免丢失冲突信息。
/// </summary>
public readonly struct SemanticScore
{
    public readonly float positive;
    public readonly float negative;

    public float Net => positive - negative;

    public SemanticScore(float positive, float negative)
    {
        this.positive = positive;
        this.negative = negative;
    }
}

/// <summary>
/// 查询语义档案时使用的范围和阈值策略。
/// </summary>
public readonly struct SemanticQueryPolicy
{
    public static readonly SemanticQueryPolicy Default = new(SemanticScope.All, 0.01f, 0f);

    public readonly SemanticScope scopes;
    public readonly float minimum_score;
    public readonly float minimum_confidence;

    public SemanticQueryPolicy(SemanticScope scopes, float minimumScore = 0.01f, float minimumConfidence = 0f)
    {
        this.scopes = scopes;
        minimum_score = minimumScore;
        minimum_confidence = minimumConfidence;
    }
}

/// <summary>
/// 一个时点上的角色语义档案。它是派生缓存，不参与存档。
/// </summary>
public sealed class SemanticProfile
{
    private readonly SemanticEvidence[] evidence;

    internal SemanticProfile(SemanticEvidence[] evidence)
    {
        this.evidence = evidence;
    }

    public IReadOnlyList<SemanticEvidence> Evidence => evidence;

    public SemanticScore GetScore(SemanticAsset semantic, SemanticQueryPolicy policy)
    {
        var positive = 0f;
        var negative = 0f;
        for (var i = 0; i < evidence.Length; i++)
        {
            var item = evidence[i];
            if (item.semantic != semantic || (item.scope & policy.scopes) == 0 ||
                item.confidence < policy.minimum_confidence) continue;
            var value = item.strength * item.confidence;
            if (item.polarity == SemanticPolarity.Positive) positive += value;
            else negative += value;
        }
        return new SemanticScore(positive, negative);
    }

    public bool Has(SemanticAsset semantic, SemanticQueryPolicy policy)
    {
        return GetScore(semantic, policy).Net >= policy.minimum_score;
    }

    public bool Matches(SemanticQueryExpression expression, SemanticQueryPolicy policy)
    {
        var set = new HashSet<SemanticAsset>(GetRanked(policy).Select(x => x.semantic));
        return expression.Matches(set, ModClass.L.SemanticLibrary);
    }

    public IReadOnlyList<SemanticRank> GetRanked(SemanticQueryPolicy policy, SemanticFacetAsset facet = null)
    {
        return evidence.Select(x => x.semantic)
            .Distinct()
            .Where(x => facet == null || x.Facet == facet)
            .Select(x => new SemanticRank(x, GetScore(x, policy)))
            .Where(x => x.score.Net >= policy.minimum_score)
            .OrderByDescending(x => x.score.Net)
            .ThenBy(x => x.semantic.id, StringComparer.Ordinal)
            .ToArray();
    }
}

public readonly struct SemanticRank
{
    public readonly SemanticAsset semantic;
    public readonly SemanticScore score;

    public SemanticRank(SemanticAsset semantic, SemanticScore score)
    {
        this.semantic = semantic;
        this.score = score;
    }
}

/// <summary>
/// 贡献器构建角色档案时使用的写入器。每条原始证据只按最强路径展开一次。
/// </summary>
public sealed class SemanticProfileBuilder
{
    private readonly SemanticLibrary library;
    private readonly List<SemanticEvidence> evidence = new();

    public SemanticProfileBuilder(SemanticLibrary library)
    {
        this.library = library;
    }

    public void Add(
        SemanticAsset semantic,
        float strength,
        SemanticScope scope,
        SemanticSourceRef source,
        float confidence = 1f,
        SemanticPolarity polarity = SemanticPolarity.Positive)
    {
        if (strength <= 0f || confidence <= 0f) return;
        var expansions = library.Expand(semantic);
        for (var i = 0; i < expansions.Count; i++)
        {
            var expansion = expansions[i];
            evidence.Add(new SemanticEvidence(
                expansion.semantic,
                semantic,
                strength * expansion.strength,
                confidence,
                scope,
                polarity,
                source,
                expansion.inferred));
        }
    }

    public void Add(
        SemanticDescriptor descriptor,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        if (descriptor == null) return;
        for (var i = 0; i < descriptor.contributions.Length; i++)
        {
            var contribution = descriptor.contributions[i];
            Add(library.Resolve(contribution.semantic_id), contribution.strength * multiplier, scope, source,
                contribution.confidence, contribution.polarity);
        }
    }

    public SemanticProfile Build()
    {
        return new SemanticProfile(evidence
            .OrderBy(x => x.semantic.id, StringComparer.Ordinal)
            .ThenBy(x => x.source.contributor_id, StringComparer.Ordinal)
            .ThenBy(x => x.source.asset_id, StringComparer.Ordinal)
            .ToArray());
    }
}

/// <summary>
/// 供调试窗口和日志使用的确定性档案文本格式化器。
/// </summary>
public static class SemanticProfileFormatter
{
    public static string Format(SemanticProfile profile, SemanticQueryPolicy? policy = null)
    {
        var queryPolicy = policy ?? SemanticQueryPolicy.Default;
        var builder = new StringBuilder();
        foreach (var rank in profile.GetRanked(queryPolicy))
        {
            builder.Append(rank.semantic.id)
                .Append(" = ")
                .Append(rank.score.Net.ToString("0.###"))
                .Append(" (+")
                .Append(rank.score.positive.ToString("0.###"))
                .Append(" / -")
                .Append(rank.score.negative.ToString("0.###"))
                .AppendLine(")");
        }
        return builder.ToString();
    }
}
