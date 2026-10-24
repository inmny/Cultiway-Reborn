using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>生成主体的支付份额、真实消耗与收束；不直接访问人物或修改资源。</summary>
public sealed partial class AbilityCast
{
    /// <summary>本次确实支付并交给生成主体的资源预算，不随伤害倍率扩大。</summary>
    public double PaidResource { get; private set; }
    /// <summary>已完成数值收束、等待来源玩法确认的累计资源。</summary>
    public double RecoveredResource { get; private set; }
    /// <summary>已转化回能的材料体积，不能同时留给后段或重复使用。</summary>
    public double RecoveredVolume { get; private set; }
    /// <summary>预测前的累计结果，回退不重复生成回收。</summary>
    private double savedRecoveredResource, savedRecoveredVolume;

    /// <summary>读取仍留在实际部位上的未耗支付份额。</summary>
    /// <param name="node">展开部位。</param>
    public double RemainingResource(int node) => !cancelled && nodes[node].Exists ? nodes[node].Resource : 0;

    /// <summary>支付只分配一次，未开始、后段和分件不能复制预算。</summary>
    /// <param name="amount">来源已确认支付的原资源数量。</param>
    private void AllocateResource(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        PaidResource = amount; if (amount == 0) return;
        double weight = Plan.Body == null ? 0 : 1;
        for (int i = 0; i < nodes.Length; i++) if (HasInitialResource(i)) weight += Plan.Nodes[i].Strength;
        if (weight == 0) return;
        for (int i = 0; i < nodes.Length; i++)
            if (HasInitialResource(i)) nodes[i].Resource = amount * (Plan.Nodes[i].Strength / weight);
    }

    /// <summary>真实生成部位取得份额；借用身体和空连接点没有可回收材料。</summary>
    /// <param name="node">编译部位。</param>
    private bool HasInitialResource(int node) => Plan.Nodes[node].FragmentSource < 0 && Plan.Nodes[node].RemainderRoot < 0 &&
        Plan.Nodes[node].Resource.Meshes.Length > 0;

    /// <summary>开始收束即停止作用，附属结构解除连接后保留自己的事实。</summary>
    /// <param name="node">真正收束的生成主体。</param>
    /// <param name="recovery">已检查操作。</param>
    private void StartRecovery(int node, CompiledRecovery recovery)
    {
        ref NodeState state = ref nodes[node];
        if (state.Recovering || !state.Exists) return;
        state.Burning.Extinguish();
        ReleaseNode(node);
        for (int child = node + 1; child < nodes.Length; child++)
            if (IsAttachedBelow(child, node))
                ReleaseNode(child);
        state.Scale = state.Transform.lossyScale;
        state.GrowthValue = 1f;
        state.Recovering = true;
        state.RecoveryInterrupted = false;
        state.Recovery = recovery;
        state.RecoveryAt = Age;
        state.RecoveryResource = state.Resource;
        state.RecoveryVolume = state.Volume;
        state.RecoveryProgress = 0f;
        state.Velocity = state.Acceleration = state.Spin = Vector3.zero;
        state.Force = 0;
        state.Revision++;
        state.Flying = false;
        state.MotionStopped = state.GrowthStopped = state.MovementStopped = true;
    }

    /// <summary>按世界时间转化真实份额，刚性形体缩小，水层水深真实降低。</summary>
    /// <param name="node">正在收束的部位。</param>
    private void AdvanceRecovery(int node)
    {
        ref NodeState state = ref nodes[node];
        float until = Mathf.Min(Age, state.StartedAt + Plan.Nodes[node].Presence.y);
        float progress = Mathf.Clamp01((until - state.RecoveryAt) / state.Recovery.Seconds);
        double delta = Math.Max(0, (double)progress - state.RecoveryProgress);
        state.PreviousTransform = state.Transform;
        if (flows?[node]?.Started == true)
        {
            AbilityFlow flow = flows[node];
            flow.Recover(state.RecoveryProgress < 1 ? (1 - progress) / (1 - state.RecoveryProgress) : 0);
            state.Volume = flow.Volume;
        }
        else
        {
            state.Volume = state.RecoveryVolume * (1 - progress);
            state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, state.Scale * Mathf.Pow(1 - progress, 1f / 3f));
            if (Plan.Nodes[node].FragmentSource >= 0) state.Transform *= Matrix4x4.Translate(-state.Pivot);
        }
        state.Resource = state.RecoveryResource * (1 - progress);
        RecoveredResource += state.RecoveryResource * state.Recovery.Efficiency * delta;
        RecoveredVolume += state.RecoveryVolume * delta;
        state.RecoveryEligible += state.RecoveryResource * state.Recovery.Efficiency * delta;
        state.RecoveryConverted += state.RecoveryVolume * delta;
        state.RecoveryProgress = progress; state.SampleEpoch = sampleEpoch;
        state.Visible = progress < 1;
        if (progress >= 1) { state.Resource = 0; state.Volume = 0; EndNode(node); }
        else if (Age >= state.StartedAt + Plan.Nodes[node].Presence.y) EndNode(node);
    }
}
