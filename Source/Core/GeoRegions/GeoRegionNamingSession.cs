using System.Collections.Generic;
using Cultiway.Core.GeoRegions.Partitioning;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在一轮“把计算结果变成游戏里的地区对象”的过程中统一处理重名。
/// 它先尝试基础名，再按地区所在方位加前后缀，最后才追加数字。
/// </summary>
internal sealed class GeoRegionNamingSession
{
    // 已经实际占用的最终名称，以及每个基础名已经分配过多少次。
    private readonly HashSet<string> usedResolvedNames = new();
    private readonly Dictionary<string, int> baseNameCounters = new();

    /// <summary>
    /// 提前占用一个需要保留的旧名称或玩家自定义名称，防止后创建的地区与它重名。
    /// </summary>
    internal void ReserveName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        usedResolvedNames.Add(name.Trim());
    }

    /// <summary>
    /// 为基础名选择一个尚未使用的最终名称。
    /// 重名时优先利用地区中心在地图中的方位进行区分，仍然重名时追加递增数字。
    /// </summary>
    internal string ResolveUniqueName(
        string generatedName,
        GeoRegionDescriptor descriptor,
        int width,
        int height)
    {
        string baseName = NormalizeDirectionalWord(generatedName);
        if (usedResolvedNames.Add(baseName))
        {
            IncreaseBaseCounter(baseName);
            return baseName;
        }

        foreach (string candidate in BuildDirectionalCandidates(baseName, descriptor, width, height))
        {
            if (!usedResolvedNames.Add(candidate)) continue;
            IncreaseBaseCounter(baseName);
            return candidate;
        }

        int index = IncreaseBaseCounter(baseName);
        while (true)
        {
            string candidate = $"{baseName}{index}";
            if (usedResolvedNames.Add(candidate)) return candidate;
            index++;
        }
    }

    /// <summary>
    /// 统一移除方位词中的“部”，避免“东北部林”和“东北林”形成两套近似名称。
    /// </summary>
    private static string NormalizeDirectionalWord(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "GeoRegion";

        return name.Trim()
            .Replace("东北部", "东北")
            .Replace("西北部", "西北")
            .Replace("东南部", "东南")
            .Replace("西南部", "西南")
            .Replace("东部", "东")
            .Replace("西部", "西")
            .Replace("南部", "南")
            .Replace("北部", "北")
            .Replace("中部", "中");
    }

    /// <summary>
    /// 重名时依次生成“方位 + 基础名”和“基础名 + 方位”两种候选，并过滤重复项。
    /// </summary>
    private static IEnumerable<string> BuildDirectionalCandidates(
        string baseName,
        GeoRegionDescriptor descriptor,
        int width,
        int height)
    {
        List<string> directions = CollectDirections(descriptor.CenterX, descriptor.CenterY, width, height);
        var emitted = new HashSet<string>();
        for (int i = 0; i < directions.Count; i++)
        {
            string direction = directions[i];
            if (string.IsNullOrEmpty(direction)) continue;

            string prefix = $"{direction}{baseName}";
            if (prefix != baseName && emitted.Add(prefix)) yield return prefix;

            string suffix = $"{baseName}{direction}";
            if (suffix != baseName && emitted.Add(suffix)) yield return suffix;
        }
    }

    /// <summary>
    /// 根据地图三等分位置收集方位词，并按组合方位、南北、东西的优先顺序返回。
    /// </summary>
    private static List<string> CollectDirections(int x, int y, int width, int height)
    {
        var result = new List<string>(6);
        if (width <= 0 || height <= 0) return result;

        int x1 = width / 3;
        int x2 = width * 2 / 3;
        int y1 = height / 3;
        int y2 = height * 2 / 3;
        string eastWest = x < x1 ? "西" : x >= x2 ? "东" : string.Empty;
        string northSouth = y < y1 ? "南" : y >= y2 ? "北" : string.Empty;

        if (!string.IsNullOrEmpty(eastWest) && !string.IsNullOrEmpty(northSouth)) result.Add($"{eastWest}{northSouth}");
        if (!string.IsNullOrEmpty(northSouth)) result.Add(northSouth);
        if (!string.IsNullOrEmpty(eastWest)) result.Add(eastWest);
        result.Add(y >= height / 2 ? "北" : "南");
        result.Add(x >= width / 2 ? "东" : "西");
        return result;
    }

    /// <summary>
    /// 记录某个基础名已经分配的次数，并返回更新后的次数。
    /// </summary>
    private int IncreaseBaseCounter(string baseName)
    {
        int count = baseNameCounters.TryGetValue(baseName, out int current) ? current + 1 : 1;
        baseNameCounters[baseName] = count;
        return count;
    }
}
