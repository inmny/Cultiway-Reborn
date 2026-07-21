using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Const;
using Cultiway.Content.Semantics;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using Cultiway.Utils;
using strings;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public static class ElixirEffectComposer
{
    private const string FallbackAtomId = "Cultiway.ElixirEffectAtom.Body";
    private const float StageDataGainChance = 0.06f;

    private static readonly SemanticAsset[] ElementSemantics =
    [
        SkillSemantics.Element.Iron,
        SkillSemantics.Element.Wood,
        SkillSemantics.Element.Water,
        SkillSemantics.Element.Ice,
        SkillSemantics.Element.Poison,
        SkillSemantics.Element.Fire,
        SkillSemantics.Element.Earth,
        SkillSemantics.Element.Neg,
        SkillSemantics.Element.Pos,
        SkillSemantics.Element.Entropy,
        SkillSemantics.Element.Wind,
        SkillSemantics.Element.Lightning,
        SkillSemantics.Element.Generic
    ];

    public static ElixirEffectComposition Compose(ElixirAsset elixir)
    {
        if (elixir == null) throw new ArgumentNullException(nameof(elixir));
        var recipe = elixir.recipe_context;
        var seed = elixir.composition_seed;
        var atoms = SelectAtoms(recipe, seed, TargetAtomCount(recipe, seed)).ToArray();
        var composition = new ElixirEffectComposition
        {
            Atoms = atoms,
            Name = ComposeName(recipe, atoms, seed),
            Description = ComposeDescription(atoms),
            EffectType = ResolveEffectType(recipe, atoms, seed)
        };

        switch (composition.EffectType)
        {
            case ElixirEffectType.StatusGain:
                composition.StatusStats = ComposeStatusStats(recipe, atoms);
                break;
            case ElixirEffectType.DataGain:
                ComposeDataGain(composition, recipe, atoms);
                break;
        }
        return composition;
    }

    private static IReadOnlyList<ElixirEffectAtomAsset> SelectAtoms(
        ElixirRecipeContext recipe,
        int seed,
        int targetCount)
    {
        var library = Manager.ElixirEffectAtomLibrary;
        var scored = library.list
            .Select(atom => new ScoredAtom(atom, atom.ScoreFor(recipe)))
            .Where(entry => entry.Score > 0f)
            .Select(entry => new ScoredAtom(
                entry.Atom,
                entry.Score + TieBreak(seed, entry.Atom.id)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Atom.id, StringComparer.Ordinal)
            .ToArray();
        if (scored.Length == 0)
        {
            var fallback = library.get(FallbackAtomId);
            return fallback == null ? Array.Empty<ElixirEffectAtomAsset>() : [fallback];
        }

        var primary = scored[0].Atom;
        if (primary.effect_mode == ElixirAtomEffectMode.Restore ||
            primary.effect_mode == ElixirAtomEffectMode.DataGain &&
            primary.data_gain_kind == ElixirDataGainKind.OneTime)
        {
            return [primary];
        }

        List<ElixirEffectAtomAsset> selected = new();
        HashSet<string> effectKeys = new(StringComparer.Ordinal);
        for (var i = 0; i < scored.Length && selected.Count < targetCount; i++)
        {
            var atom = scored[i].Atom;
            if (selected.Count > 0 && atom.effect_mode != ElixirAtomEffectMode.Adaptive) continue;
            var effectKey = atom.effect_key ?? atom.id;
            if (!effectKeys.Add(effectKey)) continue;
            selected.Add(atom);
        }
        return selected;
    }

    private static ElixirEffectType ResolveEffectType(
        ElixirRecipeContext recipe,
        ElixirEffectAtomAsset[] atoms,
        int seed)
    {
        if (atoms.Any(atom => atom.effect_mode == ElixirAtomEffectMode.Restore))
            return ElixirEffectType.Restore;
        if (atoms.Any(atom => atom.effect_mode == ElixirAtomEffectMode.DataGain))
            return ElixirEffectType.DataGain;

        var baseChance = atoms.Length == 0 ? 0f : atoms.Max(atom => atom.data_gain_chance);
        var chance = Mathf.Clamp01(baseChance + recipe.quality_stage * StageDataGainChance);
        return Sample01(seed, "data_gain") < chance
            ? ElixirEffectType.DataGain
            : ElixirEffectType.StatusGain;
    }

    private static int TargetAtomCount(ElixirRecipeContext recipe, int seed)
    {
        var count = 1;
        if (recipe.quality_stage >= 1 || recipe.ingredient_count >= 3) count++;
        if (recipe.quality_stage >= 3 || recipe.ingredient_count >= 4 && seed % 3 == 0) count++;
        return Mathf.Clamp(count, 1, 3);
    }

    private static string ComposeName(ElixirRecipeContext recipe, ElixirEffectAtomAsset[] atoms, int seed)
    {
        var quality = PickQualityPrefix(recipe, seed);
        var element = PickElementPrefix(recipe, seed);
        var primary = atoms.Length > 0 ? atoms[0].PickNameStem(seed) : "固元";
        var secondary = atoms.Length > 1 && seed % 2 == 0
            ? atoms[1].PickNameStem(seed / 7 + 17)
            : string.Empty;
        var name = NamingRuleUtils.NormalizeName($"{quality}{element}{primary}{secondary}丹");
        if (name.Length <= 10) return name;
        name = NamingRuleUtils.NormalizeName($"{quality}{primary}丹");
        return name.Length <= 10 ? name : NamingRuleUtils.NormalizeName($"{primary}丹");
    }

    private static string PickQualityPrefix(ElixirRecipeContext recipe, int seed)
    {
        if (recipe.quality_stage >= 3) return NamingRuleUtils.Pick(seed, "太清", "无垢", "九转");
        if (recipe.quality_stage == 2 && seed % 2 == 0) return NamingRuleUtils.Pick(seed, "九转", "玄", "天元");
        return string.Empty;
    }

    private static string PickElementPrefix(ElixirRecipeContext recipe, int seed)
    {
        if (seed % 3 == 1) return string.Empty;
        var element = GetDominantElement(recipe);
        if (element == SkillSemantics.Element.Iron) return NamingRuleUtils.Pick(seed, "庚金", "玄金", "金");
        if (element == SkillSemantics.Element.Wood) return NamingRuleUtils.Pick(seed, "青木", "生", "木");
        if (element == SkillSemantics.Element.Water) return NamingRuleUtils.Pick(seed, "玄水", "寒", "水");
        if (element == SkillSemantics.Element.Ice) return NamingRuleUtils.Pick(seed, "玄冰", "霜", "寒");
        if (element == SkillSemantics.Element.Poison) return NamingRuleUtils.Pick(seed, "蚀毒", "瘴", "毒");
        if (element == SkillSemantics.Element.Fire) return NamingRuleUtils.Pick(seed, "赤火", "炎", "赤");
        if (element == SkillSemantics.Element.Earth) return NamingRuleUtils.Pick(seed, "厚土", "地", "黄");
        if (element == SkillSemantics.Element.Neg) return NamingRuleUtils.Pick(seed, "幽阴", "幽", "玄");
        if (element == SkillSemantics.Element.Pos) return NamingRuleUtils.Pick(seed, "阳华", "曜", "明");
        if (element == SkillSemantics.Element.Entropy) return NamingRuleUtils.Pick(seed, "混沌", "浊", "玄");
        if (element == SkillSemantics.Element.Wind) return NamingRuleUtils.Pick(seed, "罡风", "风", "迅");
        if (element == SkillSemantics.Element.Lightning) return NamingRuleUtils.Pick(seed, "天雷", "雷", "霆");
        return element == SkillSemantics.Element.Generic ? "灵" : string.Empty;
    }

    private static SemanticAsset GetDominantElement(ElixirRecipeContext recipe)
    {
        SemanticAsset result = null;
        var bestScore = 0.01f;
        for (var i = 0; i < ElementSemantics.Length; i++)
        {
            var semantic = ElementSemantics[i];
            var score = recipe.GetSemanticScore(semantic).Net;
            if (score < bestScore) continue;
            if (Mathf.Approximately(score, bestScore) && result != null &&
                string.CompareOrdinal(semantic.id, result.id) >= 0) continue;
            result = semantic;
            bestScore = score;
        }
        return result;
    }

    private static string ComposeDescription(ElixirEffectAtomAsset[] atoms)
    {
        if (atoms == null || atoms.Length == 0) return "凝聚平和药性";
        var fragments = atoms.Select(atom => atom.description_fragment)
            .Where(fragment => !string.IsNullOrEmpty(fragment)).ToArray();
        var sentences = atoms.Select(atom => atom.effect_sentence)
            .Where(sentence => !string.IsNullOrEmpty(sentence)).Distinct().ToArray();
        var lead = fragments.Length switch
        {
            0 => "凝聚平和药性",
            1 => $"凝聚{fragments[0]}",
            2 => $"兼取{fragments[0]}与{fragments[1]}",
            _ => $"兼取{fragments[0]}、{fragments[1]}与{fragments[2]}"
        };
        return sentences.Length == 0 ? lead : $"{lead}，{string.Join("，", sentences)}";
    }

    private static Dictionary<string, float> ComposeStatusStats(
        ElixirRecipeContext recipe,
        ElixirEffectAtomAsset[] atoms)
    {
        Dictionary<string, float> result = new();
        var baseScale = 0.75f + recipe.quality_stage * 0.25f;
        for (var i = 0; i < atoms.Length; i++)
        {
            var atomScale = baseScale * (i == 0 ? 1f : 0.55f);
            AddStats(result, atoms[i].status_stats, atomScale);
        }
        return result;
    }

    private static void ComposeDataGain(
        ElixirEffectComposition composition,
        ElixirRecipeContext recipe,
        ElixirEffectAtomAsset[] atoms)
    {
        var operationAtom = atoms.FirstOrDefault(atom =>
            atom.data_gain_kind == ElixirDataGainKind.OneTime && atom.data_operations.Length > 0);
        if (operationAtom != null)
        {
            composition.DataGainKind = ElixirDataGainKind.OneTime;
            composition.DataOperations = operationAtom.data_operations.ToList();
            composition.OperationArgs = ResolveOperationArgs(recipe, operationAtom);
            return;
        }

        var traitAtom = atoms.FirstOrDefault(atom =>
            atom.data_gain_kind == ElixirDataGainKind.Trait && atom.data_traits.Length > 0);
        if (traitAtom != null)
        {
            composition.DataGainKind = ElixirDataGainKind.Trait;
            composition.DataTraits = traitAtom.data_traits.ToList();
            composition.FallbackAttributes = ComposeDataAttributes(recipe, atoms);
            return;
        }

        composition.DataGainKind = ElixirDataGainKind.Attribute;
        composition.DataAttributes = ComposeDataAttributes(recipe, atoms);
    }

    private static Dictionary<string, string> ResolveOperationArgs(
        ElixirRecipeContext recipe,
        ElixirEffectAtomAsset atom)
    {
        Dictionary<string, string> result = atom.operation_args == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(atom.operation_args);
        if (!atom.data_operations.Contains(Operations.OpenElementRoot)) return result;

        var root = ResolveElementRoot(recipe);
        if (root != null) result["element_root"] = root.id;
        return result;
    }

    private static ElementRootAsset ResolveElementRoot(ElixirRecipeContext recipe)
    {
        ElementRootAsset result = null;
        var bestScore = 0.01f;
        var semanticLibrary = ModClass.L.SemanticLibrary;
        foreach (var root in ModClass.L.ElementRootLibrary.list.OrderBy(item => item.id, StringComparer.Ordinal))
        {
            var contributions = root.Semantics?.contributions ?? Array.Empty<SemanticContribution>();
            var score = 0f;
            for (var i = 0; i < contributions.Length; i++)
            {
                var contribution = contributions[i];
                if (!semanticLibrary.TryResolve(contribution.semantic_id, out var semantic) ||
                    semantic == CultivationSemantics.Trait.ElementRoot) continue;
                score = Mathf.Max(score,
                    Mathf.Max(0f, recipe.GetSemanticScore(semantic).Net) *
                    contribution.strength * contribution.confidence);
            }

            if (score <= bestScore) continue;
            result = root;
            bestScore = score;
        }
        return result;
    }

    private static Dictionary<string, float> ComposeDataAttributes(
        ElixirRecipeContext recipe,
        ElixirEffectAtomAsset[] atoms)
    {
        Dictionary<string, float> result = new();
        var baseScale = 0.55f + recipe.quality_stage * 0.18f;
        for (var i = 0; i < atoms.Length; i++)
        {
            var atomScale = baseScale * (i == 0 ? 1f : 0.5f);
            AddStats(result, atoms[i].data_attributes, atomScale);
        }
        return result;
    }

    private static void AddStats(
        IDictionary<string, float> target,
        IReadOnlyDictionary<string, float> source,
        float multiplier)
    {
        if (source == null) return;
        foreach (var entry in source)
        {
            if (string.IsNullOrEmpty(entry.Key) || entry.Value == 0f) continue;
            target.TryGetValue(entry.Key, out var current);
            target[entry.Key] = RoundStatValue(current + entry.Value * multiplier);
        }
    }

    private static float RoundStatValue(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private static float TieBreak(int seed, string id)
    {
        return NamingRuleUtils.StableHash($"{seed}|{id}") % 1000 / 100000f;
    }

    private static float Sample01(int seed, string key)
    {
        return NamingRuleUtils.StableHash($"{seed}|{key}") / (float)int.MaxValue;
    }

    private readonly struct ScoredAtom
    {
        public readonly ElixirEffectAtomAsset Atom;
        public readonly float Score;

        public ScoredAtom(ElixirEffectAtomAsset atom, float score)
        {
            Atom = atom;
            Score = score;
        }
    }
}
