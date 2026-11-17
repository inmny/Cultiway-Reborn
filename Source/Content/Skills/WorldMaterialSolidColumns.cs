using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>同一原始定形层内，一种材料的共同厚度和实际基底。</summary>
internal sealed class WorldSolidColumn
{
    /// <summary>各自维护份额的真实记录。</summary>
    internal readonly List<WorldMaterialRecord> Rows;
    /// <summary>共同深度与按材料层次得到的基底，不修改原始冻结地表。</summary>
    internal readonly float[] Height, Ground;
    /// <summary>从同源材料建立本轮共同柱。</summary>
    /// <param name="rows">同一种材料的实际记录。</param><param name="ground">本层当前的基底。</param>
    internal WorldSolidColumn(List<WorldMaterialRecord> rows, float[] ground)
    {
        Rows = rows; Ground = (float[])ground.Clone(); Height = new float[ground.Length];
        foreach (var row in rows)
            for (int i = 0; i < Height.Length; i++) Height[i] += row.Material.Flow.Water[i].x;
    }
}

/// <summary>拆分定形层共同显示与选择，原支付和过程仍由各份材料独立维护。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>本轮按记录直接回查共同柱。</summary>
    private readonly Dictionary<long, WorldSolidColumn> solidColumns = new();
    /// <summary>本轮完整发布的共同柱。</summary>
    private readonly List<WorldSolidColumn> solidLayers = new();

    /// <summary>先按原冻结地表分组，再给不同材料分层，最后建立实际查询。</summary>
    private void UpdateSolidColumns()
    {
        solidColumns.Clear(); solidLayers.Clear();
        var buckets = rows.Values.Where(r => !r.Finished && !r.Recovering && !r.WorldGrid && r.Material.ShapeHeld && r.Material.Flow != null)
            .GroupBy(r => (r.Lineage, r.Material.Flow.Origin, r.Material.Flow.Cell, r.Material.Cells, BedHash(r.Material.Flow.Ground)));
        foreach (var bucket in buckets)
        {
            var beds = new List<List<WorldMaterialRecord>>();
            foreach (var row in bucket)
            {
                var group = beds.FirstOrDefault(group => group[0].Material.Flow.Ground.SequenceEqual(row.Material.Flow.Ground));
                if (group == null) beds.Add(group = new List<WorldMaterialRecord>());
                group.Add(row);
            }
            foreach (var bed in beds)
            {
                var basis = (float[])bed[0].Material.Flow.Ground.Clone();
                foreach (var material in bed.GroupBy(r => (r.Material.Matter.Id, r.Material.Matter.State)).OrderBy(g => g.Key.Id, StringComparer.Ordinal).ThenBy(g => g.Key.State, StringComparer.Ordinal))
                {
                    var layer = new WorldSolidColumn(material.OrderBy(r => r.Id).ToList(), basis);
                    solidLayers.Add(layer);
                    foreach (var row in layer.Rows) solidColumns.Add(row.Id, layer);
                    for (int i = 0; i < basis.Length; i++) basis[i] += layer.Height[i];
                }
            }
        }
    }

    /// <summary>冻结地表哈希只缩小分组，仍逐值确认相同地表。</summary>
    /// <param name="ground">原始冻结地表。</param>
    private static int BedHash(float[] ground)
    {
        int hash = 17;
        foreach (float value in ground) hash = unchecked(hash * 31 + value.GetHashCode());
        return hash;
    }

    /// <summary>共同格网和独立定形层使用同一实际水柱选择。</summary>
    /// <param name="row">实际记录。</param><param name="selection">本次真实形状。</param><param name="picked">接收选中格子。</param>
    private bool SelectMaterial(WorldMaterialRecord row, IReadOnlyList<AbilityCapsule> selection, List<int> picked)
    {
        if (row.WorldGrid && !row.Recovering && waterColumns.TryGetValue(WaterKey(row), out var water))
            return row.Material.SelectShared(selection, picked, water);
        if (!row.Recovering && solidColumns.TryGetValue(row.Id, out var solid))
            return row.Material.SelectShared(selection, picked, solid.Height, solid.Ground);
        return row.Material.Select(selection, picked);
    }

    /// <summary>显示、范围筛选和真实接触共用当前厚度及基底。</summary>
    /// <param name="row">实际记录。</param><param name="output">复用形状缓冲。</param>
    private void MaterialContacts(WorldMaterialRecord row, List<AbilityCapsule> output)
    {
        if (row.WorldGrid && !row.Recovering && waterColumns.TryGetValue(WaterKey(row), out var water))
            row.Material.SharedContacts(output, water);
        else if (!row.Recovering && solidColumns.TryGetValue(row.Id, out var solid))
            row.Material.SharedContacts(output, solid.Height, solid.Ground);
        else row.Material.Contacts(output);
    }

    /// <summary>按共同总厚度发布一次表面，外观分区沿用共同曲面的现有规则。</summary>
    /// <param name="result">本次完整发布。</param>
    private void AddSolidVisuals(List<FormVisualInstance> result)
    {
        UpdateSolidColumns();
        foreach (var layer in solidLayers)
        {
            var first = layer.Rows[0]; var flow = first.Material.Flow; int n = flow.Settings.Cells;
            var total = new AbilityFlow(flow.Settings) { Origin = flow.Origin, Cell = flow.Cell, Started = true };
            Array.Copy(layer.Ground, total.Ground, layer.Ground.Length);
            for (int i = 0; i < layer.Height.Length; i++) total.Water[i] = new Vector3(layer.Height[i], 0, 0);
            total.RefreshTotals();
            var looks = layer.Rows.GroupBy(Appearance).Select(g => g.First()).ToArray();
            float[][] masks = looks.Length > 1 ? looks.Select(_ => new float[(n + 2) * (n + 2)]).ToArray() : null;
            if (masks != null)
                for (int i = 0; i < layer.Height.Length; i++)
                {
                    int winner = 0; float largest = -1;
                    for (int look = 0; look < looks.Length; look++)
                    {
                        var appearance = Appearance(looks[look]);
                        float amount = layer.Rows.Where(r => Appearance(r).Equals(appearance)).Sum(r => r.Material.Flow.Water[i].x);
                        if (amount > largest) { largest = amount; winner = look; }
                    }
                    masks[winner][(i / n + 1) * (n + 2) + i % n + 1] = 1;
                }
            for (int look = 0; look < looks.Length; look++)
                result.Add(looks[look].Material.Visual(terrain, new FormLayerFrame(total, looks[look].Material.Flow.VisualOwner,
                    masks: masks, appearance: masks == null ? -1 : look, sources: layer.Rows.Count, mixedCells: Enumerable.Range(0, layer.Height.Length).Count(i => layer.Rows.Count(r => r.Material.Flow.Water[i].x > 1e-7f) > 1))));
        }
    }
}
