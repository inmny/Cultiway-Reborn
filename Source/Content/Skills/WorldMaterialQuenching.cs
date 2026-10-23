using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>火与独立水体的接触沿用实际空间索引和来源水量。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>只枚举扫过范围内的液态水，混合水柱仅提供一次共同接触。</summary>
    /// <param name="bounds">本次火体的真实扫过范围。</param><param name="output">复用的水体候选。</param>
    internal static void QueryQuenchingWater(Bounds bounds, List<QuenchingWater> output)
    {
        output.Clear();
        instance.RebuildIndex();
        instance.index.Query(bounds, instance.nearby);
        var shared = new HashSet<(string, string, int, int, bool)>();
        foreach (int index in instance.nearby.OrderBy(i => instance.indexed[i].Id))
        {
            var row = instance.indexed[index];
            if (row.Material.Matter.Id != "water" || row.Material.Matter.State != "liquid" ||
                row.Material.Volume <= 0) continue;
            instance.shapes.Clear();
            if (row.WorldGrid)
            {
                var key = WaterKey(row);
                if (!shared.Add(key)) continue;
                // 混合水柱使用共同深度，不能让极薄的某一来源决定整个水面的高度。
                var flow = row.Material.Flow;
                float[] heights = instance.waterColumns[key];
                for (int i = 0; i < heights.Length; i++)
                {
                    if (heights[i] <= 0) continue;
                    instance.shapes.Add(QuenchingCell(row, i, heights[i]));
                }
            }
            else row.Material.Contacts(instance.shapes);

            foreach (var shape in instance.shapes)
                if (AbilityContactIndex.Swept(shape, shape).Intersects(bounds))
                    output.Add(new QuenchingWater(row.Id, shape));
        }
    }

    /// <summary>混合水体的候选和确认读取同一根完整水柱，不要求代表来源拥有每一格。</summary>
    /// <param name="row">同区的稳定来源。</param><param name="index">真实共同水格。</param>
    /// <param name="height">全部来源的共同水深。</param>
    private static AbilityCapsule QuenchingCell(WorldMaterialRecord row, int index, float height)
    {
        var flow = row.Material.Flow;
        Rect area = Cultiway.Core.Skills.ConductingWater.FlowArea(flow, index);
        var cell = new ConductingWater(0, 0, flow.Ground[index], height, area, 0, 0);
        var shape = cell.Shape(WorldCell);
        return new AbilityCapsule(0, shape.A, shape.B, 0, "water", index,
            material: row.Material.Matter, halfExtents: shape.HalfExtents);
    }

    /// <summary>确认时重新核对材料和形状，按共同水柱各来源的真实比例蒸散。</summary>
    /// <param name="record">候选的稳定世界编号。</param><param name="shape">候选水格或刚性形状编号。</param>
    /// <param name="fire">确认后的实际火体。</param><param name="maximum">允许移除的最大体积。</param>
    /// <param name="changed">真正失水的世界来源。</param>
    /// <param name="contacted">确认时是否仍接触真实液态水。</param>
    internal static double EvaporateWater(long record, int shape, AbilityCapsule fire, double maximum,
        List<long> changed, out bool contacted)
    {
        contacted = false;
        changed.Clear();
        instance.RebuildIndex();
        if (!rows.TryGetValue(record, out var row) || row.Finished ||
            row.Material.Matter.Id != "water" || row.Material.Matter.State != "liquid") return 0;
        instance.shapes.Clear();
        if (row.WorldGrid)
        {
            float height = instance.waterColumns[WaterKey(row)][shape];
            if (height > 0) instance.shapes.Add(QuenchingCell(row, shape, height));
        }
        else row.Material.Contacts(instance.shapes);

        if (!instance.shapes.Any(s =>
                s.Shape == shape && AbilityPairSweep.Find(s, s, fire, fire, out _, out _, out _))) return 0;
        double removed = 0;
        if (row.WorldGrid)
        {
            var key = WaterKey(row);
            var sources = rows.Values.Where(r => r.WorldGrid && !r.Recovering && !r.Finished && WaterKey(r) == key)
                .OrderBy(r => r.Id).ToArray();
            double depth = sources.Sum(r => (double)r.Material.Flow.Water[shape].x);
            if (depth <= 0) return 0;
            contacted = true;
            foreach (var source in sources)
            {
                double share = source.Material.Flow.Water[shape].x / depth;
                double amount = source.Material.EvaporateWater(shape, maximum * share);
                if (amount > 0)
                {
                    removed += amount;
                    changed.Add(source.Id);
                }
            }
        }
        else
        {
            contacted = row.Material.Volume > 0;
            if (contacted && row.Recovering) InterruptThermalRecovery(row);
            removed = row.Material.EvaporateWater(shape, maximum);
            if (removed > 0) changed.Add(row.Id);
        }

        if (removed > 0) instance.indexDirty = true;
        return removed;
    }

    /// <summary>真实火接触打断尚未完成的世界收束，已确认的累计不重发。</summary>
    /// <param name="row">仍有真实水量的独立来源。</param>
    private static void InterruptThermalRecovery(WorldMaterialRecord row)
    {
        long groupId = row.RecoveryGroup;
        row.RecoveryGroup = 0;
        if (groupId != 0 && recoveryGroups.TryGetValue(groupId, out var group))
        {
            group.Completed += row.InitialResource * row.Efficiency * row.Progress;
            if (!rows.Values.Any(r => r.RecoveryGroup == groupId)) recoveryGroups.Remove(groupId);
        }

        row.Recovering = false;
        row.InitialVolume = row.InitialResource = row.Confirmed = row.Fraction = 0;
        row.Seconds = row.Efficiency = row.Progress = 0;
    }
}