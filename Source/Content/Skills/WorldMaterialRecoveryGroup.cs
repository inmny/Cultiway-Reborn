using System;
using System.IO;
using System.Linq;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Skills;

/// <summary>同一次范围收束的原资源账本，避免分区数量改变整数回收结果。</summary>
public sealed class WorldMaterialRecoveryGroup
{
    /// <summary>世界内的稳定过程编号及原支付者。</summary>
    public long Id, Provider;

    /// <summary>原资源种类。</summary>
    public string Resource;

    /// <summary>已经确认的累计、因精度未表示的余额，以及已结束分区的累计。</summary>
    public double Confirmed, Fraction, Completed;
}

/// <summary>共同水层的回收按原支付者和资源累计，不在每个内部区域分别舍去整数。</summary>
public sealed partial class WorldMaterialService
{
    /// <summary>只维护实际存在的世界收束，不保存旧施放。</summary>
    private static readonly System.Collections.Generic.Dictionary<long, WorldMaterialRecoveryGroup> recoveryGroups =
        new();

    /// <summary>数值片完成后，普通记录沿用自身累计，共同水层按同次收束汇总。</summary>
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
            double eligible = group.Completed;
            foreach (var row in members)
            {
                double amount = row.InitialResource * row.Efficiency * row.Progress;
                eligible += amount;
                row.Confirmed = amount;
            }

            double total = ConstructionResources.IsInteger(group.Resource) ? Math.Floor(eligible + 1e-8) : eligible;
            double request = Math.Max(0, total - group.Confirmed);
            group.Confirmed = Math.Max(group.Confirmed, total);
            double available = request + group.Fraction;
            group.Fraction = 0;
            if (ConstructionResources.IsInteger(group.Resource)) available = Math.Min(available, int.MaxValue);
            Actor actor = World.world.units.get(group.Provider);
            double granted = 0;
            if (available > 0 && actor?.data?.id == group.Provider && actor.isAlive() && actor.hasHealth())
                granted = MaterialRecoveryCredit.Grant(actor.GetExtend(), group.Resource, available,
                    out group.Fraction);
            if (request > 0) Changed?.Invoke(new WorldMaterialChange(members[0], "credit", granted));
            if (epoch != instance.generation) return;
        }
    }

    /// <summary>分区结束保留其累计，最后一个分区退出才释放共同账本。</summary>
    /// <param name="row">已经移除的真实记录。</param>
    private static void RetireRecovery(WorldMaterialRecord row)
    {
        if (row.RecoveryGroup == 0 || !recoveryGroups.TryGetValue(row.RecoveryGroup, out var group)) return;
        group.Completed += row.InitialResource * row.Efficiency * row.Progress;
        if (!rows.Values.Any(r => r.RecoveryGroup == group.Id)) recoveryGroups.Remove(group.Id);
    }

    /// <summary>外部存档中的共同账本只在读取边界检查。</summary>
    /// <param name="data">保存的独立回收过程。</param><param name="next">下一个稳定世界编号。</param>
    private static void RestoreRecoveryGroup(WorldMaterialRecoveryGroup data, long next)
    {
        bool Amount(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0;
        if (data == null || data.Id <= 0 || data.Id >= next || data.Provider <= 0 ||
            data.Resource != "none" && ConstructionResources.Resolve(data.Resource)?.MaximumAmount == null ||
            !Amount(data.Confirmed) || !Amount(data.Fraction) || !Amount(data.Completed) ||
            data.Fraction > data.Confirmed ||
            recoveryGroups.ContainsKey(data.Id)) throw new InvalidDataException("世界材料共同回收账本无效");
        recoveryGroups.Add(data.Id, data);
    }
}