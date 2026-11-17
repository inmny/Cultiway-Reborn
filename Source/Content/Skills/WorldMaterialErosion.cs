using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次施放对真实世界过程的等待，材料本身独立保存。</summary>
internal sealed class WorldErosionWatch
{
    /// <summary>真实过程和等待步骤。</summary>
    internal long Process;
    /// <summary>未命名的原操作为负一。</summary>
    internal readonly int Step;
    /// <summary>真实过程的结果。</summary>
    internal ConstructionStepState State = ConstructionStepState.Running;
    /// <summary>只在完成或中断后冻结的事实。</summary>
    internal ConstructionWorldReceipt Receipt;
    /// <summary>已经离开记录集合的本段损失。</summary>
    internal double RemovedVolume, RemovedResource;
    /// <summary>本轮累计事实及仍存在的材料。</summary>
    private double volume, resource, remainingVolume, remainingResource;
    /// <summary>本轮是否存在实际材料、继续过程或中断。</summary>
    private bool found, active, interrupted;
    /// <summary>重复使用的当前材料身份。</summary>
    private readonly List<long> records = new();
    /// <summary>同一材料跨记录仍只输出一次身份。</summary>
    private readonly HashSet<long> identities = new();

    /// <summary>记录实际等待位置。</summary>
    /// <param name="step">真实步骤。</param>
    internal WorldErosionWatch(int step) { Step = step; }
    /// <summary>每次只从当前事实重新合计，不累加上一轮快照。</summary>
    internal void Prepare()
    {
        volume = RemovedVolume; resource = RemovedResource; remainingVolume = remainingResource = 0;
        found = active = interrupted = false; records.Clear(); identities.Clear();
    }
    /// <summary>读取同一过程的当前材料，完整耗尽仍可提供真实完成。</summary>
    /// <param name="row">实际记录。</param>
    internal void Include(WorldMaterialRecord row)
    {
        var process = row.Material.Erosion;
        found = true; volume += process.LostVolume; resource += process.SpentResource;
        active |= process.Active && row.Material.Volume > 0 && !row.Finished;
        interrupted |= process.Interrupted || process.Progress < 1 && (row.Finished || row.Material.Volume <= 0);
        if (row.Material.Volume <= 0 || row.Finished) return;
        remainingVolume += row.Material.Volume; remainingResource += row.Material.Resource;
        records.Add(row.Id); identities.Add(row.Identity);
    }
    /// <summary>只有所有实际部分完成，才放行完成依赖。</summary>
    /// <param name="clock">共同确认时点。</param>
    internal void Resolve(double clock)
    {
        if (active && !interrupted) return;
        State = interrupted || !found ? ConstructionStepState.Interrupted : ConstructionStepState.Completed;
        Receipt = new ConstructionWorldReceipt(ConstructionWorldResultKind.Erosion, -1, clock,
            materials: records.OrderBy(id => id).ToArray(), materialIdentities: identities.OrderBy(id => id).ToArray(), volume: remainingVolume, resource: remainingResource, erodedVolume: volume, erodedResource: resource);
    }
    /// <summary>被后一次真实操作替换，已经记录的损失仍保留。</summary>
    /// <param name="clock">真实替换时点。</param>
    internal void Interrupt(double clock) { interrupted = true; Resolve(clock); }
}

/// <summary>真实范围侵蚀沿用已有材料选择和共同数值维护。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>只维护仍被活动施放等待的过程，结束时释放。</summary>
    private static readonly Dictionary<long, WorldErosionWatch> erosionWatches = new();

    /// <summary>完整实体按实际形状、归属及容量参与，曲面局部分割继续后批。</summary>
    /// <param name="controller">本次控制者。</param><param name="selection">真实选择形状。</param>
    /// <param name="settings">已编译过程。</param><param name="clock">确认时点。</param>
    /// <param name="cast">此次施放。</param><param name="ability">实际构造。</param>
    /// <param name="step">等待步骤。</param><param name="identities">明确输入，空集合保持空选择。</param>
    internal static WorldErosionWatch Erode(long controller, IReadOnlyList<AbilityCapsule> selection, CompiledErosion settings,
        double clock, long cast, string ability, int step, IReadOnlyList<long> identities)
    {
        instance.RebuildIndex();
        UpdateErosionWatches(clock);
        var targets = new HashSet<long>();
        foreach (var shape in selection)
        {
            instance.index.Query(AbilityContactIndex.Swept(shape, shape), instance.nearby);
            foreach (int i in instance.nearby) targets.Add(instance.indexed[i].Id);
        }
        var watch = new WorldErosionWatch(step); var affected = new List<WorldMaterialRecord>();
        double remaining = settings.World.MaximumVolume;
        foreach (long id in targets.OrderBy(id => id))
        {
            var row = rows[id]; var material = row.Material;
            if (row.Finished || row.Recovering || identities != null && !identities.Contains(row.Identity) ||
                settings.World.OwnedOnly && row.Provider != controller || !settings.World.Matches(material.Matter) ||
                material.Volume > remaining || !settings.CanAffect(material.Matter, material.Volume)) continue;
            bool selected = row.WorldGrid ? material.SelectShared(selection, instance.picked, instance.waterColumns[WaterKey(row)]) : material.Select(selection, instance.picked);
            if (!selected || material.Flow != null && instance.picked.Count != material.Flow.Water.Count(water => water.x > 0)) continue;
            if (erosionWatches.TryGetValue(material.Erosion.Id, out var previous) && previous.State == ConstructionStepState.Running) previous.Interrupt(clock);
            if (watch.Process == 0) watch.Process = nextId++;
            material.BeginErosion(settings); material.Erosion.Id = watch.Process;
            remaining -= material.Volume; affected.Add(row);
        }
        if (affected.Count == 0)
        {
            watch.Prepare(); watch.Resolve(clock); return watch;
        }
        erosionWatches.Add(watch.Process, watch);
        UpdateErosionWatches(clock);
        long epoch = instance.generation;
        foreach (var row in affected)
        {
            Changed?.Invoke(new WorldMaterialChange(row, "erode", castId: cast, abilityId: ability));
            if (epoch != instance.generation) return null;
        }
        return watch;
    }

    /// <summary>每次共同材料时间片只合计一次，避免每个施放反复扫世界。</summary>
    /// <param name="clock">已确认世界时间。</param>
    private static void UpdateErosionWatches(double clock)
    {
        if (erosionWatches.Count == 0) return;
        foreach (var watch in erosionWatches.Values) if (watch.State == ConstructionStepState.Running) watch.Prepare();
        foreach (var row in rows.Values)
            if (erosionWatches.TryGetValue(row.Material.Erosion.Id, out var watch) && watch.State == ConstructionStepState.Running) watch.Include(row);
        foreach (var watch in erosionWatches.Values) if (watch.State == ConstructionStepState.Running) watch.Resolve(clock);
    }

    /// <summary>完整耗尽的部分先保留本段损失，后续部分不会漏掉它。</summary>
    /// <param name="row">即将移除的真实记录。</param>
    private static void CarryErosionResult(WorldMaterialRecord row)
    {
        if (!erosionWatches.TryGetValue(row.Material.Erosion.Id, out var watch) || watch.State != ConstructionStepState.Running) return;
        watch.RemovedVolume += row.Material.Erosion.LostVolume;
        watch.RemovedResource += row.Material.Erosion.SpentResource;
    }
    /// <summary>原施放完成或中断后，世界材料继续自己的过程。</summary>
    /// <param name="watch">原施放持有的等待。</param>
    internal static void ReleaseErosion(WorldErosionWatch watch) => erosionWatches.Remove(watch.Process);
}
