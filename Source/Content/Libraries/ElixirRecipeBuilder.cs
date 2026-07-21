using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.Semantics;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>一次运行时丹方构建的完整结果。</summary>
public sealed class ElixirRecipeDefinition
{
    public ElixirRecipeContext Context;
    public ElixirIngredientRequirement[] Ingredients;
    public string Signature;
    public string AssetId;
    public int Seed;
}

/// <summary>从材料实体构造语义丹方，并生成与材料顺序无关的稳定身份。</summary>
public static class ElixirRecipeBuilder
{
    private const int MaxSlotSemantics = 4;
    private const float SignatureStep = 0.05f;
    private const string RuntimeAssetPrefix = "Cultiway.Elixir.Runtime.";

    public static ElixirRecipeDefinition Build(Entity[] ingredients)
    {
        if (ingredients == null || ingredients.Length == 0)
            throw new ArgumentException("丹方至少需要一份材料", nameof(ingredients));

        var records = new IngredientRecord[ingredients.Length];
        var totalIngredientWeight = 0f;
        var totalQuality = 0f;
        for (var i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.IsNull) throw new ArgumentException("丹方中存在无效材料", nameof(ingredients));

            var level = ingredient.TryGetComponent(out ItemLevel itemLevel)
                ? Mathf.Clamp((int)itemLevel, 0, 35)
                : 0;
            var weight = 1f + level / 35f;
            records[i] = new IngredientRecord(IngredientSemanticService.Build(ingredient), itemLevel.Stage, weight);
            totalIngredientWeight += weight;
            totalQuality += level;
        }

        var context = new ElixirRecipeContext
        {
            ingredient_count = ingredients.Length,
            quality_stage = Mathf.Clamp(Mathf.FloorToInt(totalQuality / ingredients.Length / 9f), 0, 3),
            semantics = BuildContextSemantics(records, totalIngredientWeight)
        };

        var slots = records
            .Select(record => BuildSlot(record))
            .Select(slot => new SlotRecord(slot, BuildSlotSignature(slot)))
            .OrderBy(record => record.Signature, StringComparer.Ordinal)
            .ToArray();
        var signature = BuildSignature(context, slots);

        return new ElixirRecipeDefinition
        {
            Context = context,
            Ingredients = slots.Select(record => record.Requirement).ToArray(),
            Signature = signature,
            AssetId = RuntimeAssetPrefix + HashSignature(signature),
            Seed = NamingRuleUtils.StableHash(signature)
        };
    }

    private static SemanticContribution[] BuildContextSemantics(
        IngredientRecord[] records,
        float totalIngredientWeight)
    {
        Dictionary<(SemanticAsset Semantic, SemanticPolarity Polarity), float> scores = new();
        for (var i = 0; i < records.Length; i++)
        {
            var ingredientWeight = records[i].Weight / Mathf.Max(0.0001f, totalIngredientWeight);
            var evidence = records[i].Profile.Evidence;
            for (var j = 0; j < evidence.Count; j++)
            {
                var item = evidence[j];
                if (item.inferred || item.semantic == CultivationSemantics.Material.Quality) continue;
                var key = (item.semantic, item.polarity);
                scores.TryGetValue(key, out var current);
                scores[key] = current + item.strength * item.confidence * ingredientWeight;
            }
        }

        var total = scores.Values.Sum();
        if (total <= 0f) return [];
        return scores
            .Where(entry => entry.Value > 0f)
            .OrderBy(entry => entry.Key.Semantic.id, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Polarity)
            .Select(entry => new SemanticContribution(
                entry.Key.Semantic,
                entry.Value / total,
                1f,
                entry.Key.Polarity))
            .ToArray();
    }

    private static ElixirIngredientRequirement BuildSlot(IngredientRecord record)
    {
        Dictionary<(SemanticAsset Semantic, SemanticPolarity Polarity), float> scores = new();
        var evidence = record.Profile.Evidence;
        for (var i = 0; i < evidence.Count; i++)
        {
            var item = evidence[i];
            if (item.inferred || item.semantic == CultivationSemantics.Material.Quality) continue;
            var key = (item.semantic, item.polarity);
            scores.TryGetValue(key, out var current);
            scores[key] = current + item.strength * item.confidence;
        }

        var selected = scores
            .Where(entry => entry.Value > 0f)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key.Semantic.id, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.Polarity)
            .Take(MaxSlotSemantics)
            .ToArray();
        var total = selected.Sum(entry => entry.Value);
        var semantics = total <= 0f
            ? Array.Empty<ElixirSemanticRequirement>()
            : selected.Select(entry => new ElixirSemanticRequirement(
                entry.Key.Semantic,
                entry.Value / total,
                entry.Key.Polarity)).ToArray();

        List<SemanticAsset> required = new();
        AddRequired(record.Profile, CultivationSemantics.Trait.ElementRoot, required);
        AddRequired(record.Profile, CultivationSemantics.Realm.Jindan, required);
        return new ElixirIngredientRequirement
        {
            semantics = semantics,
            required_semantics = required.OrderBy(item => item.id, StringComparer.Ordinal).ToArray(),
            minimum_quality_stage = Mathf.Clamp(record.QualityStage, 0, 3)
        };
    }

    private static void AddRequired(SemanticProfile profile, SemanticAsset semantic, ICollection<SemanticAsset> result)
    {
        if (profile.GetScore(semantic, SemanticQueryPolicy.Default).Net >= 0.01f) result.Add(semantic);
    }

    private static string BuildSignature(ElixirRecipeContext context, SlotRecord[] slots)
    {
        var builder = new StringBuilder();
        builder.Append("count=").Append(context.ingredient_count)
            .Append(";stage=").Append(context.quality_stage)
            .Append(";context=");
        foreach (var semantic in context.semantics ?? Array.Empty<SemanticContribution>())
        {
            builder.Append(semantic.semantic_id)
                .Append(':').Append(semantic.polarity == SemanticPolarity.Positive ? '+' : '-')
                .Append(':').Append(Quantize(semantic.strength * semantic.confidence))
                .Append(',');
        }

        builder.Append(";slots=");
        for (var i = 0; i < slots.Length; i++)
        {
            if (i > 0) builder.Append('|');
            builder.Append(slots[i].Signature);
        }
        return builder.ToString();
    }

    private static string BuildSlotSignature(ElixirIngredientRequirement slot)
    {
        var required = string.Join(",", (slot.required_semantics ?? Array.Empty<SemanticAsset>())
            .Where(item => item != null)
            .Select(item => item.id)
            .OrderBy(id => id, StringComparer.Ordinal));
        var semantics = string.Join(",", (slot.semantics ?? Array.Empty<ElixirSemanticRequirement>())
            .Where(item => item.semantic != null && item.weight > 0f)
            .OrderBy(item => item.semantic.id, StringComparer.Ordinal)
            .ThenBy(item => item.polarity)
            .Select(item => $"{item.semantic.id}:{(item.polarity == SemanticPolarity.Positive ? '+' : '-')}:{Quantize(item.weight)}"));
        return $"q{slot.minimum_quality_stage}[{required}]({semantics})";
    }

    private static int Quantize(float value)
    {
        return Mathf.RoundToInt(value / SignatureStep);
    }

    private static string HashSignature(string signature)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(signature));
        return BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
    }

    private readonly struct IngredientRecord
    {
        public readonly SemanticProfile Profile;
        public readonly int QualityStage;
        public readonly float Weight;

        public IngredientRecord(SemanticProfile profile, int qualityStage, float weight)
        {
            Profile = profile;
            QualityStage = qualityStage;
            Weight = weight;
        }
    }

    private readonly struct SlotRecord
    {
        public readonly ElixirIngredientRequirement Requirement;
        public readonly string Signature;

        public SlotRecord(ElixirIngredientRequirement requirement, string signature)
        {
            Requirement = requirement;
            Signature = signature;
        }
    }
}
