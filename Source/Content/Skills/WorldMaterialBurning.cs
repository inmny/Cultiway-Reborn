using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>独立木体的实际接触快照，不把留存主体伪造为活动施放。</summary>
internal readonly struct WorldThermalContact
{
    /// <summary>稳定世界编号和实际控制者。</summary>
    internal readonly long Id, Owner;

    /// <summary>本次接触形状、真实材料及当前有限热量。</summary>
    internal readonly AbilityCapsule Shape;

    /// <summary>只读材料规则。</summary>
    internal readonly AbilityMatter Matter;

    /// <summary>当前燃烧状态。</summary>
    internal readonly AbilityBurning Burning;

    /// <summary>当前剩余体积。</summary>
    internal readonly double Volume;

    /// <summary>从已经完成的世界状态建立本次快照。</summary>
    /// <param name="row">真实来源。</param><param name="shape">实际形状。</param>
    internal WorldThermalContact(WorldMaterialRecord row, AbilityCapsule shape)
    {
        Id = row.Id;
        Owner = row.Controller;
        Shape = shape;
        Matter = row.Material.Matter;
        Burning = row.Material.Burning;
        Volume = row.Material.Volume;
    }
}

/// <summary>世界留存木体的有限燃烧和真实接触传播。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>存在实际热量时，活动材料才额外采集传热接触。</summary>
    internal static bool HasHeat => rows.Values.Any(r => !r.Finished && r.Material.Burning.Heat > 0);

    /// <summary>从热源向活动材料查询，避免每个活动水格反向扫描世界。</summary>
    /// <param name="output">复用的真实热源集合。</param>
    internal static void HotWood(List<WorldThermalContact> output)
    {
        output.Clear();
        foreach (var row in rows.Values.Where(r => !r.Finished && r.Material.Volume > 0 && r.Material.Burning.Heat > 0)
                     .OrderBy(r => r.Id))
        {
            instance.shapes.Clear();
            instance.MaterialContacts(row, instance.shapes);
            foreach (var shape in instance.shapes) output.Add(new WorldThermalContact(row, shape));
        }
    }

    /// <summary>通过原空间索引取得可燃主体，普通木体之间不反复建立传播。</summary>
    /// <param name="bounds">真实查询范围。</param><param name="hotOnly">只取得仍可传热的来源。</param>
    /// <param name="output">复用结果。</param>
    internal static void QueryWood(Bounds bounds, bool hotOnly, List<WorldThermalContact> output)
    {
        output.Clear();
        instance.RebuildIndex();
        instance.index.Query(bounds, instance.nearby);
        foreach (int index in instance.nearby.OrderBy(i => instance.indexed[i].Id))
        {
            var row = instance.indexed[index];
            var material = row.Material;
            if (!AbilityBurning.CanBurn(material.Matter) || material.Volume <= 0 ||
                hotOnly && material.Burning.Transferable(material.Volume) <= 0) continue;
            instance.shapes.Clear();
            instance.MaterialContacts(row, instance.shapes);
            foreach (var shape in instance.shapes)
                if (AbilityContactIndex.Swept(shape, shape).Intersects(bounds))
                    output.Add(new WorldThermalContact(row, shape));
        }
    }

    /// <summary>确认时重新取得真实木体，失效候选不能消费热量。</summary>
    /// <param name="id">稳定世界编号。</param><param name="other">另一主体的已确认形状。</param>
    /// <param name="row">仍真正接触的世界来源。</param>
    internal static bool ReadWood(long id, AbilityCapsule other, out WorldMaterialRecord row)
    {
        if (!rows.TryGetValue(id, out row) || row.Finished || !AbilityBurning.CanBurn(row.Material.Matter) ||
            row.Material.Volume <= 0) return false;
        instance.shapes.Clear();
        instance.MaterialContacts(row, instance.shapes);
        foreach (var shape in instance.shapes)
            if (AbilityPairSweep.Find(shape, shape, other, other, out _, out _, out _))
                return true;
        return false;
    }

    /// <summary>世界主体接收尚缺的点火量，原来源在同一提交中扣除。</summary>
    /// <param name="id">真实来源。</param><param name="other">已确认热源形状。</param>
    /// <param name="available">真实可提供热量。</param><param name="source">原加热者。</param><param name="point">接触点。</param>
    internal static double HeatWood(long id, AbilityCapsule other, double available, long source, Vector3 point)
    {
        if (!ReadWood(id, other, out var row)) return 0;
        if (row.Material.Burning.Needed(row.Material.Volume) <= 0) return 0;
        if (row.Recovering) InterruptThermalRecovery(row);
        return row.Material.HeatWood(available, source, point);
    }

    /// <summary>只在数值任务完成后处理世界间传热，接收后不立即产生新一轮热量。</summary>
    /// <param name="clock">已推进到的真实时间。</param>
    private void ConfirmBurning(double clock)
    {
        if (!rows.Values.Any(r => r.Material.Burning.Heat > 0 || r.Material.Burning.Burned != r.ReportedBurned)) return;
        long epoch = generation;
        var sourceShapes = new List<AbilityCapsule>();
        var water = new List<QuenchingWater>();
        var wood = new List<WorldThermalContact>();
        var changed = new List<long>();
        foreach (var source in rows.Values.Where(r => !r.Finished && r.Material.Burning.Heat > 0).OrderBy(r => r.Id)
                     .ToArray())
        {
            sourceShapes.Clear();
            MaterialContacts(source, sourceShapes);
            foreach (var shape in sourceShapes)
            {
                Bounds bounds = AbilityContactIndex.Swept(shape, shape);
                QueryQuenchingWater(bounds, water);
                foreach (var candidate in water)
                {
                    double heat = source.Material.Burning.Heat;
                    if (heat <= 0) break;
                    if (!AbilityPairSweep.Find(shape, shape, candidate.Shape, candidate.Shape, out _, out var point,
                            out _)) continue;
                    double removed = EvaporateWater(candidate.Record, candidate.Shape.Shape, shape,
                        heat / AbilityQuenching.ForcePerVolume, changed, out bool touched);
                    if (!touched) continue;
                    double spent = removed > 0 ? Math.Min(heat, removed * AbilityQuenching.ForcePerVolume) : heat;
                    source.Material.Burning.Cool(spent);
                    if (removed > 0) ConstructedAbilityService.AddSteam(point, removed, clock);
                    ReportBurning(source, "cool", point, clock, spent);
                    if (epoch != generation) return;
                }

                if (source.Material.Burning.Transferable(source.Material.Volume) <= 0) continue;
                QueryWood(bounds, false, wood);
                foreach (var target in wood)
                {
                    double available = source.Material.Burning.Transferable(source.Material.Volume);
                    if (available <= 0) break;
                    if (target.Id == source.Id || !AbilityPairSweep.Find(shape, shape, target.Shape, target.Shape,
                            out _, out var point, out _)) continue;
                    long owner = source.Material.Burning.Source > 0
                        ? source.Material.Burning.Source
                        : source.Controller;
                    double amount = HeatWood(target.Id, shape, available, owner, point);
                    if (amount <= 0) continue;
                    source.Material.Burning.Cool(amount);
                    ReportBurning(rows[target.Id], "heat", point, clock, amount, sourceWorld: source.Id);
                    if (epoch != generation) return;
                }
            }
        }

        foreach (var row in rows.Values.OrderBy(r => r.Id).ToArray())
        {
            double delta = row.Material.Burning.Burned - row.ReportedBurned;
            row.ReportedBurned = row.Material.Burning.Burned;
            if (delta > 0) ReportBurning(row, "burn", row.Material.Transform.GetColumn(3), clock, delta);
            if (epoch != generation) return;
        }
    }

    /// <summary>世界数值提交后统一发布，不在接收尚未扣款时触发回调。</summary>
    /// <param name="row">真实世界主体。</param><param name="kind">结果类别。</param><param name="point">实际位置。</param>
    /// <param name="clock">共同世界时间。</param><param name="amount">真实变化量。</param>
    /// <param name="sourceCast">本次传热施放。</param><param name="sourceWorld">本次独立热源。</param>
    internal static void ReportBurning(WorldMaterialRecord row, string kind, Vector3 point, double clock, double amount,
        long sourceCast = 0, long sourceWorld = 0) =>
        ConstructedAbilityService.ReportWorldBurning(row.Id, kind, point, clock, amount, row.Material, sourceCast,
            sourceWorld);
}