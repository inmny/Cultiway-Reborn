using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils;

namespace Cultiway.Content.Sects;

/// <summary>
/// 生成宗门名称并在当前世界内解决重名。
/// </summary>
internal static class SectNamingService
{
    private static readonly string[] FallbackPrefixes =
    [
        "太", "上", "玉", "紫", "玄", "灵", "云", "天",
        "清", "真", "元", "星", "霄", "华", "明", "微"
    ];

    private static readonly string[] FallbackCores =
    [
        "虚", "霄", "府", "华", "真", "元", "道", "岳",
        "渊", "阳", "冥", "辰", "衡", "光", "川", "海",
        "岚", "霞", "泉", "峰", "玄", "灵", "清", "苍"
    ];

    private static readonly string[] FallbackSuffixes = ["宗", "门", "派"];

    internal static string GenerateName(Sect sect, Actor founder)
    {
        SectNamingContext context = SectNamingContext.Create(sect, founder);
        IReadOnlyList<SectNameCandidate> candidates = SectNameComposer.ComposeCandidates(context);
        HashSet<string> usedNames = CollectUsedNames(sect);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (usedNames.Add(candidates[i].Name))
            {
                LogName(sect, founder, context, candidates[i].Name, candidates[i].Source);
                return candidates[i].Name;
            }
        }

        foreach (SectNameCandidate candidate in candidates)
        {
            foreach (string variant in BuildGeographicVariants(candidate.Name, context))
            {
                if (!usedNames.Add(variant)) continue;

                LogName(sect, founder, context, variant, $"unique:{candidate.Source}");
                return variant;
            }
        }

        foreach (string fallback in BuildFallbackNames(context.Seed))
        {
            if (!usedNames.Add(fallback)) continue;

            LogName(sect, founder, context, fallback, "unique:fallback");
            return fallback;
        }

        throw new InvalidOperationException($"无法生成唯一宗门名称: sect={sect.getID()} signature={context.Signature}");
    }

    private static HashSet<string> CollectUsedNames(Sect current)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (Sect sect in WorldboxGame.I.Sects)
        {
            if (sect == current || sect.isRekt()) continue;
            if (!string.IsNullOrEmpty(sect.name))
            {
                names.Add(sect.name);
            }
        }
        return names;
    }

    private static IEnumerable<string> BuildGeographicVariants(string baseName, SectNamingContext context)
    {
        string[] prefixes =
        [
            context.ResidenceCore,
            context.HomeCityCore,
            context.Direction
        ];
        for (int i = 0; i < prefixes.Length; i++)
        {
            string prefix = prefixes[i];
            if (string.IsNullOrEmpty(prefix) || baseName.StartsWith(prefix, StringComparison.Ordinal)) continue;

            if (SectNameComposer.TryNormalizeCandidate(prefix + baseName, out string variant))
            {
                yield return variant;
            }
        }
    }

    private static IEnumerable<string> BuildFallbackNames(int seed)
    {
        int pairCount = FallbackPrefixes.Length * FallbackCores.Length * FallbackSuffixes.Length;
        int pairStart = PositiveMod(NamingRuleUtils.StableHash($"{seed}|fallback-pair"), pairCount);
        for (int offset = 0; offset < pairCount; offset++)
        {
            int index = (pairStart + offset) % pairCount;
            string suffix = FallbackSuffixes[index % FallbackSuffixes.Length];
            index /= FallbackSuffixes.Length;
            string core = FallbackCores[index % FallbackCores.Length];
            string prefix = FallbackPrefixes[index / FallbackCores.Length];
            if (SectNameComposer.TryNormalizeCandidate(prefix + core + suffix, out string candidate))
            {
                yield return candidate;
            }
        }

        int tripleCount = FallbackPrefixes.Length * FallbackCores.Length * FallbackCores.Length
                          * FallbackSuffixes.Length;
        int tripleStart = PositiveMod(NamingRuleUtils.StableHash($"{seed}|fallback-triple"), tripleCount);
        for (int offset = 0; offset < tripleCount; offset++)
        {
            int index = (tripleStart + offset) % tripleCount;
            string suffix = FallbackSuffixes[index % FallbackSuffixes.Length];
            index /= FallbackSuffixes.Length;
            string second = FallbackCores[index % FallbackCores.Length];
            index /= FallbackCores.Length;
            string first = FallbackCores[index % FallbackCores.Length];
            string prefix = FallbackPrefixes[index / FallbackCores.Length];
            if (SectNameComposer.TryNormalizeCandidate(prefix + first + second + suffix, out string candidate))
            {
                yield return candidate;
            }
        }
    }

    private static void LogName(
        Sect sect,
        Actor founder,
        SectNamingContext context,
        string name,
        string source)
    {
        SectVerifyLog.Log(
            "SectName",
            $"sect_id={sect.getID()} founder={SectVerifyLog.Actor(founder)} name={name} source={source} doctrine={context.DoctrineCore} residence={context.ResidenceName} element={context.PrimaryElement}");
    }

    private static int PositiveMod(int value, int divisor)
    {
        return (value & int.MaxValue) % divisor;
    }
}
