using System;
using System.Collections.Generic;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 一片地区中各种生物群系的实际格子数量。数组按生物群系标识排列，供地区说明保存。
/// </summary>
internal readonly struct GeoRegionBiomeCompositionData
{
    /// <summary>创建地区的生物群系组成记录。</summary>
    internal GeoRegionBiomeCompositionData(
        string coreBiomeId,
        string dominantBiomeId,
        string[] biomeIds,
        int[] biomeTileCounts)
    {
        CoreBiomeId = coreBiomeId ?? string.Empty;
        DominantBiomeId = dominantBiomeId ?? string.Empty;
        BiomeIds = biomeIds ?? Array.Empty<string>();
        BiomeTileCounts = biomeTileCounts ?? Array.Empty<int>();
    }

    /// <summary>核心格子中数量最多的生物群系标识。</summary>
    internal string CoreBiomeId { get; }
    /// <summary>整片地区中数量最多的生物群系标识。</summary>
    internal string DominantBiomeId { get; }
    /// <summary>地区中出现过的生物群系标识，按文字顺序排列。</summary>
    internal string[] BiomeIds { get; }
    /// <summary>每种生物群系占用的格子数，与 BiomeIds 一一对应。</summary>
    internal int[] BiomeTileCounts { get; }
}

/// <summary>统计一组地图格中的生物群系，并找出核心部分和全部格子各自最多的群系。</summary>
internal static class GeoRegionBiomeComposition
{
    /// <summary>统计地区格子的生物群系组成；列表前 coreTileCount 个格子视为核心。</summary>
    internal static GeoRegionBiomeCompositionData Build(
        GeoRegionTerrainSnapshot terrain,
        IList<int> tileIds,
        int coreTileCount)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (tileIds == null) throw new ArgumentNullException(nameof(tileIds));
        if (coreTileCount <= 0 || coreTileCount > tileIds.Count)
        {
            throw new InvalidOperationException(
                $"GeoRegion biome composition 核心尺寸无效: core={coreTileCount}, tiles={tileIds.Count}");
        }

        var totalCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var coreCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < tileIds.Count; i++)
        {
            string biomeId = terrain.GetCell(tileIds[i]).BiomeId;
            if (string.IsNullOrWhiteSpace(biomeId)) continue;
            biomeId = biomeId.Trim();
            AddCount(totalCounts, biomeId);
            if (i < coreTileCount) AddCount(coreCounts, biomeId);
        }

        var biomeIds = new List<string>(totalCounts.Keys);
        biomeIds.Sort(StringComparer.Ordinal);
        var biomeTileCounts = new int[biomeIds.Count];
        for (int i = 0; i < biomeIds.Count; i++)
        {
            biomeTileCounts[i] = totalCounts[biomeIds[i]];
        }

        return new GeoRegionBiomeCompositionData(
            ResolveDominant(coreCounts),
            ResolveDominant(totalCounts),
            biomeIds.ToArray(),
            biomeTileCounts);
    }

    /// <summary>把指定生物群系的格子数量加一。</summary>
    private static void AddCount(Dictionary<string, int> counts, string biomeId)
    {
        counts.TryGetValue(biomeId, out int count);
        counts[biomeId] = count + 1;
    }

    /// <summary>找出格子数最多的生物群系；数量相同时选择文字顺序更靠前的标识。</summary>
    private static string ResolveDominant(Dictionary<string, int> counts)
    {
        string dominant = string.Empty;
        int dominantCount = 0;
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value < dominantCount ||
                pair.Value == dominantCount &&
                string.CompareOrdinal(pair.Key, dominant) >= 0)
            {
                continue;
            }
            dominant = pair.Key;
            dominantCount = pair.Value;
        }
        return dominant;
    }
}
