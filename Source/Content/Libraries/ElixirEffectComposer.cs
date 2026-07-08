using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using strings;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public static class ElixirEffectComposer
{
    private const string FallbackAtomId = "Cultiway.ElixirEffectAtom.Body";

    public static ElixirEffectComposition Compose(ElixirAsset elixir)
    {
        var recipe = elixir != null && elixir.has_recipe_semantic
            ? elixir.recipe_semantic
            : CreateLooseRecipe(elixir?.description_key, elixir?.ingredients?.Select(x => x.GetName()).ToArray());
        var text = $"{elixir?.description_key}|{string.Join("|", elixir?.ingredients?.Select(x => x.GetName()) ?? Array.Empty<string>())}";
        var seed = NamingRuleUtils.StableHash($"{elixir?.id}|{text}|{recipe.quality_stage}|{recipe.quality_level}|{recipe.main_shape_id}|{recipe.primary_element_index}");
        return Compose(recipe, text, seed);
    }

    public static ElixirEffectComposition ComposeFromIngredientNames(string[] ingredientNames)
    {
        var recipe = CreateLooseRecipe(string.Empty, ingredientNames);
        var text = string.Join("|", ingredientNames ?? Array.Empty<string>());
        return Compose(recipe, text, NamingRuleUtils.StableHash(text));
    }

    public static string ResolveEffectHint(ElixirRecipeContext recipe)
    {
        var atom = SelectAtoms(recipe, string.Empty, NamingRuleUtils.StableHash($"{recipe.main_shape_id}|{recipe.primary_element_index}"), 1).FirstOrDefault();
        return atom?.tag ?? "body";
    }

    private static ElixirEffectComposition Compose(ElixirRecipeContext recipe, string text, int seed)
    {
        var atoms = SelectAtoms(recipe, text, seed, TargetAtomCount(recipe, seed)).ToArray();
        var composition = new ElixirEffectComposition { Atoms = atoms };
        composition.Name = ComposeName(recipe, atoms, seed);
        composition.Description = ComposeDescription(atoms);
        composition.StatusStats = ComposeStatusStats(recipe, atoms);
        ComposeDataGain(composition, recipe, atoms, seed);
        return composition;
    }

    private static List<ElixirEffectAtomAsset> SelectAtoms(ElixirRecipeContext recipe, string text, int seed, int targetCount)
    {
        var library = Manager.ElixirEffectAtomLibrary;
        var scored = library?.list?
            .Select(atom => new
            {
                Atom = atom,
                Score = atom.ScoreFor(recipe, text) + TieBreak(seed, atom.id)
            })
            .Where(x => x.Score > 0f)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored == null || scored.Count == 0)
        {
            var fallback = library?.get(FallbackAtomId);
            return fallback == null ? new List<ElixirEffectAtomAsset>() : new List<ElixirEffectAtomAsset> { fallback };
        }

        List<ElixirEffectAtomAsset> selected = new();
        HashSet<string> tags = new(StringComparer.Ordinal);
        foreach (var entry in scored)
        {
            if (selected.Count >= targetCount) break;
            var tag = entry.Atom.tag ?? entry.Atom.id;
            if (tags.Contains(tag)) continue;
            selected.Add(entry.Atom);
            tags.Add(tag);
        }

        for (var i = 0; selected.Count == 0 && i < scored.Count; i++)
        {
            selected.Add(scored[i].Atom);
        }
        return selected;
    }

    private static int TargetAtomCount(ElixirRecipeContext recipe, int seed)
    {
        var count = 1;
        if (recipe.quality_stage >= 1 || recipe.ingredient_count >= 3) count++;
        if (recipe.quality_stage >= 3 || (recipe.ingredient_count >= 4 && seed % 3 == 0)) count++;
        return Mathf.Clamp(count, 1, 3);
    }

    private static string ComposeName(ElixirRecipeContext recipe, ElixirEffectAtomAsset[] atoms, int seed)
    {
        var prefix = PickQualityPrefix(recipe, seed);
        var element = PickElementPrefix(recipe, seed);
        var main = atoms.Length > 0 ? atoms[0].PickNameStem(seed) : "固元";
        var secondary = atoms.Length > 1 && seed % 2 == 0 ? atoms[1].PickNameStem(seed / 7 + 17) : string.Empty;
        var name = NamingRuleUtils.NormalizeName($"{prefix}{element}{main}{secondary}丹");
        if (name.Length <= 10) return name;

        name = NamingRuleUtils.NormalizeName($"{prefix}{main}丹");
        if (name.Length <= 10) return name;
        return NamingRuleUtils.NormalizeName($"{main}丹");
    }

    private static string PickQualityPrefix(ElixirRecipeContext recipe, int seed)
    {
        if (recipe.quality_stage >= 3) return NamingRuleUtils.Pick(seed, "太清", "无垢", "九转");
        if (recipe.quality_stage == 2 && seed % 2 == 0) return NamingRuleUtils.Pick(seed, "九转", "玄", "天元");
        return string.Empty;
    }

    private static string PickElementPrefix(ElixirRecipeContext recipe, int seed)
    {
        if (recipe.primary_element_index < ElementIndex.Iron || seed % 3 == 1) return string.Empty;
        return recipe.primary_element_index switch
        {
            ElementIndex.Iron => NamingRuleUtils.Pick(seed, "庚金", "玄金", "金"),
            ElementIndex.Wood => NamingRuleUtils.Pick(seed, "青木", "生", "木"),
            ElementIndex.Water => NamingRuleUtils.Pick(seed, "玄水", "寒", "水"),
            ElementIndex.Fire => NamingRuleUtils.Pick(seed, "赤火", "炎", "赤"),
            ElementIndex.Earth => NamingRuleUtils.Pick(seed, "厚土", "地", "黄"),
            ElementIndex.Neg => NamingRuleUtils.Pick(seed, "幽阴", "幽", "玄"),
            ElementIndex.Pos => NamingRuleUtils.Pick(seed, "阳华", "曜", "明"),
            ElementIndex.Entropy => NamingRuleUtils.Pick(seed, "混沌", "浊", "玄"),
            _ => string.Empty
        };
    }

    private static string ComposeDescription(ElixirEffectAtomAsset[] atoms)
    {
        if (atoms == null || atoms.Length == 0) return "淬炼血气，增强体魄";
        var fragments = atoms.Select(x => x.description_fragment).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        var sentences = atoms.Select(x => x.effect_sentence).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
        var lead = fragments.Length switch
        {
            0 => "凝聚平和药性",
            1 => $"凝聚{fragments[0]}",
            2 => $"兼取{fragments[0]}与{fragments[1]}",
            _ => $"兼取{fragments[0]}、{fragments[1]}与{fragments[2]}"
        };
        return sentences.Length == 0 ? lead : $"{lead}，{string.Join("，", sentences)}";
    }

    private static Dictionary<string, float> ComposeStatusStats(ElixirRecipeContext recipe, ElixirEffectAtomAsset[] atoms)
    {
        Dictionary<string, float> result = new();
        var baseScale = 0.75f + recipe.quality_stage * 0.25f + recipe.quality_level * 0.04f + Mathf.Log(recipe.strength + 1f) * 0.12f;
        for (var i = 0; i < atoms.Length; i++)
        {
            var atomScale = baseScale * (i == 0 ? 1f : 0.55f);
            foreach (var kv in atoms[i].status_stats ?? new Dictionary<string, float>())
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == 0f) continue;
                result.TryGetValue(kv.Key, out var current);
                result[kv.Key] = RoundStatValue(current + kv.Value * atomScale);
            }
        }

        if (result.Count == 0)
        {
            result[S.health] = RoundStatValue(8f * baseScale);
        }
        return result;
    }

    private static void ComposeDataGain(ElixirEffectComposition composition, ElixirRecipeContext recipe,
        ElixirEffectAtomAsset[] atoms, int seed)
    {
        var operationAtom = atoms.FirstOrDefault(x => x.data_operations != null && x.data_operations.Length > 0);
        var traitAtom = atoms.FirstOrDefault(x => x.data_traits != null && x.data_traits.Length > 0);

        if (operationAtom != null && recipe.quality_stage >= 2 && seed % 4 == 0)
        {
            composition.DataGainChosen = "one_time";
            composition.DataOperations = operationAtom.data_operations.Take(2).ToList();
            composition.OperationArgs = operationAtom.operation_args;
            return;
        }

        if (traitAtom != null && recipe.quality_stage >= 2 && seed % 3 == 0)
        {
            composition.DataGainChosen = "trait";
            composition.DataTraits = traitAtom.data_traits.Take(2).ToList();
            composition.FallbackAttributes = ComposeDataAttributes(recipe, atoms);
            return;
        }

        composition.DataGainChosen = "attribute";
        composition.DataAttributes = ComposeDataAttributes(recipe, atoms);
    }

    private static Dictionary<string, float> ComposeDataAttributes(ElixirRecipeContext recipe, ElixirEffectAtomAsset[] atoms)
    {
        Dictionary<string, float> result = new();
        var scale = 0.55f + recipe.quality_stage * 0.18f + recipe.quality_level * 0.025f + Mathf.Log(recipe.strength + 1f) * 0.08f;
        for (var i = 0; i < atoms.Length; i++)
        {
            var atomScale = scale * (i == 0 ? 1f : 0.5f);
            foreach (var kv in atoms[i].data_attributes ?? new Dictionary<string, float>())
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == 0f) continue;
                result.TryGetValue(kv.Key, out var current);
                result[kv.Key] = RoundStatValue(current + kv.Value * atomScale);
            }
        }

        if (result.Count == 0)
        {
            result[S.health] = RoundStatValue(4f * scale);
        }
        return result;
    }

    private static ElixirRecipeContext CreateLooseRecipe(string hint, string[] ingredientNames)
    {
        var text = $"{hint}|{string.Join("|", ingredientNames ?? Array.Empty<string>())}";
        return new ElixirRecipeContext
        {
            ingredient_count = ingredientNames?.Length ?? 0,
            primary_element_index = GuessElement(text),
            secondary_element_index = NamingRuleUtils.NoElement,
            main_shape_id = GuessShape(text),
            effect_hint = string.Empty,
            quality_stage = 0,
            quality_level = 0,
            strength = 1f
        };
    }

    private static int GuessElement(string text)
    {
        if (string.IsNullOrEmpty(text)) return NamingRuleUtils.NoElement;
        if (text.Contains("火") || text.Contains("炎") || text.Contains("赤")) return ElementIndex.Fire;
        if (text.Contains("金") || text.Contains("锋") || text.Contains("甲")) return ElementIndex.Iron;
        if (text.Contains("木") || text.Contains("生") || text.Contains("青")) return ElementIndex.Wood;
        if (text.Contains("水") || text.Contains("寒") || text.Contains("冰")) return ElementIndex.Water;
        if (text.Contains("土") || text.Contains("黄") || text.Contains("地")) return ElementIndex.Earth;
        if (text.Contains("阴") || text.Contains("幽")) return ElementIndex.Neg;
        if (text.Contains("阳") || text.Contains("曜")) return ElementIndex.Pos;
        if (text.Contains("混沌") || text.Contains("熵")) return ElementIndex.Entropy;
        return NamingRuleUtils.NoElement;
    }

    private static string GuessShape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Contains("根") || text.Contains("参")) return ShapeId(ItemShapes.Root);
        if (text.Contains("芝") || text.Contains("菇")) return ShapeId(ItemShapes.Mushroom);
        if (text.Contains("果")) return ShapeId(ItemShapes.Fruit);
        if (text.Contains("莲")) return ShapeId(ItemShapes.Lotus);
        if (text.Contains("翼")) return ShapeId(ItemShapes.Wing);
        if (text.Contains("羽")) return ShapeId(ItemShapes.Feather);
        if (text.Contains("甲") || text.Contains("壳")) return ShapeId(ItemShapes.Shell);
        if (text.Contains("石")) return ShapeId(ItemShapes.Stone);
        if (text.Contains("晶")) return ShapeId(ItemShapes.Crystal);
        if (text.Contains("爪")) return ShapeId(ItemShapes.Claw);
        if (text.Contains("牙")) return ShapeId(ItemShapes.Tooth);
        if (text.Contains("角")) return ShapeId(ItemShapes.Horn);
        if (text.Contains("血")) return ShapeId(ItemShapes.Blood);
        if (text.Contains("骨")) return ShapeId(ItemShapes.Bone);
        return string.Empty;
    }

    private static string ShapeId(ItemShapeAsset shape)
    {
        return shape?.id ?? string.Empty;
    }

    private static float RoundStatValue(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private static float TieBreak(int seed, string id)
    {
        return (NamingRuleUtils.StableHash($"{seed}|{id}") % 1000) / 100000f;
    }
}
