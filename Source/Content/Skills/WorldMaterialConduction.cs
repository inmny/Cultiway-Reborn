using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>导电只读取实际液态水柱，显示不能建立水路。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>在确认时点读取实际材料和原版水面，独立水格保留自己的分辨率。</summary>
    /// <param name="seeds">真实触水入口。</param><param name="range">沿水路距离的上限。</param>
    internal static List<ConductingWater> ConductingWater(IReadOnlyList<AbilityCapsule> seeds, float range)
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

        foreach (var row in rows.Values.OrderBy(row => row.Id))
        {
            var flow = row.Material.Flow;
            if (row.WorldGrid || row.Recovering || row.Finished || flow == null ||
                row.Material.Matter.Id != "water" || row.Material.Matter.State != "liquid") continue;
            for (int i = 0; i < flow.Water.Length; i++)
                if (flow.Water[i].x > 0)
                {
                    Rect area = WaterArea(flow, i);
                    result.Add(new ConductingWater(Mathf.FloorToInt(area.center.x / WorldCell),
                        Mathf.FloorToInt(area.center.y / WorldCell), flow.Ground[i], flow.Water[i].x, area, row.Id, i));
                }
        }

        if (seeds.Count == 0) return result;
        Bounds bounds = AbilityContactIndex.Swept(seeds[0], seeds[0]);
        for (int i = 1; i < seeds.Count; i++) bounds.Encapsulate(AbilityContactIndex.Swept(seeds[i], seeds[i]));
        Rect region = Rect.MinMaxRect(bounds.min.x - range, bounds.min.y - range, bounds.max.x + range,
            bounds.max.y + range);
        ConstructedAbilityService.GroundProvider?.Invoke(region);
        for (int y = Mathf.Max(0, Mathf.FloorToInt(region.yMin));
             y < Mathf.Min(MapBox.height, Mathf.CeilToInt(region.yMax));
             y++)
        for (int x = Mathf.Max(0, Mathf.FloorToInt(region.xMin));
             x < Mathf.Min(MapBox.width, Mathf.CeilToInt(region.xMax));
             x++)
            if (NativeColumn(x, y, out var column))
                result.Add(column);
        return result;
    }

    /// <summary>相邻格共用同一次边缘公式，避免小数格宽的舍入产生假干隙。</summary>
    /// <param name="flow">真实独立水层。</param><param name="index">实际水格。</param>
    private static Rect WaterArea(AbilityFlow flow, int index)
    {
        int x = index % flow.Settings.Cells, y = index / flow.Settings.Cells;
        return Rect.MinMaxRect(flow.Origin.x + x * flow.Cell, flow.Origin.y + y * flow.Cell,
            flow.Origin.x + (x + 1) * flow.Cell, flow.Origin.y + (y + 1) * flow.Cell);
    }

    /// <summary>Worldfall 在主线程提供实际水面承托；非水面或已露出海面的地形返回空值。</summary>
    internal static Func<int, int, float?> NativeWaterBottom;

    /// <summary>原版地块没有体积资料时只建立零厚接触面，不创造水量或资源。</summary>
    /// <param name="x">原版横格。</param><param name="y">原版纵格。</param><param name="column">实际原版水面或海水柱。</param>
    private static bool NativeColumn(int x, int y, out ConductingWater column)
    {
        column = default;
        var tile = World.world.GetTileSimple(x, y);
        if (tile?.Type is not
            { liquid: true, ocean: true, lava: false, can_be_unfrozen: false, forever_frozen: false }) return false;
        float? bottom = NativeWaterBottom == null ? 0 : NativeWaterBottom(x, y);
        if (!bottom.HasValue || bottom.Value > 0) return false;
        column = new ConductingWater(x, y, bottom.Value, -bottom.Value, new Rect(x, y, 1, 1), -1, 0);
        return true;
    }

    /// <summary>共同水路采用同一实际格宽。</summary>
    internal static float ConductingCell => WorldCell;

    /// <summary>伤害回调可能冻结或移除材料，后续分支不能沿旧快照继续。</summary>
    /// <param name="cell">已到达水柱快照。</param><param name="depth">最低导电水深。</param><param name="current">仍存在的真实水柱。</param>
    internal static bool StillConducting(in ConductingWater cell, float depth, out ConductingWater current)
    {
        current = default;
        if (cell.Source == -1)
            return NativeColumn(cell.X, cell.Y, out current) &&
                   (current.Surface || current.Depth >= depth);
        if (cell.Source > 0)
        {
            if (!rows.TryGetValue(cell.Source, out var row) || row.Finished || row.Recovering ||
                row.Material.Matter.Id != "water" || row.Material.Matter.State != "liquid" ||
                row.Material.Flow == null) return false;
            var flow = row.Material.Flow;
            if (row.WorldGrid || cell.Index >= flow.Water.Length || flow.Water[cell.Index].x < depth) return false;
            Rect area = WaterArea(flow, cell.Index);
            if (area != cell.Area) return false;
            current = new ConductingWater(cell.X, cell.Y, flow.Ground[cell.Index], flow.Water[cell.Index].x,
                area, cell.Source, cell.Index);
            return true;
        }

        instance.RebuildIndex();
        var key = ("water", "liquid", cell.X / 16, cell.Y / 16);
        if (!instance.waterColumns.TryGetValue(key, out var heights)) return false;
        int i = cell.Y % 16 * 16 + cell.X % 16;
        if (heights[i] < depth) return false;
        current = new ConductingWater(cell.X, cell.Y, instance.waterBeds[key][i], heights[i]);
        return true;
    }

    /// <summary>仅保存已经完成传播的短暂显示，不在恢复后重放伤害。</summary>
    private readonly Dictionary<(int, int), double> conductionPulse = new();

    /// <summary>独立水格按材料编号与格子记录显示，不能映射成更大的共同格。</summary>
    private readonly Dictionary<(long, int), double> independentPulse = new();

    /// <summary>按真实到达格短暂增强原有水面，不增加装饰线条和攻击实体。</summary>
    /// <param name="route">实际到达水路。</param><param name="clock">确认时点。</param>
    internal static void PulseConduction(IEnumerable<ConductingWater> route, double clock)
    {
        foreach (var cell in route)
            if (cell.Source == 0) instance.conductionPulse[(cell.X, cell.Y)] = clock + .45;
            else if (cell.Source > 0) instance.independentPulse[(cell.Source, cell.Index)] = clock + .45;
    }

    /// <summary>独立水层仅增强真实到达格的原有表面，不改变水深。</summary>
    /// <param name="row">本次发布的实际独立材料。</param>
    private Cultiway.Core.Visuals.Forms.FormLayerFrame IndependentConductionFrame(WorldMaterialRecord row)
    {
        var flow = row.Material.Flow;
        if (flow == null || row.Recovering || independentPulse.Count == 0) return null;
        int n = flow.Settings.Cells;
        var glow = new float[(n + 2) * (n + 2)];
        double now = World.world.getCurWorldTime();
        for (int i = 0; i < flow.Water.Length; i++)
            if (independentPulse.TryGetValue((row.Id, i), out double until))
                glow[(i / n + 1) * (n + 2) + i % n + 1] = .9f * Mathf.Clamp01((float)((until - now) / .45));
        return new Cultiway.Core.Visuals.Forms.FormLayerFrame(flow, conduction: glow);
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
