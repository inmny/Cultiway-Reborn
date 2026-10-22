using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>活动水体只导出数值水格；外部确认传播后，才记录短暂表面反馈。</summary>
public sealed partial class AbilityCast
{
    /// <summary>每个部位最多64乘64格，以固定间隔同时标识部位和原水格。</summary>
    internal const int ConductionStride = 64 * 64;

    /// <summary>已确认到达水格的本次年龄截止点；显示反馈不改变物理和支付。</summary>
    private readonly Dictionary<int, float> conductionGlow = new();

    /// <summary>在共同确认时点读取当前水格，不复制体积或创建世界材料。</summary>
    /// <param name="source">活动施放的数值标识。</param><param name="cell">共同显示格宽。</param>
    /// <param name="output">本次水路快照。</param>
    internal void AppendConductingWater(long source, float cell, List<ConductingWater> output)
    {
        if (Finished || flows == null) return;
        for (int node = 0; node < flows.Length; node++)
        {
            var flow = flows[node];
            if (flow?.Started != true || !nodes[node].Exists || nodes[node].Matter.Id != "water" ||
                nodes[node].Matter.State != "liquid") continue;
            for (int i = 0; i < flow.Water.Length; i++)
                if (flow.Water[i].x > 0)
                    output.Add(ConductingCell(source, node, i, cell));
        }
    }

    /// <summary>回调改变部位后重新读取真实水格；零攻击力量本身不删除水。</summary>
    /// <param name="previous">原确认水格。</param><param name="depth">最低实际水深。</param>
    /// <param name="cell">共同显示格宽。</param><param name="current">仍存在的当前水格。</param>
    internal bool ReadConductingWater(in ConductingWater previous, float depth, float cell,
        out ConductingWater current)
    {
        current = default;
        if (Finished) return false;
        int node = previous.Index / ConductionStride, index = previous.Index % ConductionStride;
        var flow = flows[node];
        if (!nodes[node].Exists || nodes[node].Matter.Id != "water" || nodes[node].Matter.State != "liquid" ||
            flow.Water[index].x < depth) return false;
        current = ConductingCell(previous.Source, node, index, cell);
        return current.Area == previous.Area;
    }

    /// <summary>同一水格的形状和重新确认使用同一次实际边缘计算。</summary>
    /// <param name="source">本次施放标识。</param><param name="node">真实部位。</param>
    /// <param name="index">原水格。</param><param name="cell">共同显示格宽。</param>
    private ConductingWater ConductingCell(long source, int node, int index, float cell)
    {
        var flow = flows[node];
        Rect area = ConductingWater.FlowArea(flow, index);
        return new ConductingWater(Mathf.FloorToInt(area.center.x / cell), Mathf.FloorToInt(area.center.y / cell),
            flow.Ground[index], flow.Water[index].x, area, source, node * ConductionStride + index);
    }

    /// <summary>只能由主线程对已到达的实际水格调用，预测不会生成反馈。</summary>
    /// <param name="index">部位及原水格的共同编号。</param>
    internal void PulseConduction(int index) => conductionGlow[index] = Age + FormLayerFrame.ConductionSeconds;

    /// <summary>按原部位发布不可变明暗，不改变水量、动量或接触范围。</summary>
    /// <param name="node">实际水层部位。</param>
    private FormLayerFrame ConductingFrame(int node)
    {
        var flow = flows[node];
        if (conductionGlow.Count == 0) return new FormLayerFrame(flow);
        int n = flow.Settings.Cells;
        var glow = new float[(n + 2) * (n + 2)];
        for (int i = 0; i < flow.Water.Length; i++)
            if (conductionGlow.TryGetValue(node * ConductionStride + i, out float until))
            {
                glow[(i / n + 1) * (n + 2) + i % n + 1] = FormLayerFrame.ConductionStrength *
                                                          Mathf.Clamp01(
                                                              (until - Age) / FormLayerFrame.ConductionSeconds);
                if (until <= Age) conductionGlow.Remove(node * ConductionStride + i);
            }

        return new FormLayerFrame(flow, conduction: glow);
    }
}