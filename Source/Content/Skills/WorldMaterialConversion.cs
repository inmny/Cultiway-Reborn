using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>在已确认的真实范围内转化世界材料，不重建施放或重置支付来源。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>接收真实执行请求后选择材料，先分配额度，再转交数量，最后更新承托。</summary>
    /// <param name="controller">当前控制者。</param><param name="selection">部位实际空间形状。</param>
    /// <param name="matter">明确输出材料。</param><param name="rule">来源筛选和最大体积。</param>
    /// <param name="clock">已确认的世界时点。</param><param name="cast">此次施放编号。</param><param name="ability">实际构造编号。</param>
    /// <param name="identities">可选的明确材料编号；空集合只产生零结果。</param>
    internal static ConstructionWorldReceipt ConvertMatter(long controller, IReadOnlyList<AbilityCapsule> selection, AbilityMatter matter,
        CompiledWorldMatter rule, double clock, long cast, string ability, IReadOnlyList<long> identities = null)
    {
        // 先以共同水柱建立候选，归属筛选不会改变原支付者。
        instance.RebuildIndex();
        var targets = new HashSet<long>();
        foreach (var shape in selection)
        {
            instance.index.Query(AbilityContactIndex.Swept(shape, shape), instance.nearby);
            foreach (int i in instance.nearby) targets.Add(instance.indexed[i].Id);
        }

        var candidates = targets.Select(id => rows[id])
            .Where(r => (identities == null || identities.Contains(r.Identity)) && !r.Finished && !r.Recovering && (!rule.OwnedOnly || r.Provider == controller) &&
                        rule.Matches(r.Material.Matter, matter)).OrderBy(r => r.Id).ToArray();
        double remaining = rule.MaximumVolume;
        long epoch = instance.generation;
        var done = new HashSet<(string, string, int, int, bool)>();
        var changedRows = new Dictionary<long, double>();
        var outputIdentities = new Dictionary<long, long>();
        double convertedResource = 0;
        foreach (var candidate in candidates)
        {
            if (remaining <= 0) break;
            if (!rows.TryGetValue(candidate.Id, out var row) || row.Finished || row.Recovering) continue;
            if (row.Material.Flow == null)
            {
                if (row.Material.Volume > remaining || !row.Material.Select(selection, instance.picked)) continue;
                double amount = row.Material.Volume;
                int before = row.Material.Cells * row.Material.Cells;
                var converted = row.Material.ConvertTo(matter, instance.terrain);
                int after = converted.Cells * converted.Cells;
                if (cells + reservedCells - before + after > MaximumCells) continue;
                row.Material = converted;
                cells += after - before;
                remaining -= amount;
                changedRows[row.Id] = amount;
                convertedResource += row.Material.Resource;
                continue;
            }

            WorldMaterialRecord[] group;
            float[] heights;
            if (row.WorldGrid)
            {
                var key = WaterKey(row);
                if (!done.Add(key)) continue;
                group = candidates.Where(r => r.WorldGrid && WaterKey(r).Equals(key) && !r.Finished && !r.Recovering)
                    .ToArray();
                heights = instance.waterColumns[key];
            }
            else
            {
                group = new[] { row };
                heights = row.Material.Flow.Water.Select(w => w.x).ToArray();
            }

            var chosen = new HashSet<int>();
            foreach (var source in group)
            {
                source.Material.SelectShared(selection, instance.picked, heights);
                foreach (int i in instance.picked) chosen.Add(i);
            }

            if (chosen.Count == 0) continue;
            var selected = chosen.OrderBy(i => i).ToList();
            var ratios = new double[heights.Length];
            double accepted = 0;
            float area = row.Material.Flow.Cell * row.Material.Flow.Cell;
            foreach (int i in selected)
            {
                double volume = group.Sum(r => (double)r.Material.Flow.Water[i].x) * area;
                if (volume <= 0) continue;
                double fraction = Math.Min(1, Math.Max(0, remaining - accepted) / volume);
                ratios[i] = fraction;
                accepted += volume * fraction;
                if (accepted >= remaining) break;
            }

            var actual = group.Where(r => selected.Any(i => ratios[i] > 0 && r.Material.Flow.Water[i].x > 0)).ToArray();
            if (actual.Length == 0) continue;
            var destinations = actual.ToDictionary(r => r.Id,
                r => r.WorldGrid
                    ? rows.Values.FirstOrDefault(d =>
                        outputIdentities.TryGetValue(r.Identity, out long identity) && d.Identity == identity &&
                        d.WorldGrid && !d.Recovering && !d.Finished && d.Lineage == r.Lineage &&
                        d.Expires == r.Expires && ReferenceEquals(d.Material.Matter, matter) &&
                        d.Material.ShapeHeld == (matter.HoldsShape || matter.State == "solid") &&
                        d.Material.CanMerge && r.Material.CanMerge &&
                        d.Material.Flow.Origin == r.Material.Flow.Origin)
                    : null);
            // 接收记录及水格全部容得下后才提取；容量耗尽保留原材料。
            int added = destinations.Count(pair => pair.Value == null),
                needed = actual.Where(r => destinations[r.Id] == null).Sum(r => r.Material.Cells * r.Material.Cells);
            if (rows.Count + reservedRows + added > MaximumRecords ||
                cells + reservedCells + needed > MaximumCells) continue;
            foreach (var source in actual)
            {
                AbilityMaterial taken = source.Material.Extract(selected, 1, ratios);
                convertedResource += taken.Resource;
                AbilityMaterial converted = taken.ConvertTo(matter, instance.terrain);
                var destination = destinations[source.Id];
                if (destination == null)
                {
                    long id = nextId++;
                    if (!outputIdentities.TryGetValue(source.Identity, out long identity)) outputIdentities.Add(source.Identity, identity = id);
                    destination = new WorldMaterialRecord
                    {
                        Id = id,
                        Lineage = source.Lineage,
                        Identity = identity,
                        Provider = source.Provider,
                        Controller = source.Controller,
                        Funding = source.Funding,
                        WorldGrid = source.WorldGrid,
                        Clock = clock,
                        Expires = source.Expires,
                        Material = converted
                    };
                    rows.Add(destination.Id, destination);
                    cells += converted.Cells * converted.Cells;
                }
                else destination.Material.Merge(converted);

                changedRows.TryGetValue(destination.Id, out double previous);
                changedRows[destination.Id] = previous + taken.Volume;
            }

            remaining = Math.Max(0, remaining - accepted);
        }

        if (changedRows.Count == 0) return new ConstructionWorldReceipt(ConstructionWorldResultKind.Matter, -1, clock);
        var changedSources = changedRows.Keys.Select(id => rows[id]).ToArray();
        // 完整更新承托和索引后再通知，回调看到的是同一轮已经确认的事实。
        instance.NormalizeSolidColumns();
        instance.MergeWorldLayers();
        instance.ApplySolidGround(rows.Values
            .Where(r => r.WorldGrid && !r.Finished && !r.Recovering && !r.Material.ShapeHeld).ToArray());
        instance.indexDirty = true;
        var changedIdentities = changedSources.Select(source => rows.ContainsKey(source.Id) ? source.Id : rows.Values.First(row =>
            row.Identity == source.Identity && row.WorldGrid && WaterKey(row).Equals(WaterKey(source))).Id).Distinct().OrderBy(id => id).ToArray();
        var materialIdentities = changedSources.Select(row => row.Identity).Distinct().OrderBy(id => id).ToArray();
        foreach (var pair in changedRows.OrderBy(pair => pair.Key))
            if (rows.TryGetValue(pair.Key, out var changed))
            {
                Changed?.Invoke(new WorldMaterialChange(changed, "matter", castId: cast, abilityId: ability,
                    convertedVolume: pair.Value));
                if (epoch != instance.generation) return null;
            }
        return new ConstructionWorldReceipt(ConstructionWorldResultKind.Matter, -1, clock, materials: changedIdentities,
            materialIdentities: materialIdentities, volume: changedRows.Values.Sum(), resource: convertedResource);
    }

    /// <summary>冻结来源共用最低实际基底和合计厚度，新增冻结不是多片水面互相覆盖。</summary>
    private void NormalizeSolidColumns()
    {
        foreach (var area in rows.Values
                     .Where(r => r.WorldGrid && !r.Finished && !r.Recovering && r.Material.ShapeHeld)
                     .GroupBy(r => (WaterKey(r).Item3, WaterKey(r).Item4)))
        {
            var materials = area.GroupBy(WaterKey).OrderBy(g => g.Key.Item1, StringComparer.Ordinal)
                .ThenBy(g => g.Key.Item2, StringComparer.Ordinal).Select(g => g.ToArray()).ToArray();
            for (int i = 0; i < 256; i++)
            {
                float basis = float.PositiveInfinity;
                foreach (var sources in materials)
                    foreach (var row in sources)
                        if (row.Material.Flow.Water[i].x > 0)
                            basis = Mathf.Min(basis, row.Material.Flow.Ground[i]);
                if (float.IsPositiveInfinity(basis)) continue;
                foreach (var sources in materials)
                {
                    float depth = 0;
                    foreach (var row in sources)
                    {
                        row.Material.Flow.Ground[i] = basis;
                        depth += row.Material.Flow.Water[i].x;
                    }

                    basis += depth;
                }
            }
        }
    }

    /// <summary>解冻等过程将相同来源重新接入同区，不重复建立通量来源索引。</summary>
    private void MergeWorldLayers()
    {
        foreach (var group in rows.Values.Where(r => r.WorldGrid && !r.Finished && !r.Recovering && r.Material.CanMerge)
                     .GroupBy(r => (r.Identity, Key: WaterKey(r), r.Expires)))
        {
            var records = group.OrderBy(r => r.Id).ToArray();
            if (records.Length < 2) continue;
            var first = records[0];
            foreach (var row in records.Skip(1))
            {
                first.Material.Merge(row.Material);
                rows.Remove(row.Id);
                cells -= row.Material.Cells * row.Material.Cells;
            }
        }
    }

    /// <summary>固态层顶部成为水流的实际承托，不改变原版地图通行。</summary>
    /// <param name="active">当前需要流动的格网层。</param>
    private void ApplySolidGround(WorldMaterialRecord[] active)
    {
        var tops = new Dictionary<(int, int), float[]>();
        foreach (var group in rows.Values
                     .Where(r => r.WorldGrid && !r.Finished && !r.Recovering && r.Material.ShapeHeld)
                     .GroupBy(WaterKey))
        {
            var key = group.Key;
            if (!tops.TryGetValue((key.Item3, key.Item4), out var top))
            {
                top = Enumerable.Repeat(float.NegativeInfinity, 256).ToArray();
                tops.Add((key.Item3, key.Item4), top);
            }

            var sources = group.ToArray();
            for (int i = 0; i < 256; i++)
            {
                float depth = 0, basis = float.PositiveInfinity;
                foreach (var row in sources)
                    if (row.Material.Flow.Water[i].x > 0)
                    {
                        depth += row.Material.Flow.Water[i].x;
                        basis = Mathf.Min(basis, row.Material.Flow.Ground[i]);
                    }

                if (depth > 0) top[i] = Mathf.Max(top[i], basis + depth);
            }
        }

        foreach (var row in active)
        {
            row.Material.Flow.RefreshGround(terrain, true);
            var key = WaterKey(row);
            if (!tops.TryGetValue((key.Item3, key.Item4), out var top)) continue;
            for (int i = 0; i < 256; i++)
                if (!float.IsNegativeInfinity(top[i]))
                    row.Material.Flow.Ground[i] =
                        Mathf.Max(row.Material.Flow.Ground[i], top[i]);
        }
    }
}
