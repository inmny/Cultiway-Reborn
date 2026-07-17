using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public class ElixirEffectAtomAsset : Asset
{
    /// <summary>丹药效果组合器用于去重和提示的稳定技术键。</summary>
    public string effect_key;
    public string[] name_stems = [];
    public string description_fragment;
    public string effect_sentence;
    public string[] keywords = [];
    public Dictionary<string, float> status_stats = new();
    public Dictionary<string, float> data_attributes = new();
    public string[] data_traits = [];
    public string[] data_operations = [];
    public Dictionary<string, string> operation_args;
    public Func<ElixirRecipeContext, float> ScoreRecipe;

    public float ScoreFor(ElixirRecipeContext recipe, string text)
    {
        var score = Mathf.Max(0f, ScoreRecipe?.Invoke(recipe) ?? 0f);
        if (!string.IsNullOrEmpty(text) && text.ContainsAny(keywords))
        {
            score += 1.5f;
        }
        return score;
    }

    public string PickNameStem(int seed)
    {
        if (name_stems == null || name_stems.Length == 0) return string.Empty;
        return name_stems[Math.Abs(seed) % name_stems.Length];
    }
}
