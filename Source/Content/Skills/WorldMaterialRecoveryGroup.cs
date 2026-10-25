using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Skills;

/// <summary>同一次范围收束按各原资源累计，跨分区和不同组成均不重复取整。</summary>
public sealed class WorldMaterialRecoveryGroup
{
    /// <summary>世界内稳定过程编号及原支付者。</summary>
    public long Id, Provider;
    /// <summary>每种原资源独立的已确认累计及精度余额。</summary>
    public MaterialRecoveryBook Credits = new();
    /// <summary>已结束分区留下的各原资源累计，最后一个分区退出才释放。</summary>
    public Dictionary<string, double> Completed = new(StringComparer.Ordinal);
}

/// <summary>共同水层按原支付者和实际资源合计，材料组成不同也不重复舍去整数。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>只保存仍存在的世界收束，不依赖旧施放。</summary>
    private static readonly Dictionary<long, WorldMaterialRecoveryGroup> recoveryGroups = new();

    /// <summary>主线程在数值片完成后，分别确认普通记录和共同水层的实际回能。</summary>
    private static void ConfirmWorldCredits()
    {
        long epoch = instance.generation;
        foreach (var row in rows.Values.ToArray())
            if (row.RecoveryGroup == 0)
            {
                Credit(row);
                if (epoch != instance.generation) return;
            }
        foreach (var group in recoveryGroups.Values.ToArray())
        {
            var members = rows.Values.Where(row => row.RecoveryGroup == group.Id).ToArray();
            if (members.Length == 0) continue;
            var eligible = new Dictionary<string, double>(group.Completed, StringComparer.Ordinal);
            foreach (var row in members) AddFunding(eligible, row);
            Actor actor = World.world.units.get(group.Provider);
            var owner = actor?.data?.id == group.Provider && actor.isAlive() && actor.hasHealth() ? actor.GetExtend() : null;
            foreach (var result in group.Credits.Credit(eligible, owner, () => epoch == instance.generation))
            {
                if (result.Requested > 0) Changed?.Invoke(new WorldMaterialChange(members[0], "credit", result.Granted, resource: result.Resource));
                if (epoch != instance.generation) return;
            }
        }
    }

    /// <summary>把一份材料已经收束的量分别记入各原资源。</summary>
    /// <param name="totals">本次独占的原资源累计。</param><param name="row">同一过程中的材料分区。</param>
    private static void AddFunding(Dictionary<string, double> totals, WorldMaterialRecord row)
    {
        foreach (var pair in row.Funding.Totals(row.InitialResource * row.Efficiency * row.Progress))
        { totals.TryGetValue(pair.Key, out double previous); totals[pair.Key] = previous + pair.Value; }
    }

    /// <summary>结束分区保留已收束累计，最后一份材料结束时移除共同过程。</summary>
    /// <param name="row">已经移除的真实记录。</param>
    private static void RetireRecovery(WorldMaterialRecord row)
    {
        if (row.RecoveryGroup == 0 || !recoveryGroups.TryGetValue(row.RecoveryGroup, out var group)) return;
        AddFunding(group.Completed, row);
        if (!rows.Values.Any(r => r.RecoveryGroup == group.Id)) recoveryGroups.Remove(group.Id);
    }

    /// <summary>外部共同过程只在读取边界检查金额及原资源。</summary>
    /// <param name="data">保存的独立过程。</param><param name="next">下一个稳定世界编号。</param>
    private static void RestoreRecoveryGroup(WorldMaterialRecoveryGroup data, long next)
    {
        if (data == null || data.Id <= 0 || data.Id >= next || data.Provider <= 0 || data.Completed == null ||
            data.Completed.Count > 65536 || data.Credits == null || data.Completed.Any(pair => !MaterialFunding.Finite(pair.Value)) || recoveryGroups.ContainsKey(data.Id))
            throw new InvalidDataException("世界材料共同回收账本无效");
        foreach (string resource in data.Completed.Keys.Concat(data.Credits.Entries?.Keys ?? Enumerable.Empty<string>()).Distinct())
            ConstructionResources.Validate(resource, true);
        recoveryGroups.Add(data.Id, data);
    }
}
