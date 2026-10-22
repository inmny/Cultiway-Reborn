using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>导电只读取实际液态水柱，显示不能建立水路。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>在确认时点建立已进入共同格网的真实导体快照。</summary>
    internal static List<ConductingWater> ConductingWater()
    {
        instance.RebuildIndex();
        var result = new List<ConductingWater>();
        foreach (var pair in instance.waterColumns.OrderBy(p => p.Key.Item4).ThenBy(p => p.Key.Item3))
        {
            if (pair.Key.Item1 != "water" || pair.Key.Item2 != "liquid") continue;
            var bed = instance.waterBeds[pair.Key];
            for (int i = 0; i < 256; i++)
                if (pair.Value[i] > 0)
                    result.Add(new ConductingWater(pair.Key.Item3 * 16 + i % 16, pair.Key.Item4 * 16 + i / 16, bed[i],
                        pair.Value[i]));
        }

        return result;
    }

    /// <summary>共同水路采用同一实际格宽。</summary>
    internal static float ConductingCell => WorldCell;

    /// <summary>伤害回调可能冻结或移除材料，后续分支不能沿旧快照继续。</summary>
    /// <param name="cell">已到达水柱快照。</param><param name="depth">最低导电水深。</param><param name="current">仍存在的真实水柱。</param>
    internal static bool StillConducting(in ConductingWater cell, float depth, out ConductingWater current)
    {
        instance.RebuildIndex();
        var key = ("water", "liquid", cell.X / 16, cell.Y / 16);
        current = default;
        if (!instance.waterColumns.TryGetValue(key, out var heights)) return false;
        int i = cell.Y % 16 * 16 + cell.X % 16;
        if (heights[i] < depth) return false;
        current = new ConductingWater(cell.X, cell.Y, instance.waterBeds[key][i], heights[i]);
        return true;
    }

    /// <summary>仅保存已经完成传播的短暂显示，不在恢复后重放伤害。</summary>
    private readonly Dictionary<(int, int), double> conductionPulse = new();

    /// <summary>按真实到达格短暂增强原有水面，不增加装饰线条和攻击实体。</summary>
    /// <param name="route">实际到达水路。</param><param name="clock">确认时点。</param>
    internal static void PulseConduction(IEnumerable<ConductingWater> route, double clock)
    {
        foreach (var cell in route) instance.conductionPulse[(cell.X, cell.Y)] = clock + .45;
    }

    /// <summary>发布时一次建立不可变邻格明暗，不改变水深和动量。</summary>
    /// <param name="x">区域横坐标。</param><param name="y">区域纵坐标。</param>
    private float[] ConductionGlow(int x, int y)
    {
        if (conductionPulse.Count == 0) return null;
        var result = new float[18 * 18];
        double now = World.world.getCurWorldTime();
        for (int dy = -1; dy <= 16; dy++)
        for (int dx = -1; dx <= 16; dx++)
            if (conductionPulse.TryGetValue((x * 16 + dx, y * 16 + dy), out double until))
                    result[(dy + 1) * 18 + dx + 1] = .9f * Mathf.Clamp01((float)((until - now) / .45));
        return result;
    }
}
