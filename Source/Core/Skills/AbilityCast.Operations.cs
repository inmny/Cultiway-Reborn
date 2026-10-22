using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一主体上的一次性条件运动，不增加施放或重置接触预算。</summary>
public sealed partial class AbilityCast
{
    /// <summary>独占的条件操作状态及预测前态。</summary>
    private readonly OperationState[] operations, savedOperations;
    /// <summary>一次整体采样的标记，避免事件提前采样某个部位后又重复推进。</summary>
    private long sampleEpoch;
    /// <summary>是否正在一次数值采样中。</summary>
    private bool sampling;
    /// <summary>本轮共同步长和目标。</summary>
    private float samplingStep;
    /// <summary>只含数值的本轮目标。</summary>
    private Vector3 samplingTarget;

    /// <summary>接受一次实际事件；未存在的对象不会凭操作自行复活。</summary>
    /// <param name="index">共用计划内的操作索引。</param>
    /// <param name="eventMatter">事件发生时的材料，不读取派发中的后续变化。</param>
    /// <param name="before">事件发生前的真实数值。</param>
    /// <param name="after">事件发生后的真实数值。</param>
    private void ReceiveOperation(int index, AbilityMatter eventMatter, float before, float after)
    {
        CompiledEventOperation operation = Plan.EventOperations[index];
        ref OperationState execution = ref operations[index]; NodeState state = nodes[operation.Node];
        if (execution.Received || !operation.When.Crossed(before, after) || !state.Exists || !state.Started && (float.IsPositiveInfinity(state.BirthAt) || operation.At > 0f) ||
            state.Started && NodeAge(operation.Node) < operation.At || operation.When.Matter != null && !ReferenceEquals(operation.When.Matter, eventMatter)) return;
        execution.Received = true; execution.Due = Age + operation.When.Delay;
        if (execution.Due > Age || !state.Started) return;
        if (sampling && nodes[operation.Node].SampleEpoch == -sampleEpoch) return;
        if (sampling && nodes[operation.Node].SampleEpoch != sampleEpoch)
            SampleNode(operation.Node, samplingStep, samplingTarget, false);
        if (!execution.Done && nodes[operation.Node].Exists) ApplyOperation(index);
    }

    /// <summary>在准确时点执行已经接受的操作，原资料顺序决定同刻的覆盖。</summary>
    /// <param name="node">本次已推进的部位。</param>
    private void ApplyPendingOperations(int node)
    {
        foreach (CompiledEventOperation operation in Plan.Nodes[node].EventOperations)
            if (operations[operation.Index].Received && !operations[operation.Index].Done && operations[operation.Index].Due <= Age)
                ApplyOperation(operation.Index);
    }

    /// <summary>先记为已执行，再改变同一主体，避免事件重入重复作用。</summary>
    /// <param name="index">已经到时的操作。</param>
    private void ApplyOperation(int index)
    {
        operations[index].Done = true;
        CompiledEventOperation operation = Plan.EventOperations[index]; int node = operation.Node;
        ref NodeState state = ref nodes[node];
        if (state.Recovering || state.Leaving) return;
        switch (operation.Kind)
        {
            case ConstructionOperationKind.Leave: StartLeave(node); break;
            case ConstructionOperationKind.Recover: StartRecovery(node, operation.Recovery); break;
            case ConstructionOperationKind.Flow: StartFlow(node); break;
            case ConstructionOperationKind.Fracture: RequestFracture(node); break;
            case ConstructionOperationKind.Gather: StartGather(node, operation.Gather, Age); break;
            case ConstructionOperationKind.Stop: StopNode(node, 1f); break;
            case ConstructionOperationKind.Grow: StartGrowth(node, operation); break;
            case ConstructionOperationKind.Animate: StartMotion(node, operation); break;
            case ConstructionOperationKind.ConvertMatter:
                ChangeMatter(node, operation.Matter);
                operations[index].WorldRequested = operation.WorldMatter != null;
                break;
            case ConstructionOperationKind.Detach: ReleaseNode(node); break;
            case ConstructionOperationKind.Fly:
                Vector3 previousVelocity = state.Free ? state.Velocity : state.AttachedVelocity;
                ReleaseNode(node); StartFlight(ref state, operation.Flight, Age); AdjustFragmentVelocity(node, previousVelocity); break;
            case ConstructionOperationKind.Spin:
                state.SpinOffset *= Quaternion.Euler(state.Spin * Mathf.Max(0f, Age - state.SpinAt));
                state.Spin = operation.Vector; state.SpinAt = Age; break;
        }
    }

    /// <summary>从实际姿态解除连接，继承这个点的运动，不读取显示对象。</summary>
    /// <param name="node">实际部位。</param>
    private void ReleaseNode(int node)
    {
        ref NodeState state = ref nodes[node];
        if (state.Free) return;
        state.Free = true; state.Position = state.Transform.GetColumn(3); state.Rotation = state.Transform.rotation;
        state.ReleasedAt = Age;
        state.Velocity = state.AttachedVelocity;
    }

    /// <summary>在同一主体上开始完整飞行，保持材料、作用量、身份和内部形状。</summary>
    /// <param name="state">本次实际状态。</param>
    /// <param name="flight">已经复制的完整参数。</param>
    /// <param name="at">真实开始的世界时点。</param>
    private void StartFlight(ref NodeState state, CompiledFlight flight, float at)
    {
        state.Flight = flight; state.FlightStartedAt = at; state.Flying = true; state.MovementStopped = false;
        state.Velocity = (flight.InheritVelocity ? state.Velocity : Vector3.zero) + (flight.WorldVelocity ? flight.Velocity : state.Rotation * flight.Velocity);
        state.Acceleration = flight.WorldVelocity ? flight.Acceleration : state.Rotation * flight.Acceleration;
        if (flight.Path != null) StartPath(ref state);
    }

    /// <summary>保持实际接触姿态；恢复整体运动不会让形变和尺寸突然跳到未来阶段。</summary>
    /// <param name="node">实际停止的主体。</param>
    /// <param name="fraction">本次真实作用位置在步内的进度。</param>
    private void StopNode(int node, float fraction)
    {
        ref NodeState state = ref nodes[node];
        Matrix4x4 old = state.Transform, previous = state.PreviousTransform;
        state.Position = Vector3.Lerp(previous.MultiplyPoint3x4(state.Pivot), old.MultiplyPoint3x4(state.Pivot), fraction);
        state.Rotation = Quaternion.Slerp(previous.rotation, old.rotation, fraction);
        Vector3 actualScale = Vector3.Lerp(previous.lossyScale, old.lossyScale, fraction);
        state.GrowthValue = Mathf.Lerp(state.PreviousGrowth, state.GrowthValue, fraction);
        state.Scale = actualScale / Mathf.Max(.00001f, state.GrowthValue);
        state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, actualScale);
        if (Plan.Nodes[node].FragmentSource >= 0) state.Transform *= Matrix4x4.Translate(-state.Pivot);
        state.ShapeAge = Mathf.Lerp(state.PreviousShapeAge, state.ShapeAge, fraction);
        if (poses[node] != null && fraction < 1f)
        {
            state.MotionAge = Mathf.Lerp(state.PreviousMotionAge, state.MotionAge, fraction);
            state.BlendAge = Mathf.Lerp(state.PreviousBlendAge, state.BlendAge, fraction);
            SampleMotion(node, state.MotionAge, state.BlendAge);
        }
        state.MotionStopped = true; state.GrowthStopped = true; state.MovementStopped = true; state.Free = true; state.ReleasedAt = Age;
        state.Velocity = Vector3.zero; state.Acceleration = Vector3.zero; state.Spin = Vector3.zero;
        if (fractures != null && Plan.Nodes[node].Fracture != null) fractures[node].Captured = false;
        Matrix4x4 correction = state.Transform * old.inverse;
        for (int i = node + 1; i < nodes.Length; i++) if (IsAttachedBelow(i, node))
        { nodes[i].Transform = correction * nodes[i].Transform; nodes[i].Revision++; }
    }

    /// <summary>接受、执行和延迟均随数值快照复制。</summary>
    private struct OperationState
    {
        /// <summary>一次性事件是否已经接受及执行。</summary>
        internal bool Received, Done;

        /// <summary>已执行但尚未确认的世界材料转换。</summary>
        internal bool WorldRequested;

        /// <summary>真实世界秒数上的执行时点。</summary>
        internal float Due;
    }
}