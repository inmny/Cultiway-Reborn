using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Semantics;

/// <summary>
/// 全局规范语义库。负责身份解析、旧键别名、关系校验和蕴含闭包编译。
/// </summary>
public sealed class SemanticLibrary : AssetLibrary<SemanticAsset>
{
    private readonly Dictionary<string, SemanticAsset> byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<SemanticAsset, SemanticExpansion[]> expansions = new();

    public int Revision { get; private set; }

    public override void init()
    {
        SkillSemantics.Register(this);
        LinkAndValidate();
    }

    public void LinkAndValidate()
    {
        byKey.Clear();
        expansions.Clear();

        foreach (var semantic in list.OrderBy(x => x.id, StringComparer.Ordinal))
        {
            AddKey(semantic.id, semantic);
            for (var i = 0; i < semantic.aliases.Length; i++) AddKey(semantic.aliases[i], semantic);

            semantic.Facet = ModClass.L.SemanticFacetLibrary.get(semantic.facet_id);
            if (semantic.Facet == null)
                throw new InvalidOperationException($"语义 {semantic.id} 引用了不存在的维度 {semantic.facet_id}");
        }

        foreach (var semantic in list)
        {
            ValidateRelations(semantic);
            expansions[semantic] = CompileExpansion(semantic);
        }

        Revision++;
    }

    public bool TryResolve(string idOrAlias, out SemanticAsset semantic)
    {
        if (string.IsNullOrEmpty(idOrAlias))
        {
            semantic = null;
            return false;
        }
        return byKey.TryGetValue(idOrAlias, out semantic);
    }

    public SemanticAsset Resolve(string idOrAlias)
    {
        if (TryResolve(idOrAlias, out var semantic)) return semantic;
        throw new KeyNotFoundException($"未注册语义或别名: {idOrAlias}");
    }

    public IReadOnlyList<SemanticExpansion> Expand(SemanticAsset semantic)
    {
        return expansions.TryGetValue(semantic, out var result)
            ? result
            : Array.Empty<SemanticExpansion>();
    }

    private void AddKey(string key, SemanticAsset semantic)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (byKey.TryGetValue(key, out var existing) && existing != semantic)
            throw new InvalidOperationException($"语义键 {key} 同时指向 {existing.id} 和 {semantic.id}");
        byKey[key] = semantic;
    }

    private void ValidateRelations(SemanticAsset semantic)
    {
        for (var i = 0; i < semantic.parent_ids.Length; i++) Resolve(semantic.parent_ids[i]);
        for (var i = 0; i < semantic.implications.Length; i++)
        {
            var implication = semantic.implications[i];
            Resolve(implication.semantic_id);
            if (implication.strength <= 0f || implication.strength > 1f)
                throw new InvalidOperationException(
                    $"语义 {semantic.id} 到 {implication.semantic_id} 的蕴含强度必须位于 (0, 1]");
        }
    }

    private SemanticExpansion[] CompileExpansion(SemanticAsset origin)
    {
        var strengths = new Dictionary<SemanticAsset, float> { [origin] = 1f };
        var visiting = new HashSet<SemanticAsset>();
        Visit(origin, 1f, origin, strengths, visiting);
        return strengths
            .OrderBy(x => x.Key.id, StringComparer.Ordinal)
            .Select(x => new SemanticExpansion(x.Key, x.Value, x.Key != origin))
            .ToArray();
    }

    private void Visit(
        SemanticAsset current,
        float pathStrength,
        SemanticAsset origin,
        Dictionary<SemanticAsset, float> strengths,
        HashSet<SemanticAsset> visiting)
    {
        if (!visiting.Add(current))
            throw new InvalidOperationException($"语义 {origin.id} 的父级或蕴含关系存在环");

        for (var i = 0; i < current.parent_ids.Length; i++)
            VisitEdge(Resolve(current.parent_ids[i]), pathStrength, origin, strengths, visiting);

        for (var i = 0; i < current.implications.Length; i++)
        {
            var implication = current.implications[i];
            VisitEdge(Resolve(implication.semantic_id), pathStrength * implication.strength,
                origin, strengths, visiting);
        }

        visiting.Remove(current);
    }

    private void VisitEdge(
        SemanticAsset target,
        float strength,
        SemanticAsset origin,
        Dictionary<SemanticAsset, float> strengths,
        HashSet<SemanticAsset> visiting)
    {
        if (visiting.Contains(target))
            throw new InvalidOperationException($"语义 {origin.id} 的父级或蕴含关系存在环");
        if (strengths.TryGetValue(target, out var previous) && strength <= previous) return;
        strengths[target] = strength;
        Visit(target, strength, origin, strengths, visiting);
    }
}

/// <summary>
/// 一条已经编译好的语义展开结果。
/// </summary>
public readonly struct SemanticExpansion
{
    public readonly SemanticAsset semantic;
    public readonly float strength;
    public readonly bool inferred;

    public SemanticExpansion(SemanticAsset semantic, float strength, bool inferred)
    {
        this.semantic = semantic;
        this.strength = Mathf.Clamp01(strength);
        this.inferred = inferred;
    }
}
