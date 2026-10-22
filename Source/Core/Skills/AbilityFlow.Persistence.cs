using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>世界材料的水层数值保存与分割记账。</summary>
internal sealed partial class AbilityFlow
{
    /// <summary>共同世界水层逐格保存的原支付余量，普通施放为空。</summary>
    internal double[] Budget;

    /// <summary>逐格未耗资源的当前合计。</summary>
    internal double Resource => Budget == null ? 0 : Budget.Sum();

    /// <summary>已经交给其他世界记录的真实体积，不记作外流或蒸散。</summary>
    internal double Transferred;

    /// <summary>分割之后重新统计，不推进时间。</summary>
    internal void RefreshTotals() => Recount();

    /// <summary>保存当前水深、动量及去向，不保存工作缓冲或地表服务。</summary>
    /// <param name="held">保持态需要保存当时的真实形态基底。</param>
    internal AbilityFlowData Capture(bool held)
    {
        return new AbilityFlowData
        {
            Settings = new ConstructionFlow
            {
                Cells = Settings.Cells,
                Size = Settings.Size,
                Gravity = Settings.Gravity,
                Drag = Settings.Drag,
                Evaporation = Settings.Evaporation,
                OpenBoundary = Settings.OpenBoundary
            },
            Budget = Budget == null ? null : (double[])Budget.Clone(),
            Cell = Cell,
            X = Origin.x,
            Y = Origin.y,
            Water = Water.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
            Bed = held ? (float[])Ground.Clone() : null,
            Escaped = Escaped,
            Evaporated = Evaporated,
            Recovered = Recovered,
            Transferred = Transferred
        };
    }

    /// <summary>在正式资料边界核对，随后创建独立工作区。</summary>
    /// <param name="data">实际保存的有限水层。</param>
    internal static AbilityFlow Restore(AbilityFlowData data)
    {
        bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        if (data?.Settings == null) throw new InvalidDataException("世界水层缺少数值设置");
        var settings = new CompiledFlow(data.Settings, "世界水层");
        int count = settings.Cells * settings.Cells;
        if (!Finite(data.Cell) || data.Cell <= 0 || data.Cell > 1000000 || !Finite(data.X) || !Finite(data.Y) ||
            data.Water == null || data.Water.Length != count * 3 || data.Water.Any(v => !Finite(v)) ||
            data.Bed != null && (data.Bed.Length != count || data.Bed.Any(v => !Finite(v))) ||
            !Finite(data.Escaped) || data.Escaped < 0 || !Finite(data.Evaporated) || data.Evaporated < 0 ||
            !Finite(data.Recovered) || data.Recovered < 0 || !Finite(data.Transferred) || data.Transferred < 0)
            throw new InvalidDataException("世界水层尺寸、形态或水量去向无效");
        if (data.Budget != null && (data.Budget.Length != count || data.Budget.Any(v => !Finite(v) || v < 0)))
            throw new InvalidDataException("世界水层逐格支付余量无效");
        var flow = new AbilityFlow(settings)
        {
            Cell = data.Cell,
            Origin = new Vector2(data.X, data.Y),
            Started = true,
            Escaped = data.Escaped,
            Evaporated = data.Evaporated,
            Recovered = data.Recovered,
            Transferred = data.Transferred
        };
        for (int i = 0; i < count; i++)
        {
            int at = i * 3;
            if (data.Water[at] < 0) throw new InvalidDataException("世界水层不能有负水深");
            flow.Water[i] = new Vector3(data.Water[at], data.Water[at + 1], data.Water[at + 2]);
        }

        if (data.Bed != null) Array.Copy(data.Bed, flow.Ground, count);
        flow.Budget = data.Budget == null ? null : (double[])data.Budget.Clone();
        flow.Recount();
        return flow;
    }
}