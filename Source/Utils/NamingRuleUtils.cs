using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Utils;

internal static class NamingRuleUtils
{
    internal const int NoElement = -1;

    internal static ItemLevel CalculateQuality(float powerLevel, int xianLevel, float elementStrength, float jindanStrength)
    {
        var score = Mathf.Max(powerLevel, xianLevel * 2f);
        score += Mathf.Log(Mathf.Max(1f, elementStrength) + 1f) * 2f;
        score += Mathf.Log(Mathf.Max(1f, jindanStrength) + 1f) * 2f;
        var value = Mathf.Clamp(Mathf.RoundToInt(score), 0, 35);
        return new ItemLevel
        {
            Stage = Mathf.Clamp(value / 9, 0, 3),
            Level = Mathf.Clamp(value % 9, 0, 8)
        };
    }

    internal static string LocalizeElement(int elementIndex)
    {
        if (elementIndex < ElementIndex.Iron || elementIndex > ElementIndex.Entropy) return string.Empty;
        return Localize(ElementIndex.ElementNames[elementIndex]);
    }

    internal static int GetPrimaryElement(ElementRoot root, out float value)
    {
        var values = GetElementValues(root);
        return GetMaxIndex(values, out value);
    }

    internal static int GetSecondaryElement(ElementRoot root, int primary, out float value)
    {
        var values = GetElementValues(root);
        return GetSecondMaxIndex(values, primary, out value);
    }

    internal static float[] GetElementValues(ElementRoot root)
    {
        return
        [
            root.Iron,
            root.Wood,
            root.Water,
            root.Fire,
            root.Earth,
            root.Neg,
            root.Pos,
            root.Entropy
        ];
    }

    internal static int GetMaxIndex(float[] values, out float value)
    {
        var idx = NoElement;
        value = 0f;
        if (values == null) return idx;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] <= value) continue;
            value = values[i];
            idx = i;
        }
        return idx;
    }

    internal static int GetSecondMaxIndex(float[] values, int primary, out float value)
    {
        var idx = NoElement;
        value = 0f;
        if (values == null) return idx;
        for (var i = 0; i < values.Length; i++)
        {
            if (i == primary || values[i] <= value) continue;
            value = values[i];
            idx = i;
        }
        return idx;
    }

    internal static void AddElementValue(float[] values, int index, float value)
    {
        if (values == null || index < 0 || index >= values.Length || value <= 0f) return;
        values[index] += value;
    }

    internal static void Increment(Dictionary<string, int> dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key)) return;
        dict.TryGetValue(key, out var count);
        dict[key] = count + 1;
    }

    internal static string PickTopKey(Dictionary<string, int> dict)
    {
        if (dict == null || dict.Count == 0) return string.Empty;
        return dict.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key;
    }

    internal static string GetSourceDisplayName(Actor actor)
    {
        if (actor == null) return string.Empty;
        var locale = actor.asset?.name_locale;
        if (!string.IsNullOrEmpty(locale) && LM.Has(locale)) return LM.Get(locale);
        if (!string.IsNullOrEmpty(actor.asset?.id) && LM.Has(actor.asset.id)) return LM.Get(actor.asset.id);
        return actor.asset?.id ?? actor.getName();
    }

    internal static string GetSourceDisplayName(string sourceAssetId, string fallbackName = null)
    {
        if (string.IsNullOrEmpty(sourceAssetId)) return fallbackName ?? string.Empty;
        var asset = AssetManager.actor_library.get(sourceAssetId);
        if (asset != null)
        {
            if (!string.IsNullOrEmpty(asset.name_locale) && LM.Has(asset.name_locale)) return LM.Get(asset.name_locale);
            if (LM.Has(asset.id)) return LM.Get(asset.id);
        }

        if (LM.Has(sourceAssetId)) return LM.Get(sourceAssetId);
        return !string.IsNullOrEmpty(fallbackName) ? fallbackName : sourceAssetId;
    }

    internal static bool IsPlantSource(string sourceAssetId)
    {
        return !string.IsNullOrEmpty(sourceAssetId) && Actors.Plant != null && sourceAssetId == Actors.Plant.id;
    }

    internal static void ApplyElementRoot(IngredientNamingContext context, ElementRoot root)
    {
        if (context == null) return;
        context.ElementRootId = root.Type?.id ?? string.Empty;
        context.ElementStrength = root.GetStrength();
        context.PrimaryElementIndex = GetPrimaryElement(root, out var primaryValue);
        context.PrimaryElementValue = primaryValue;
        context.SecondaryElementIndex = GetSecondaryElement(root, context.PrimaryElementIndex, out var secondaryValue);
        context.SecondaryElementValue = secondaryValue;
    }

    /// <summary>把金丹名称、强度和元素组成合并进材料命名上下文。</summary>
    internal static void ApplyJindan(IngredientNamingContext context, Jindan jindan)
    {
        if (context == null) return;
        context.JindanName = jindan.GetName();
        context.JindanStrength = jindan.strength;

        var composition = jindan.GetComposition().AsArray();
        var primary = GetMaxIndex(composition, out var primaryValue);
        var secondary = GetSecondMaxIndex(composition, primary, out var secondaryValue);
        MergeElementCandidate(context, primary, primaryValue);
        MergeElementCandidate(context, secondary, secondaryValue);
    }

    /// <summary>将一个元素候选并入上下文，并始终维持主、次元素按强度降序排列。</summary>
    private static void MergeElementCandidate(IngredientNamingContext context, int index, float value)
    {
        if (index == NoElement || value <= 0f) return;
        if (index == context.PrimaryElementIndex)
        {
            context.PrimaryElementValue = Mathf.Max(context.PrimaryElementValue, value);
            return;
        }
        if (index == context.SecondaryElementIndex)
        {
            context.SecondaryElementValue = Mathf.Max(context.SecondaryElementValue, value);
            if (context.SecondaryElementValue <= context.PrimaryElementValue) return;
            (context.PrimaryElementIndex, context.SecondaryElementIndex) =
                (context.SecondaryElementIndex, context.PrimaryElementIndex);
            (context.PrimaryElementValue, context.SecondaryElementValue) =
                (context.SecondaryElementValue, context.PrimaryElementValue);
            return;
        }
        if (context.PrimaryElementIndex == NoElement || value > context.PrimaryElementValue)
        {
            context.SecondaryElementIndex = context.PrimaryElementIndex;
            context.SecondaryElementValue = context.PrimaryElementValue;
            context.PrimaryElementIndex = index;
            context.PrimaryElementValue = value;
            return;
        }
        if (context.SecondaryElementIndex == NoElement || value > context.SecondaryElementValue)
        {
            context.SecondaryElementIndex = index;
            context.SecondaryElementValue = value;
        }
    }

    internal static string Localize(string key)
    {
        return string.IsNullOrEmpty(key) ? string.Empty : LM.Has(key) ? LM.Get(key) : key;
    }

    internal static string TrimKnownSuffix(string value, params string[] suffixes)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value.Substring(0, value.Length - suffix.Length);
            }
        }
        return value;
    }

    internal static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "无名丹药";
        var text = value.Trim();
        text = text.Replace("五行", "");
        text = text.Replace("五灵", "");
        text = text.Replace("普通", "");
        text = text.Replace("修士", "");
        while (text.Contains("灵灵")) text = text.Replace("灵灵", "灵");
        while (text.Contains("玄玄")) text = text.Replace("玄玄", "玄");
        return string.IsNullOrEmpty(text) ? "灵材" : text;
    }

    internal static string LimitNameLength(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.Length <= maxLength ? name : name.Substring(0, maxLength);
    }

    internal static string Pick(int seed, params string[] values)
    {
        if (values == null || values.Length == 0) return string.Empty;
        return values[Math.Abs(seed) % values.Length];
    }

    internal static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            value ??= string.Empty;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return (int)(hash & 0x7fffffff);
        }
    }
}
