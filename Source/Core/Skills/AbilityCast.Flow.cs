using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一部位从原形进入共同数值流散，保留材料、体积、身份与接触记录。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只有声明流散的部位才分配工作区及预测前态。</summary>
    private readonly AbilityFlow[] flows, savedFlows;

    /// <summary>当前尚在此部位中的实际材料体积。</summary>
    /// <param name="node">实际部位。</param>
    public double RemainingVolume(int node) => nodes[node].Volume;

    /// <summary>流散范围也进入主线程地表刷新，不能只刷新原形中心。</summary>
    /// <param name="node">实际部位。</param>
    internal Rect? FlowBounds(int node) => flows?[node]?.Started == true ?
        new Rect(flows[node].Origin, Vector2.one * flows[node].Cell * flows[node].Settings.Cells) : null;

    /// <summary>从已有姿态、运动和体积开始；重复事件不能再创建一份水量。</summary>
    /// <param name="node">实际开始的部位。</param>
    private void StartFlow(int node)
    {
        AbilityFlow flow = flows[node]; if (flow.Started) return;
        ref NodeState state = ref nodes[node]; ReleaseNode(node);
        Bounds bounds = poses[node].Bounds; Matrix4x4 matrix = state.Transform;
        Vector3 extent = Vector3.zero;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 p = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            Vector3 d = matrix.MultiplyPoint3x4(p) - matrix.MultiplyPoint3x4(bounds.center);
            extent = Vector3.Max(extent, new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z)));
        }
        float scale = MaximumScale(matrix);
        flow.Start(matrix.MultiplyPoint3x4(bounds.center), extent, scale, state.Volume, state.Velocity, terrain);
        if (state.ShapeHeld) flow.StopMotion();
        flow.SourceTransform = matrix;
        state.Revision++; state.FlowAt = NodeAge(node); state.Flying = false; state.MovementStopped = false; state.Acceleration = Vector3.zero;
    }

    /// <summary>真实世界时间推进流散，保持态和明确停止保留当前形状。</summary>
    /// <param name="node">实际部位。</param><param name="step">本次有效流散时间。</param>
    /// <param name="target">本轮已经采集的实际追踪位置。</param>
    private void AdvanceFlow(int node, float step, Vector3 target)
    {
        ref NodeState state = ref nodes[node]; AbilityFlow flow = flows[node];
        if (!state.ShapeHeld && !state.MovementStopped)
        {
            if (state.Flying)
            {
                Vector3 velocity = state.Velocity;
                if (state.Flight.TurnRate > 0 && velocity.sqrMagnitude > .000001f)
                {
                    Vector3 direction = (state.HasTarget ? state.Target : target) - flow.Centre;
                    direction.z = 0;
                    if (direction.sqrMagnitude > .000001f)
                        velocity = Vector3.RotateTowards(velocity, direction.normalized * velocity.magnitude,
                            state.Flight.TurnRate * Mathf.Deg2Rad * step, 0);
                }
                flow.SetVelocity(velocity + state.Acceleration * step);
            }

            double beforeVolume = flow.Volume, beforeLoss = flow.Escaped + flow.Evaporated;
            flow.RefreshGround(terrain);
            flow.Advance(step);
            // 只按真实外流及蒸散扣份额；闭合通量的浮点舍入不成为可累计的资源损耗。
            double loss = System.Math.Max(0, flow.Escaped + flow.Evaporated - beforeLoss);
            if (beforeVolume > 0) state.Resource *= System.Math.Max(0, 1 - loss / beforeVolume);
        }

        state.Volume = flow.Volume;
        state.Position = flow.Centre;
        state.Velocity = flow.Velocity;
        state.Transform.m03 = state.Position.x;
        state.Transform.m13 = state.Position.y;
        state.Transform.m23 = state.Position.z;
    }

    /// <summary>每格采用真实地面至水面的薄体，宽度不再同时决定空中作用高度。</summary>
    /// <param name="node">所属的共同部位。</param><param name="output">本轮实际作用区。</param>
    /// <param name="includeEnd">保留刚消退处最后仍湿润的路径。</param>
    private void AppendFlowContacts(int node, List<AbilityCapsule> output, bool includeEnd)
    {
        AbilityFlow flow = flows[node]; NodeState state = nodes[node]; int n = flow.Settings.Cells;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            int index = y * n + x; float depth = flow.Water[index].x;
            float dry = flow.Cell * AbilityFlow.DryFraction, until = 1f;
            if (depth < dry)
            {
                float before = includeEnd && savedFlows[node].Started ? savedFlows[node].Water[index].x : 0f;
                if (before < dry) continue;
                until = Mathf.Clamp01((before - dry) / (before - depth));
            }
            var centre = new Vector3(flow.Origin.x + (x + .5f) * flow.Cell, flow.Origin.y + (y + .5f) * flow.Cell, flow.Ground[index] + depth * .5f);
            output.Add(new AbilityCapsule(node, centre, centre, 0f, state.Matter.Id, Plan.Nodes[node].Resource.Contacts.Length + index, state.Revision, state.Matter,
                new Vector3(flow.Cell * .5f, flow.Cell * .5f, depth * .5f), until));
        }
    }
}
