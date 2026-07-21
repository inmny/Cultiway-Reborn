using System;
using System.Collections.Generic;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public enum ElixirAtomEffectMode
{
    Adaptive,
    DataGain,
    Restore
}

public enum ElixirDataGainKind
{
    Attribute,
    Trait,
    OneTime
}

/// <summary>一个 atom 对一条规范语义的适配权重。</summary>
public readonly struct ElixirSemanticAffinity
{
    public readonly SemanticAsset semantic;
    public readonly float weight;

    public ElixirSemanticAffinity(SemanticAsset semantic, float weight)
    {
        this.semantic = semantic;
        this.weight = weight;
    }
}

public class ElixirEffectAtomAsset : Asset
{
    public string effect_key;
    public string[] name_stems = [];
    public string description_fragment;
    public string effect_sentence;
    public Dictionary<string, float> status_stats = new();
    public Dictionary<string, float> data_attributes = new();
    public string[] data_traits = [];
    public OperationAsset[] data_operations = [];
    public Dictionary<string, string> operation_args;
    public SemanticAsset[] required_semantics = [];
    public ElixirSemanticAffinity[] semantic_affinities = [];
    public int minimum_quality_stage;
    public float base_score;
    public ElixirAtomEffectMode effect_mode;
    public float data_gain_chance;
    public ElixirDataGainKind data_gain_kind;

    public float ScoreFor(ElixirRecipeContext context)
    {
        if (context.quality_stage < minimum_quality_stage) return 0f;
        for (var i = 0; i < required_semantics.Length; i++)
        {
            if (!context.HasSemantic(required_semantics[i])) return 0f;
        }

        var score = Mathf.Max(0f, base_score);
        for (var i = 0; i < semantic_affinities.Length; i++)
        {
            var affinity = semantic_affinities[i];
            if (affinity.semantic == null || affinity.weight <= 0f) continue;
            score += Mathf.Max(0f, context.GetSemanticScore(affinity.semantic).Net) * affinity.weight;
        }
        return score;
    }

    public string PickNameStem(int seed)
    {
        if (name_stems == null || name_stems.Length == 0) return string.Empty;
        return name_stems[(seed & int.MaxValue) % name_stems.Length];
    }
}
