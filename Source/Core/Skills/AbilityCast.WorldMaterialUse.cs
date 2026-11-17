using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>生成材料交给原版地块后退出当前主体，保留余量供原流程继续。</summary>
public sealed partial class AbilityCast
{
    /// <summary>预估和正式接触只读取实际生成部位及其局部水格。</summary>
    /// <param name="node">实际部位，身体为负一。</param><param name="shape">真实形状编号。</param>
    /// <param name="volume">操作要求的体积。</param>
    internal bool CanUseWorldMaterial(int node, int shape, double volume)
    {
        if (volume == 0) return true;
        if (cancelled || node < 0 || !nodes[node].Exists || nodes[node].Recovering || nodes[node].Leaving ||
            Plan.Nodes[node].Resource.Meshes.Length == 0) return false;
        var flow = flows?[node];
        double available = nodes[node].Volume;
        float represented = nodes[node].MaterialRetained;
        if (flow?.Started == true)
        {
            int cell = shape - Plan.Nodes[node].Resource.Contacts.Length;
            if (cell < 0 || cell >= flow.Water.Length) return false;
            represented = flow.Water[cell].x;
            available = represented * (double)flow.Cell * flow.Cell;
        }

        // 极小份额须能真正改变实体或水深，避免舍入为零后仍取得永久结果。
        return available >= volume && represented * (float)Math.Max(0, 1 - volume / available) < represented;
    }

    /// <summary>只在原版结果确实成功后调用，先移除材料再通知后续过程。</summary>
    /// <param name="node">已检查的生成部位。</param><param name="shape">已确认的实际水格或实体形状。</param>
    /// <param name="volume">本次交出的体积。</param><param name="point">共同接触位置。</param>
    /// <param name="resource">随材料退出的原支付份额。</param>
    internal double UseWorldMaterial(int node, int shape, double volume, Vector3 point, out double resource)
    {
        resource = 0;
        if (volume == 0) return 0;
        ref NodeState state = ref nodes[node];
        double before = state.Volume;
        var flow = flows?[node];
        double removed = volume;
        if (flow?.Started == true)
        {
            removed = flow.Transfer(shape - Plan.Nodes[node].Resource.Contacts.Length, volume);
            state.Volume = flow.Volume;
        }
        else
        {
            state.Volume = Math.Max(0, before - volume);
            float retained = (float)(state.Volume / before);
            state.MaterialRetained *= retained;
            state.Transform *= Matrix4x4.Scale(Vector3.one * Mathf.Pow(retained, 1f / 3f));
        }

        double ratio = Math.Max(0, 1 - removed / before);
        resource = state.Resource * (1 - ratio);
        state.Resource -= resource;
        state.Burning.Retain(ratio);
        float force = state.Force;
        state.Force *= (float)ratio;
        state.Revision++;
        if (force > state.Force && state.InitialForce > 0)
            SignalEvent(node, ConstructionChangeTrigger.ForceBelow, point, force / state.InitialForce,
                state.Force / state.InitialForce);
        if (state.Volume <= before * .0000001)
        {
            state.Volume = state.Resource = 0;
            state.Force = 0;
            for (int child = node + 1; child < nodes.Length; child++)
                if (nodes[child].Exists && !nodes[child].Free && Plan.Nodes[child].Parent == node)
                    ReleaseNode(child);
            EndNode(node);
        }

        FlushBirths(inputs.Target);
        return removed;
    }
}
