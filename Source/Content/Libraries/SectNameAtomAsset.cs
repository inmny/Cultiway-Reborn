using System;
using System.Collections.Generic;
using Cultiway.Content.Sects;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 宗门命名语义的来源类别。
/// </summary>
public enum SectNameAtomCategory
{
    Element,
    Cultivation,
    Residence,
    Policy,
    Generic
}

/// <summary>
/// 宗门命名原子；集中声明特定语义可使用的词干、门号和组合模板。
/// </summary>
public sealed class SectNameAtomAsset : Asset
{
    public SectNameAtomCategory category;
    public string[] name_stems = [];
    public string[] suffixes = [];
    public string[] patterns = [];
    public int priority;
    internal Func<SectNamingContext, float> ScoreContext;

    internal float ScoreFor(SectNamingContext context)
    {
        return Math.Max(0f, ScoreContext(context));
    }

    internal string PickNameStem(int seed)
    {
        return Pick(name_stems, seed);
    }

    internal IEnumerable<string> EnumerateSuffixes(int seed)
    {
        if (suffixes.Length == 0) yield break;

        int start = PositiveMod(seed, suffixes.Length);
        for (int i = 0; i < suffixes.Length; i++)
        {
            yield return suffixes[(start + i) % suffixes.Length];
        }
    }

    private static string Pick(string[] values, int seed)
    {
        return values.Length == 0 ? string.Empty : values[PositiveMod(seed, values.Length)];
    }

    private static int PositiveMod(int value, int divisor)
    {
        return (value & int.MaxValue) % divisor;
    }
}

/// <summary>
/// 宗门命名原子资产库。
/// </summary>
public sealed class SectNameAtomLibrary : AssetLibrary<SectNameAtomAsset>
{
    internal IEnumerable<SectNameAtomAsset> All => list;
}
