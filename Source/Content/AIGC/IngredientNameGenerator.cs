using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Semantics;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class IngredientNameGenerator : PromptNameGenerator<IngredientNameGenerator>
{
    private const float MinimumRawSemanticScore = 0.2f;
    private const float MinimumSecondaryNamingScore = 0.25f;
    private const float SecondaryRelativeScore = 0.35f;

    private static readonly int[] MaxNameLengths = [4, 5, 7, 9];
    private static readonly string[][] QualityFallbackStems =
    [
        ["灵", "玄", "元"],
        ["玄", "灵", "凝"],
        ["天", "玄", "九转"],
        ["仙", "太清", "无垢"]
    ];

    protected override string NameDictPath { get; } =
        Path.Combine(Application.persistentDataPath, "Cultiway_IngredientNameDict.json");

    [Hotfixable]
    protected override string GetSystemPrompt()
    {
        return "你需要为用户给出的材料命名，并且要符合材料来源的特性，不要有任何符号，不要给出思考过程，仅给出一个答案。\\nInput example:\\n为拥有火灵根，金煌金丹的龙掉落的材料命名。\\nOutput example:\\n赤金龙鳞";
    }

    protected override string GetDefaultName(string[] param)
    {
        return GenerateDefaultName(param);
    }

    /// <summary>构建 Tooltip 等展示逻辑使用的材料上下文。</summary>
    public static IngredientNamingContext CreateContext(Entity ingredient)
    {
        var context = new IngredientNamingContext();
        if (ingredient.IsNull) return context;

        if (ingredient.TryGetComponent(out ItemCreation creation))
        {
            context.SourceAssetId = creation.creator_asset_id ?? string.Empty;
            context.SourceName = NamingRuleUtils.GetSourceDisplayName(creation.creator_asset_id, creation.creator);
        }
        if (ingredient.TryGetComponent(out ItemShape shape))
        {
            context.ShapeId = shape.shape_id;
        }
        if (ingredient.TryGetComponent(out ElementRoot root))
        {
            NamingRuleUtils.ApplyElementRoot(context, root);
        }
        if (ingredient.TryGetComponent(out Jindan jindan))
        {
            NamingRuleUtils.ApplyJindan(context, jindan);
        }
        if (ingredient.TryGetComponent(out ItemLevel level))
        {
            context.QualityStage = level.Stage;
            context.QualityLevel = level.Level;
        }

        return context;
    }

    /// <summary>根据材料实体当前可推导的直接语义生成稳定名称。</summary>
    public static string GenerateDefaultName(Entity ingredient)
    {
        if (ingredient.IsNull || !ingredient.TryGetComponent(out ItemShape itemShape)) return "灵材";

        var shape = itemShape.Type;
        var candidates = CollectNamingCandidates(ingredient);
        var seed = CreateSemanticSeed(ingredient, itemShape, candidates);
        var noun = PickShapeNoun(shape, seed);
        var stage = GetQualityStage(ingredient);
        var maxLength = MaxNameLengths[stage];

        if (ingredient.TryGetComponent(out ItemCreation creation) &&
            NamingRuleUtils.IsPlantSource(creation.creator_asset_id) &&
            !string.IsNullOrWhiteSpace(creation.creator))
        {
            return ComposePlantName(creation.creator, shape, noun);
        }

        if (shape == ItemShapes.Ball &&
            ingredient.TryGetComponent(out Jindan jindan) &&
            !string.IsNullOrWhiteSpace(jindan.GetName()))
        {
            return ComposeJindanCoreName(jindan.GetName(), seed, maxLength);
        }

        return ComposeSemanticName(candidates, noun, stage, maxLength, seed);
    }

    /// <summary>保留给 PromptNameGenerator 的无实体离线兜底。</summary>
    public static string GenerateDefaultName(string[] param)
    {
        if (param == null || param.Length == 0) return "灵材";
        var shape = param[param.Length - 1];
        var descriptor = PickLegacyDescriptor(param);
        var seed = NamingRuleUtils.StableHash(string.Join("|", param));
        var noun = ItemShapes.PickIngredientNameCandidate(shape, seed);
        return NamingRuleUtils.LimitNameLength(NamingRuleUtils.NormalizeName($"{descriptor}{noun}"), 9);
    }

    public static string LocalizeElement(int elementIndex)
    {
        return NamingRuleUtils.LocalizeElement(elementIndex);
    }

    protected override bool IsValid(string name)
    {
        return name.Length is > 1 and < 10;
    }

    [Hotfixable]
    protected override string GetPrompt(string[] param)
    {
        StringBuilder sb = new();

        sb.Append("为拥有");
        for (int i = 1; i < param.Length; i++)
        {
            if (i > 1)
            {
                sb.Append('，');
            }
            sb.Append('“');
            sb.Append(param[i]);
            sb.Append('”');
        }

        sb.Append("的");
        sb.Append(param[0]);
        sb.Append("掉落的材料命名");

        return sb.ToString();
    }

    private static List<SemanticNameCandidate> CollectNamingCandidates(Entity ingredient)
    {
        var policy = new SemanticQueryPolicy(SemanticScope.All, MinimumRawSemanticScore);
        var ranks = IngredientSemanticService.Build(ingredient).GetDirectRanked(policy);
        var candidates = new List<SemanticNameCandidate>(ranks.Count);
        for (var i = 0; i < ranks.Count; i++)
        {
            var semantic = ranks[i].semantic;
            if (semantic == null ||
                semantic.naming_salience <= 0f ||
                semantic.naming_stems == null ||
                semantic.naming_stems.Length == 0) continue;

            var score = ranks[i].score.Net * semantic.naming_salience;
            if (score <= 0f) continue;
            candidates.Add(new SemanticNameCandidate(semantic, score));
        }

        candidates.Sort((left, right) =>
        {
            var scoreOrder = right.score.CompareTo(left.score);
            return scoreOrder != 0
                ? scoreOrder
                : string.CompareOrdinal(left.semantic.id, right.semantic.id);
        });
        return candidates;
    }

    private static int CreateSemanticSeed(
        Entity ingredient,
        ItemShape itemShape,
        IReadOnlyList<SemanticNameCandidate> candidates)
    {
        var key = new StringBuilder();
        if (ingredient.TryGetComponent(out ItemCreation creation))
        {
            key.Append(creation.creator_asset_id ?? string.Empty);
        }
        key.Append('|').Append(itemShape.shape_id ?? string.Empty);
        if (ingredient.TryGetComponent(out ItemLevel level))
        {
            key.Append('|').Append(level.Stage).Append('|').Append(level.Level);
        }
        if (ingredient.TryGetComponent(out Jindan jindan))
        {
            key.Append('|').Append(jindan.formation.signature ?? string.Empty);
        }
        key.Append('|');
        for (var i = 0; i < candidates.Count; i++)
        {
            key.Append(candidates[i].semantic.id).Append(';');
        }
        return NamingRuleUtils.StableHash(key.ToString());
    }

    private static string PickShapeNoun(ItemShapeAsset shape, int seed)
    {
        var noun = shape?.PickIngredientNameCandidate(seed);
        return string.IsNullOrWhiteSpace(noun) ? "材" : noun.Trim();
    }

    private static int GetQualityStage(Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out ItemLevel level)) return 0;
        return Mathf.Clamp(level.Stage, 0, MaxNameLengths.Length - 1);
    }

    private static string ComposePlantName(string sourceName, ItemShapeAsset shape, string noun)
    {
        var name = sourceName.Trim();
        if (name.EndsWith(noun, StringComparison.Ordinal)) return name;

        var shapeNouns = shape?.ingredient_name_candidates;
        if (shapeNouns != null)
        {
            for (var i = 0; i < shapeNouns.Length; i++)
            {
                var candidate = shapeNouns[i];
                if (!string.IsNullOrEmpty(candidate) && name.EndsWith(candidate, StringComparison.Ordinal))
                {
                    return name;
                }
            }
        }
        return $"{name}{noun}";
    }

    private static string ComposeJindanCoreName(string jindanName, int seed, int maxLength)
    {
        var noun = seed % 2 == 0 ? "丹核" : "丹珠";
        var normalized = NamingRuleUtils.NormalizeName(jindanName);
        var prefix = NamingRuleUtils.TrimKnownSuffix(normalized, "金丹", "丹");
        var available = Mathf.Max(0, maxLength - noun.Length);
        if (prefix.Length > available) prefix = prefix.Substring(0, available);
        return $"{prefix}{noun}";
    }

    private static string ComposeSemanticName(
        IReadOnlyList<SemanticNameCandidate> candidates,
        string noun,
        int stage,
        int maxLength,
        int seed)
    {
        var primaryCompact = stage == 0;
        var secondaryCompact = stage <= 1;
        if (!TryFindPrimary(candidates, noun, primaryCompact, seed, out var primary, out var primaryStem))
        {
            return ComposeQualityFallback(stage, noun, maxLength, seed);
        }

        SemanticNameCandidate? secondary = null;
        if (TryFindSecondary(candidates, primary, primaryStem, noun, secondaryCompact, seed,
                out var secondaryCandidate))
        {
            secondary = secondaryCandidate;
        }

        var name = ComposeSemanticParts(primary, secondary, primaryCompact, secondaryCompact, noun, seed);
        if (!string.IsNullOrEmpty(name) && name.Length <= maxLength) return name;

        if (secondary.HasValue && !secondaryCompact)
        {
            secondaryCompact = true;
            name = ComposeSemanticParts(primary, secondary, primaryCompact, secondaryCompact, noun, seed);
            if (!string.IsNullOrEmpty(name) && name.Length <= maxLength) return name;
        }

        if (!primaryCompact)
        {
            primaryCompact = true;
            name = ComposeSemanticParts(primary, secondary, primaryCompact, secondaryCompact, noun, seed);
            if (!string.IsNullOrEmpty(name) && name.Length <= maxLength) return name;
        }

        if (secondary.HasValue)
        {
            secondary = null;
            name = ComposeSemanticParts(primary, null, primaryCompact, secondaryCompact, noun, seed);
            if (!string.IsNullOrEmpty(name) && name.Length <= maxLength) return name;
        }

        if (!TryPickStem(primary.semantic, seed, primaryCompact, noun, null, out primaryStem))
        {
            return ComposeQualityFallback(stage, noun, maxLength, seed);
        }

        var available = Mathf.Max(0, maxLength - noun.Length);
        if (primaryStem.Length > available) primaryStem = primaryStem.Substring(0, available);
        var trimmed = $"{primaryStem}{noun}";
        return trimmed.Length >= 2 ? trimmed : "灵材";
    }

    private static bool TryFindPrimary(
        IReadOnlyList<SemanticNameCandidate> candidates,
        string noun,
        bool compact,
        int seed,
        out SemanticNameCandidate primary,
        out string stem)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!TryPickStem(candidates[i].semantic, seed, compact, noun, null, out stem)) continue;
            primary = candidates[i];
            return true;
        }

        primary = default;
        stem = string.Empty;
        return false;
    }

    private static bool TryFindSecondary(
        IReadOnlyList<SemanticNameCandidate> candidates,
        SemanticNameCandidate primary,
        string primaryStem,
        string noun,
        bool compact,
        int seed,
        out SemanticNameCandidate secondary)
    {
        var minimumScore = Mathf.Max(MinimumSecondaryNamingScore, primary.score * SecondaryRelativeScore);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate.semantic == primary.semantic || candidate.score < minimumScore) continue;

                var sameFacet = candidate.semantic.Facet == primary.semantic.Facet;
                if ((pass == 0 && sameFacet) || (pass == 1 && !sameFacet)) continue;
                if (!TryPickStem(candidate.semantic, seed, compact, noun, primaryStem, out _)) continue;

                secondary = candidate;
                return true;
            }
        }

        secondary = default;
        return false;
    }

    private static string ComposeSemanticParts(
        SemanticNameCandidate primary,
        SemanticNameCandidate? secondary,
        bool primaryCompact,
        bool secondaryCompact,
        string noun,
        int seed)
    {
        if (!TryPickStem(primary.semantic, seed, primaryCompact, noun, null, out var primaryStem))
        {
            return string.Empty;
        }

        var secondaryStem = string.Empty;
        if (secondary.HasValue)
        {
            TryPickStem(secondary.Value.semantic, seed, secondaryCompact, noun, primaryStem, out secondaryStem);
        }
        return $"{primaryStem}{secondaryStem}{noun}";
    }

    private static bool TryPickStem(
        SemanticAsset semantic,
        int seed,
        bool compact,
        string noun,
        string previousStem,
        out string stem)
    {
        var variants = BuildStemVariants(semantic, seed, compact);
        for (var i = 0; i < variants.Count; i++)
        {
            var candidate = variants[i];
            if (HasLexicalOverlap(candidate, noun) || HasLexicalOverlap(previousStem, candidate)) continue;
            stem = candidate;
            return true;
        }

        stem = string.Empty;
        return false;
    }

    private static List<string> BuildStemVariants(SemanticAsset semantic, int seed, bool compact)
    {
        var result = new List<string>();
        var stems = semantic.naming_stems;
        if (compact)
        {
            var explicitCompact = new List<string>();
            var slicedCompact = new List<string>();
            for (var i = 0; i < stems.Length; i++)
            {
                var value = stems[i]?.Trim();
                if (string.IsNullOrEmpty(value)) continue;
                if (value.Length == 1) explicitCompact.Add(value);
                else slicedCompact.Add(value.Substring(0, 1));
            }
            AppendStemGroup(result, explicitCompact, semantic, seed);
            AppendStemGroup(result, slicedCompact, semantic, seed);
            return result;
        }

        var maxLength = 0;
        for (var i = 0; i < stems.Length; i++)
        {
            var length = stems[i]?.Trim().Length ?? 0;
            if (length > maxLength) maxLength = length;
        }
        for (var length = maxLength; length >= 1; length--)
        {
            var group = new List<string>();
            for (var i = 0; i < stems.Length; i++)
            {
                var value = stems[i]?.Trim();
                if (!string.IsNullOrEmpty(value) && value.Length == length) group.Add(value);
            }
            AppendStemGroup(result, group, semantic, seed);
        }
        return result;
    }

    private static void AppendStemGroup(
        List<string> result,
        List<string> group,
        SemanticAsset semantic,
        int seed)
    {
        group.Sort((left, right) =>
        {
            var leftHash = NamingRuleUtils.StableHash($"{seed}|{semantic.id}|{left}");
            var rightHash = NamingRuleUtils.StableHash($"{seed}|{semantic.id}|{right}");
            var hashOrder = leftHash.CompareTo(rightHash);
            return hashOrder != 0 ? hashOrder : string.CompareOrdinal(left, right);
        });
        for (var i = 0; i < group.Count; i++)
        {
            if (!result.Contains(group[i])) result.Add(group[i]);
        }
    }

    private static bool HasLexicalOverlap(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        for (var i = 0; i < left.Length; i++)
        {
            if (right.IndexOf(left[i]) >= 0) return true;
        }
        return false;
    }

    private static string ComposeQualityFallback(int stage, string noun, int maxLength, int seed)
    {
        var stems = QualityFallbackStems[stage];
        var start = seed % stems.Length;
        var descriptor = string.Empty;
        for (var i = 0; i < stems.Length; i++)
        {
            var candidate = stems[(start + i) % stems.Length];
            if (HasLexicalOverlap(candidate, noun)) continue;
            descriptor = candidate;
            break;
        }
        if (string.IsNullOrEmpty(descriptor)) descriptor = "灵";

        var available = Mathf.Max(0, maxLength - noun.Length);
        if (descriptor.Length > available) descriptor = descriptor.Substring(0, available);
        var name = $"{descriptor}{noun}";
        return name.Length >= 2 ? name : "灵材";
    }

    private static string PickLegacyDescriptor(string[] param)
    {
        var joined = string.Join("|", param ?? Array.Empty<string>());
        var seed = NamingRuleUtils.StableHash(joined);
        if (joined.ContainsAny("金煌", "金灵根", "金")) return NamingRuleUtils.Pick(seed, "庚金", "金", "玄金");
        if (joined.ContainsAny("木灵根", "青木", "木")) return NamingRuleUtils.Pick(seed, "青木", "青", "生");
        if (joined.ContainsAny("水灵根", "寒霜", "冰", "水")) return NamingRuleUtils.Pick(seed, "玄水", "冰", "寒");
        if (joined.ContainsAny("火灵根", "烈火", "火", "炎")) return NamingRuleUtils.Pick(seed, "赤火", "炎", "赤");
        if (joined.ContainsAny("土灵根", "润土", "土")) return NamingRuleUtils.Pick(seed, "厚土", "黄", "地");
        if (joined.ContainsAny("阴", "幽")) return NamingRuleUtils.Pick(seed, "幽阴", "幽", "玄");
        if (joined.ContainsAny("阳", "曜")) return NamingRuleUtils.Pick(seed, "阳华", "曜", "明");
        if (joined.ContainsAny("混沌", "熵")) return NamingRuleUtils.Pick(seed, "混沌", "浊", "玄");
        return NamingRuleUtils.Pick(seed, "灵", "玄", "凝");
    }

    private readonly struct SemanticNameCandidate
    {
        public readonly SemanticAsset semantic;
        public readonly float score;

        public SemanticNameCandidate(SemanticAsset semantic, float score)
        {
            this.semantic = semantic;
            this.score = score;
        }
    }
}
