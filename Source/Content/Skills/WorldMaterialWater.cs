using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Performance;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>世界水体共用格网，保留来源而不各自叠加一套独立水面。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>全图最长边最多512格，小地图保持一地图格的宽度。</summary>
    private static float WorldCell => Mathf.Max(1, Mathf.Ceil(Mathf.Max(MapBox.width, MapBox.height) / 512f));

    /// <summary>完整发布期间的区域外观缓存，不以临时施放维持水面。</summary>
    private readonly Dictionary<(string, string, int, int, string, string, Color?), object> waterOwners = new();

    /// <summary>当前筛选周期共同水柱，来源选择不重复扫描所有其他材料。</summary>
    private readonly Dictionary<(string, string, int, int), float[]> waterColumns = new();

    /// <summary>物理材料和区域坐标共同确定一根连续水柱。</summary>
    /// <param name="row">真实共同格网层。</param>
    private static (string, string, int, int) WaterKey(WorldMaterialRecord row) => (row.Material.Matter.Id,
        row.Material.Matter.State, (int)Math.Round(row.Material.Flow.Origin.x / (16 * WorldCell)),
        (int)Math.Round(row.Material.Flow.Origin.y / (16 * WorldCell)));

    /// <summary>真实外观仍由资源、表面和染色共同确定。</summary>
    /// <param name="row">真实材料。</param>
    private static (string, string, Color?) Appearance(WorldMaterialRecord row) => (row.Material.Form.Description.Id,
        row.Material.Surface, row.Material.Dye);

    /// <summary>空间筛选先聚合一次水柱，仍保留各来源的独立水量。</summary>
    private void UpdateWaterColumns()
    {
        waterColumns.Clear();
        foreach (var row in rows.Values)
            if (row.WorldGrid && !row.Finished && !row.Recovering)
            {
                var key = WaterKey(row);
                if (!waterColumns.TryGetValue(key, out var heights)) waterColumns.Add(key, heights = new float[256]);
                for (int i = 0; i < 256; i++) heights[i] += row.Material.Flow.Water[i].x;
            }
    }

    /// <summary>开放水层按面积保守转入共同格网；额度不足时保留原材料并施加回压。</summary>
    private void ConvertOpenWater()
    {
        foreach (var row in rows.Values.ToArray())
        {
            if (row.WorldGrid || row.Recovering || row.Finished || row.Material.Flow == null ||
                row.Material.Matter.HoldsShape || !row.Material.Flow.Settings.OpenBoundary) continue;
            AbilityFlow source = row.Material.Flow;
            float cell = WorldCell;
            var coordinates = new HashSet<(int, int)>();
            int nx = Mathf.CeilToInt(MapBox.width / cell), ny = Mathf.CeilToInt(MapBox.height / cell);
            for (int i = 0; i < source.Water.Length; i++)
                if (source.Water[i].x > 0)
                {
                    double left = source.Origin.x + i % source.Settings.Cells * (double)source.Cell,
                        bottom = source.Origin.y + i / source.Settings.Cells * (double)source.Cell;
                    int x0 = Math.Max(0, (int)Math.Floor(left / cell)),
                        x1 = Math.Min(nx - 1, (int)Math.Ceiling((left + source.Cell) / cell) - 1);
                    int y0 = Math.Max(0, (int)Math.Floor(bottom / cell)),
                        y1 = Math.Min(ny - 1, (int)Math.Ceiling((bottom + source.Cell) / cell) - 1);
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        coordinates.Add((x / 16, y / 16));
                }

            if (coordinates.Count == 0)
            {
                row.Material.Resource = row.Material.Volume = 0;
                row.Finished = true;
                continue;
            }

            if (rows.Count + reservedRows + coordinates.Count - 1 > MaximumRecords ||
                cells + reservedCells - source.Water.Length + coordinates.Count * 256 > MaximumCells) continue;
            var outputs = coordinates.OrderBy(p => p.Item2).ThenBy(p => p.Item1)
                .ToDictionary(p => p, p => NewFlow(source, p, cell));
            double initial = row.Material.Volume,
                ratio = initial > 0 ? row.Material.Resource / initial : 0,
                accepted = 0;
            for (int i = 0; i < source.Water.Length; i++)
            {
                Vector3 water = source.Water[i];
                if (water.x <= 0) continue;
                double left = source.Origin.x + i % source.Settings.Cells * (double)source.Cell,
                    bottom = source.Origin.y + i / source.Settings.Cells * (double)source.Cell;
                int x0 = Math.Max(0, (int)Math.Floor(left / cell)),
                    x1 = Math.Min(nx - 1, (int)Math.Ceiling((left + source.Cell) / cell) - 1);
                int y0 = Math.Max(0, (int)Math.Floor(bottom / cell)),
                    y1 = Math.Min(ny - 1, (int)Math.Ceiling((bottom + source.Cell) / cell) - 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    double overlap =
                        Math.Max(0, Math.Min(left + source.Cell, (x + 1) * cell) - Math.Max(left, x * cell)) *
                        Math.Max(0, Math.Min(bottom + source.Cell, (y + 1) * cell) - Math.Max(bottom, y * cell));
                    accepted += (double)water.x * overlap;
                    AbilityFlow target = outputs[(x / 16, y / 16)];
                    int at = y % 16 * 16 + x % 16;
                    target.Water[at] += water * (float)(overlap / (cell * cell));
                    target.Budget[at] += (double)water.x * overlap * ratio;
                }
            }

            double total = 0;
            foreach (var flow in outputs.Values)
            {
                flow.RefreshTotals();
                flow.RefreshGround(terrain, true);
                total += flow.Volume;
            }

            double budget = outputs.Values.Sum(flow => flow.Resource);
            double allowed = Math.Min(row.Material.Resource, ratio * Math.Min(initial, accepted));
            if (budget > allowed && budget > 0)
                foreach (var flow in outputs.Values)
                    for (int i = 0; i < flow.Budget.Length; i++)
                        flow.Budget[i] *= allowed / budget;
            AbilityMaterial original = row.Material;
            bool first = true;
            cells -= source.Water.Length;
            foreach (var flow in outputs.Values)
            {
                WorldMaterialRecord piece;
                if (first)
                {
                    piece = row;
                    flow.Escaped = source.Escaped + Math.Max(0, initial - total);
                    flow.Evaporated = source.Evaporated;
                    flow.Recovered = source.Recovered;
                    flow.Transferred = source.Transferred;
                    first = false;
                }
                else
                {
                    piece = CopyWaterRow(row, flow);
                    rows.Add(piece.Id, piece);
                }

                piece.Material = original.WithFlow(flow);
                piece.WorldGrid = true;
                cells += 256;
            }

            indexDirty = true;
        }
    }

    /// <summary>一个来源在相邻区建立空接收层，不给它添加初始量。</summary>
    /// <param name="source">同一来源的原层。</param><param name="position">区域坐标。</param><param name="cell">共同格宽。</param>
    private static AbilityFlow NewFlow(AbilityFlow source, (int, int) position, float cell)
    {
        var settings =
            new CompiledFlow(
                new ConstructionFlow
                {
                    Cells = 16, Size = 16, Gravity = source.Settings.Gravity, Drag = source.Settings.Drag,
                    Evaporation = source.Settings.Evaporation, OpenBoundary = true
                }, "世界水层");
        return new AbilityFlow(settings)
        {
            Cell = cell, Origin = new Vector2(position.Item1 * 16 * cell, position.Item2 * 16 * cell), Started = true,
            Budget = new double[256]
        };
    }

    /// <summary>区域只复制归属和过程规则，水量来自独占新工作区。</summary>
    /// <param name="source">原来源层。</param><param name="flow">新空层或已分配层。</param>
    private static WorldMaterialRecord CopyWaterRow(WorldMaterialRecord source, AbilityFlow flow) => new()
    {
        Id = nextId++, Lineage = source.Lineage, Provider = source.Provider, Controller = source.Controller,
        Resource = source.Resource,
        WorldGrid = true, Clock = source.Clock, Expires = source.Expires, Material = source.Material.WithFlow(flow)
    };

    /// <summary>仅为湿边界预留相邻层，超出预算的界面由共同计算反射。</summary>
    /// <param name="active">当前同一时间的真实流动来源层。</param>
    private void AddWaterHalo(WorldMaterialRecord[] active)
    {
        float cell = WorldCell;
        var existing = new HashSet<(long, int, int)>(active.Select(row => (row.Lineage,
            (int)Math.Round(row.Material.Flow.Origin.x / (16 * cell)),
            (int)Math.Round(row.Material.Flow.Origin.y / (16 * cell)))));
        foreach (var row in active.OrderBy(row => row.Id))
        {
            AbilityFlow flow = row.Material.Flow;
            int tx = (int)Math.Round(flow.Origin.x / (16 * cell)), ty = (int)Math.Round(flow.Origin.y / (16 * cell));
            for (int side = 0; side < 4; side++)
            {
                bool wet = false;
                for (int p = 0; p < 16; p++)
                {
                    int at = side switch { 0 => p * 16, 1 => p * 16 + 15, 2 => p, _ => 240 + p };
                    if (flow.Water[at].x > 0)
                    {
                        wet = true;
                        break;
                    }
                }

                if (!wet) continue;
                int x = tx + (side == 0 ? -1 : side == 1 ? 1 : 0), y = ty + (side == 2 ? -1 : side == 3 ? 1 : 0);
                if (x < 0 || y < 0 || x * 16 * cell >= MapBox.width || y * 16 * cell >= MapBox.height ||
                    existing.Contains((row.Lineage, x, y))) continue;
                if (rows.Count + reservedRows >= MaximumRecords || cells + reservedCells + 256 > MaximumCells) continue;
                var next = NewFlow(flow, (x, y), cell);
                next.RefreshGround(terrain, true);
                var neighbour = CopyWaterRow(row, next);
                rows.Add(neighbour.Id, neighbour);
                cells += 256;
                existing.Add((row.Lineage, x, y));
            }
        }
    }

    /// <summary>共同时间先备邻区，再分阶段计算与提交；不在线程里调用游戏对象。</summary>
    /// <param name="from">共同起点。</param><param name="until">确认的目标时点。</param>
    private IEnumerator<bool> AdvanceWater(double from, double until)
    {
        double clock = from;
        long epoch = generation;
        while (clock < until)
        {
            var active = rows.Values.Where(r =>
                    r.WorldGrid && !r.Material.Matter.HoldsShape && !r.Recovering && !r.Finished && r.Clock <= clock)
                .ToArray();
            if (active.Length == 0) yield break;
            AddWaterHalo(active);
            active = rows.Values.Where(r =>
                    r.WorldGrid && !r.Material.Matter.HoldsShape && !r.Recovering && !r.Finished && r.Clock <= clock)
                .ToArray();
            // 地表由主线程取得，邻区扩大后也采集其真实承托。
            Rect bounds = default;
            bool first = true;
            foreach (var row in active)
            {
                var p = row.Material.Flow.Origin;
                if (first)
                {
                    bounds = new Rect(p.x, p.y, 16 * WorldCell, 16 * WorldCell);
                    first = false;
                }
                else
                {
                    bounds.xMin = Mathf.Min(bounds.xMin, p.x);
                    bounds.yMin = Mathf.Min(bounds.yMin, p.y);
                    bounds.xMax = Mathf.Max(bounds.xMax, p.x + 16 * WorldCell);
                    bounds.yMax = Mathf.Max(bounds.yMax, p.y + 16 * WorldCell);
                }
            }

            terrain = ConstructedAbilityService.GroundProvider?.Invoke(bounds);
            foreach (var row in active) row.Material.RefreshGround(terrain);
            ApplySolidGround(active);
            float dt = (float)(until - clock);
            var domains = active.GroupBy(r => (r.Material.Matter.Id, r.Material.Matter.State)).Select(g =>
                new AbilityWorldWater(g.Select(r => new AbilityWaterLayer(r.Id, r.Lineage, r.Material.Flow)), WorldCell,
                    new Vector2(MapBox.width, MapBox.height), dt)).ToArray();
            float step = domains.Min(d => d.Seconds);
            if (step < dt)
                domains = active.GroupBy(r => (r.Material.Matter.Id, r.Material.Matter.State)).Select(g =>
                    new AbilityWorldWater(g.Select(r => new AbilityWaterLayer(r.Id, r.Lineage, r.Material.Flow)),
                        WorldCell, new Vector2(MapBox.width, MapBox.height), step)).ToArray();
            var tasks = domains.SelectMany(d => Enumerable.Range(0, d.Count).Select(i => (Domain: d, Index: i)))
                .ToArray();
            int work = active.Length * 256;
            using (var calculation = MaterialTasks(tasks.Length, work, i => tasks[i].Domain.Compute(tasks[i].Index)))
                while (calculation.MoveNext())
                {
                    if (epoch != generation) yield break;
                    yield return false;
                }

            if (epoch != generation) yield break;
            using (var calculation = MaterialTasks(tasks.Length, work, i => tasks[i].Domain.Commit(tasks[i].Index)))
                while (calculation.MoveNext())
                {
                    if (epoch != generation) yield break;
                    yield return false;
                }

            if (epoch != generation) yield break;
            clock = Math.Min(until, clock + step);
            foreach (var row in active)
            {
                row.Clock = clock;
                row.Material.Volume = row.Material.Flow.Volume;
                row.Material.Resource = row.Material.Flow.Resource;
                if (row.Expires > 0 && clock >= row.Expires) row.Finished = true;
            }

            if (clock >= until || until - clock < 1e-9)
            {
                foreach (var row in active) row.Clock = until;
                yield break;
            }

            yield return false;
        }
    }

    /// <summary>小批直接计算，中批复用同步并行，大批才跨帧等待，避免微任务空等整帧。</summary>
    /// <param name="count">实际任务数。</param><param name="work">格子或形体工作量。</param><param name="action">只改独占数值的任务。</param>
    private IEnumerator<bool> MaterialTasks(int count, int work, Action<int> action)
    {
        if (work < 1024)
        {
            for (int i = 0; i < count; i++) action(i);
            yield break;
        }

        if (work < 16384)
        {
            SimulationWorkerPool.Instance.RunIndexed(0, count, action);
            yield break;
        }

        job = SimulationWorkerPool.Instance.BeginIndexed(0, count, action);
        while (job.HasValue && !SimulationWorkerPool.Instance.IsCompleted(job.Value)) yield return false;
        CompleteJob();
    }

    /// <summary>无水且不再需要接收的空区退出，仍有材料的来源保留必要去向记录。</summary>
    private static void PruneWater()
    {
        var pooled = rows.Values.Where(r => r.WorldGrid && !r.Recovering && !r.Finished).ToArray();
        var wet = new HashSet<long>(pooled.Where(r => r.Material.Volume > 0).Select(r => r.Lineage));
        var needed = new HashSet<(long, int, int)>();
        foreach (var row in pooled)
            if (row.Material.Volume > 0)
            {
                var key = WaterKey(row);
                needed.Add((row.Lineage, key.Item3, key.Item4));
                for (int side = 0; side < 4; side++)
                {
                    bool edge = false;
                    for (int p = 0; p < 16; p++)
                    {
                        int at = side switch { 0 => p * 16, 1 => p * 16 + 15, 2 => p, _ => 240 + p };
                        if (row.Material.Flow.Water[at].x > 0)
                        {
                            edge = true;
                            break;
                        }
                    }

                    if (edge)
                        needed.Add((row.Lineage, key.Item3 + (side == 0 ? -1 : side == 1 ? 1 : 0),
                            key.Item4 + (side == 2 ? -1 : side == 3 ? 1 : 0)));
                }
            }

        foreach (var row in pooled)
            if (row.Material.Volume <= 0)
            {
                var key = WaterKey(row);
                var flow = row.Material.Flow;
                bool history = flow.Escaped > 0 || flow.Evaporated > 0 || flow.Recovered > 0 || flow.Transferred > 0;
                if (!wet.Contains(row.Lineage) || !history && !needed.Contains((row.Lineage, key.Item3, key.Item4)))
                    row.Finished = true;
            }
    }

    /// <summary>共同水面使用合计深度；同格占比最多的外观决定表面，来源仍独立输运。</summary>
    private FormVisualInstance[] WaterVisuals()
    {
        var result = rows.Values.Where(r => !r.Finished && (!r.WorldGrid || r.Recovering))
            .Select(r => r.Material.Visual(terrain)).ToList();
        var groups = rows.Values.Where(r => !r.Finished && r.WorldGrid && !r.Recovering).GroupBy(WaterKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id).ToArray());
        var totals = new Dictionary<(string, string, int, int), AbilityFlow>();
        foreach (var pair in groups)
        {
            var first = pair.Value[0];
            var flow = NewFlow(first.Material.Flow, (pair.Key.Item3, pair.Key.Item4), WorldCell);
            foreach (var row in pair.Value)
                for (int i = 0; i < 256; i++)
                    flow.Water[i] += row.Material.Flow.Water[i];
            flow.RefreshTotals();
            Array.Copy(first.Material.Flow.Ground, flow.Ground, 256);
            totals.Add(pair.Key, flow);
        }

        var used = new HashSet<(string, string, int, int, string, string, Color?)>();
        foreach (var pair in groups)
        {
            var key = pair.Key;
            var neighbours = new List<WorldMaterialRecord>();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (groups.TryGetValue((key.Item1, key.Item2, key.Item3 + dx, key.Item4 + dy), out var other))
                    neighbours.AddRange(other);
            if (!neighbours.Any(r => r.Material.Volume > 0)) continue;
            var looks = neighbours.OrderBy(r => r.Id).GroupBy(Appearance).Select(g => g.First()).ToArray();
            var bank = looks.Select(_ => new float[18 * 18]).ToArray();
            var water = new Vector3[18 * 18];
            var ground = new float[18 * 18];
            for (int y = -1; y <= 16; y++)
            for (int x = -1; x <= 16; x++)
            {
                int tx = key.Item3 + (x < 0 ? -1 : x >= 16 ? 1 : 0),
                    ty = key.Item4 + (y < 0 ? -1 : y >= 16 ? 1 : 0),
                    i = (y + 16) % 16 * 16 + (x + 16) % 16,
                    at = (y + 1) * 18 + x + 1;
                var position = (key.Item1, key.Item2, tx, ty);
                if (!totals.TryGetValue(position, out var total))
                {
                    ground[at] = terrain?.Sample((key.Item3 * 16 + x + .5f) * WorldCell,
                        (key.Item4 * 16 + y + .5f) * WorldCell) ?? 0;
                    continue;
                }

                water[at] = total.Water[i];
                ground[at] = total.Ground[i];
                float largest = -1;
                int winner = 0;
                for (int look = 0; look < looks.Length; look++)
                {
                    var appearance = Appearance(looks[look]);
                    float amount = 0;
                    foreach (var row in groups[position])
                        if (Appearance(row).Equals(appearance))
                            amount += row.Material.Flow.Water[i].x;
                    if (amount > largest)
                    {
                        largest = amount;
                        winner = look;
                    }
                }

                bank[winner][at] = 1;
            }

            for (int look = 0; look < looks.Length; look++)
            {
                if (!bank[look].Any(v => v > 0)) continue;
                var row = looks[look];
                var appearance = Appearance(row);
                var ownerKey = (key.Item1, key.Item2, key.Item3, key.Item4, appearance.Item1, appearance.Item2,
                    appearance.Item3);
                used.Add(ownerKey);
                if (!waterOwners.TryGetValue(ownerKey, out object owner))
                    waterOwners.Add(ownerKey, owner = new object());
                result.Add(row.Material.Visual(terrain,
                    new FormLayerFrame(totals[key], owner, water, ground, bank, look,
                        groups[key].Count(r => r.Material.Volume > 0),
                        Enumerable.Range(0, 256)
                            .Count(i => groups[key].Count(r => r.Material.Flow.Water[i].x > 1e-7f) > 1))));
            }
        }

        foreach (var key in waterOwners.Keys.Where(key => !used.Contains(key)).ToArray()) waterOwners.Remove(key);
        return result.ToArray();
    }
}