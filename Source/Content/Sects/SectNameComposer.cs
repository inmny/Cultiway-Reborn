using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Libraries;
using Cultiway.Utils;

namespace Cultiway.Content.Sects;

internal sealed class SectNameCandidate
{
    public string Name;
    public float Score;
    public string Source;
}

/// <summary>
/// 根据创宗语义生成并排序宗门名称候选。
/// </summary>
internal static class SectNameComposer
{
    private static readonly string[] ValidEndings =
    [
        "山庄", "书院", "学宫", "剑宗", "剑门", "剑派",
        "宗", "门", "派", "宫", "观", "谷", "洞", "庄", "院", "阁", "堂", "会", "府", "山"
    ];

    internal static IReadOnlyList<SectNameCandidate> ComposeCandidates(SectNamingContext context)
    {
        List<ScoredAtom> atoms = SelectAtoms(context);
        ScoredAtom element = atoms.FirstOrDefault(atom => atom.Asset.category == SectNameAtomCategory.Element);
        List<SectNameCandidate> candidates = new();

        for (int i = 0; i < atoms.Count; i++)
        {
            AddAtomCandidates(candidates, context, atoms[i], element);
        }

        SectNameCandidate[] ranked = candidates
            .GroupBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();
        if (ranked.Length == 0)
        {
            throw new InvalidOperationException($"宗门命名没有生成有效候选: signature={context.Signature}");
        }

        float bestScore = ranked[0].Score;
        SectNameCandidate[] preferred = ranked
            .Where(candidate => candidate.Score >= bestScore - 8f)
            .Take(5)
            .ToArray();
        int start = PositiveMod(NamingRuleUtils.StableHash($"{context.Signature}|preferred"), preferred.Length);
        List<SectNameCandidate> result = new(ranked.Length);
        for (int i = 0; i < preferred.Length; i++)
        {
            result.Add(preferred[(start + i) % preferred.Length]);
        }

        HashSet<string> added = new(result.Select(candidate => candidate.Name), StringComparer.Ordinal);
        for (int i = 0; i < ranked.Length; i++)
        {
            if (added.Add(ranked[i].Name))
            {
                result.Add(ranked[i]);
            }
        }
        return result;
    }

    internal static bool TryNormalizeCandidate(string rawName, out string name)
    {
        name = SectNameText.NormalizeToken(rawName);
        if (name.Length is < 2 or > 6) return false;
        bool hasValidEnding = false;
        for (int i = 0; i < ValidEndings.Length; i++)
        {
            if (!name.EndsWith(ValidEndings[i], StringComparison.Ordinal)) continue;

            hasValidEnding = true;
            break;
        }
        if (!hasValidEnding) return false;
        if (HasAdjacentDuplicate(name)) return false;
        return true;
    }

    private static List<ScoredAtom> SelectAtoms(SectNamingContext context)
    {
        SectNameAtomLibrary library = Libraries.Manager.SectNameAtomLibrary;
        List<ScoredAtom> scored = library.All
            .Select(asset => new ScoredAtom
            {
                Asset = asset,
                Score = asset.ScoreFor(context) + TieBreak(context.Signature, asset.id)
            })
            .Where(atom => atom.Score > 0f)
            .OrderByDescending(atom => atom.Score)
            .ThenByDescending(atom => atom.Asset.priority)
            .ThenBy(atom => atom.Asset.id, StringComparer.Ordinal)
            .ToList();

        List<ScoredAtom> selected = new();
        AddCategory(selected, scored, SectNameAtomCategory.Element, 1);
        AddCategory(selected, scored, SectNameAtomCategory.Cultivation, 2);
        AddCategory(selected, scored, SectNameAtomCategory.Residence, 1);
        AddCategory(selected, scored, SectNameAtomCategory.Policy, 3);
        AddCategory(selected, scored, SectNameAtomCategory.Generic, 1);
        return selected;
    }

    private static void AddCategory(
        List<ScoredAtom> selected,
        List<ScoredAtom> scored,
        SectNameAtomCategory category,
        int count)
    {
        selected.AddRange(scored.Where(atom => atom.Asset.category == category).Take(count));
    }

    private static void AddAtomCandidates(
        List<SectNameCandidate> candidates,
        SectNamingContext context,
        ScoredAtom atom,
        ScoredAtom element)
    {
        AddPatternCandidates(candidates, context, atom, element, atom.Asset.patterns);
    }

    private static void AddPatternCandidates(
        List<SectNameCandidate> candidates,
        SectNamingContext context,
        ScoredAtom atom,
        ScoredAtom element,
        string[] patterns)
    {
        int atomSeed = NamingRuleUtils.StableHash($"{context.Signature}|{atom.Asset.id}");
        string theme = atom.Asset.PickNameStem(atomSeed);
        string elementStem = element?.Asset.PickNameStem(
            NamingRuleUtils.StableHash($"{context.Signature}|element")) ?? string.Empty;

        for (int i = 0; i < patterns.Length; i++)
        {
            string pattern = patterns[i];
            if (!HasRequiredValues(pattern, context, theme, elementStem)) continue;

            if (!pattern.Contains("{suffix}", StringComparison.Ordinal))
            {
                AddCandidate(candidates, context, atom, pattern, theme, elementStem, string.Empty);
                continue;
            }

            int suffixRank = 0;
            foreach (string suffix in atom.Asset.EnumerateSuffixes(atomSeed / 3 + i * 17))
            {
                AddCandidate(candidates, context, atom, pattern, theme, elementStem, suffix, suffixRank);
                suffixRank++;
            }
        }
    }

    private static void AddCandidate(
        List<SectNameCandidate> candidates,
        SectNamingContext context,
        ScoredAtom atom,
        string pattern,
        string theme,
        string element,
        string suffix,
        int suffixRank = 0)
    {
        if (UsesEquivalentParts(pattern, context, theme, element)) return;

        string rawName = pattern
            .Replace("{doctrine_short}", context.DoctrineShort)
            .Replace("{doctrine}", context.DoctrineCore)
            .Replace("{residence_full}", context.ResidenceFull)
            .Replace("{residence}", context.ResidenceCore)
            .Replace("{element}", element)
            .Replace("{theme}", theme)
            .Replace("{suffix}", suffix);
        if (!TryNormalizeCandidate(rawName, out string name)) return;

        candidates.Add(new SectNameCandidate
        {
            Name = name,
            Score = PatternScore(pattern)
                    + Math.Min(atom.Score, 130f) / 30f
                    + atom.Asset.priority * 0.02f
                    - suffixRank * 2f,
            Source = $"{atom.Asset.id}:{pattern}"
        });
    }

    private static bool HasRequiredValues(
        string pattern,
        SectNamingContext context,
        string theme,
        string element)
    {
        if (pattern.Contains("{doctrine}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(context.DoctrineCore)) return false;
        if (pattern.Contains("{doctrine_short}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(context.DoctrineShort)) return false;
        if (pattern.Contains("{residence}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(context.ResidenceCore)) return false;
        if (pattern.Contains("{residence_full}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(context.ResidenceFull)) return false;
        if (pattern.Contains("{element}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(element)) return false;
        if (pattern.Contains("{theme}", StringComparison.Ordinal)
            && string.IsNullOrEmpty(theme)) return false;
        return true;
    }

    private static bool UsesEquivalentParts(
        string pattern,
        SectNamingContext context,
        string theme,
        string element)
    {
        if (pattern.Contains("{theme}", StringComparison.Ordinal))
        {
            if (pattern.Contains("{doctrine_short}", StringComparison.Ordinal)
                && PartsOverlap(context.DoctrineShort, theme)) return true;
            if (pattern.Contains("{residence}", StringComparison.Ordinal)
                && PartsOverlap(context.ResidenceCore, theme)) return true;
            if (pattern.Contains("{element}", StringComparison.Ordinal)
                && PartsOverlap(element, theme)) return true;
        }

        return pattern.Contains("{residence}", StringComparison.Ordinal)
               && pattern.Contains("{doctrine_short}", StringComparison.Ordinal)
               && PartsOverlap(context.ResidenceCore, context.DoctrineShort);
    }

    private static bool PartsOverlap(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        return left == right || left.Contains(right) || right.Contains(left);
    }

    private static float PatternScore(string pattern)
    {
        if (pattern.Contains("{residence_full}", StringComparison.Ordinal)) return 92f;
        if (pattern.Contains("{residence}", StringComparison.Ordinal)
            && pattern.Contains("{doctrine_short}", StringComparison.Ordinal)) return 91f;
        if (pattern.Contains("{doctrine}", StringComparison.Ordinal)) return 90f;
        if (pattern.Contains("{residence}", StringComparison.Ordinal)
            && !pattern.Contains("{theme}", StringComparison.Ordinal)) return 90f;
        if (pattern.Contains("{doctrine_short}", StringComparison.Ordinal)
            && pattern.Contains("{theme}", StringComparison.Ordinal)) return 87f;
        if (pattern.Contains("{element}", StringComparison.Ordinal)
            && pattern.Contains("{theme}", StringComparison.Ordinal)) return 86f;
        if (pattern.Contains("{residence}", StringComparison.Ordinal)
            && pattern.Contains("{theme}", StringComparison.Ordinal)) return 85f;
        if (pattern.Contains("{theme}", StringComparison.Ordinal)) return 78f;
        if (pattern.Contains("{element}", StringComparison.Ordinal)) return 77f;
        return 70f;
    }

    private static bool HasAdjacentDuplicate(string name)
    {
        for (int i = 1; i < name.Length; i++)
        {
            if (name[i] == name[i - 1]) return true;
        }
        return false;
    }

    private static float TieBreak(string signature, string id)
    {
        return NamingRuleUtils.StableHash($"{signature}|{id}") % 1000 / 100000f;
    }

    private static int PositiveMod(int value, int divisor)
    {
        return (value & int.MaxValue) % divisor;
    }

    private sealed class ScoredAtom
    {
        public SectNameAtomAsset Asset;
        public float Score;
    }
}
