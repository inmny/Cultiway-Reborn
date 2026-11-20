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
    private void ReceiveOperation(int index, AbilityMatter eventMatter, float before, float after, Vector3 point)
    {
        CompiledEventOperation operation = Plan.EventOperations[index];
        ref OperationState execution = ref operations[index]; NodeState state = nodes[operation.Node];
        if (OperationEventReadyAt(index) > Age || !operation.When.Crossed(before, after) ||
            operation.When.Matter != null && !ReferenceEquals(operation.When.Matter, eventMatter)) return;
        ReceiveOperationAt(index, Age + operation.When.Delay);
        execution.EventPoint = point;
        if (operation.Step >= 0 && !StepInputsReady(operation.Step)) return;
        if (execution.Due > Age || !state.Started) return;
        if (sampling && nodes[operation.Node].SampleEpoch == -sampleEpoch) return;
        if (sampling && nodes[operation.Node].SampleEpoch != sampleEpoch)
            SampleNode(operation.Node, samplingStep, samplingTarget, false);
        if (!execution.Done && nodes[operation.Node].Exists) ApplyOperation(index);
    }

    /// <summary>接触查询与实际接收共用准备时点，预测跨越边界时仍能回到真正可用的位置。</summary>
    /// <param name="index">已经解析的条件操作。</param>
    private float OperationEventReadyAt(int index)
    {
        CompiledEventOperation operation = Plan.EventOperations[index];
        NodeState state = nodes[operation.Node];
        if (operations[index].Received || !state.Exists) return float.PositiveInfinity;
        return state.Started ? state.StartedAt + operation.At :
            !float.IsPositiveInfinity(state.BirthAt) && operation.At <= 0f ? 0f : float.PositiveInfinity;
    }

    /// <summary>在准确时点执行已经接受的操作，原资料顺序决定同刻的覆盖。</summary>
    /// <param name="node">本次已推进的部位。</param>
    private void ApplyPendingOperations(int node)
    {
        CompiledRange timers = Plan.Program.NodeTimers[node];
        for (int t = timers.Start; t < timers.End; t++)
        {
            int index = Plan.Program.Timers[t]; var action = Plan.EventOperations[index];
            if (action.Step < 0 && action.When == null && !operations[index].Received && NodeAge(node) >= action.At)
                ReceiveOperationAt(index, Age);
        }
        if (nodes[node].PendingOperations == 0) return;
        CompiledRange range = Plan.Program.NodeEvents[node];
        for (int index = range.Start; index < range.End; index++)
        {
            if ((nodes[node].PendingOperations & (1 << (index - range.Start))) == 0 || operations[index].Due > Age) continue;
            var command = Plan.Program.Commands[Plan.Program.EventCommands[index]];
            if (command.Step < 0 || StepInputsReady(command.Step)) ApplyOperation(index);
        }
    }

    /// <summary>接受事件和定时输入共用同一待执行位，随节点快照一起回退。</summary>
    /// <param name="index">独占状态索引。</param><param name="due">实际可执行时刻。</param>
    private void ReceiveOperationAt(int index, float due)
    {
        operations[index].Received = true; operations[index].Due = due;
        int node = Plan.EventOperations[index].Node;
        nodes[node].PendingOperations |= (ushort)(1 << (index - Plan.Program.NodeEvents[node].Start));
    }

    /// <summary>先记为已执行，再改变同一主体，避免事件重入重复作用。</summary>
    /// <param name="index">已经到时的操作。</param>
    private void ApplyOperation(int index)
    {
        CompiledEventOperation operation = Plan.EventOperations[index]; var program = Plan.Program;
        CompiledCommand command = program.Commands[program.EventCommands[index]]; int node = command.Node;
        // 交锋先扣减双方力量；监听派发完毕后再交接，避免把未扣减的力量交给下一部位。
        if (operation.Kind == ConstructionOperationKind.PassRemainder && (dispatchingEvents || nodes[node].HasClashPoint)) return;
        operations[index].Done = true;
        nodes[node].PendingOperations &= (ushort)(ushort.MaxValue ^ (1 << (index - program.NodeEvents[node].Start)));
        ref NodeState state = ref nodes[node];
        if (state.Recovering || state.Leaving) return;
        if (!program.Groups[command.Group].Apply(this, operation, command.Parameter))
        {
            if (operation.Step >= 0) stepStates[operation.Step].State = ConstructionStepState.Interrupted;
            return;
        }
        StepApplied(operation);
    }

    /// <summary>定时和条件投送共同取得真实旧速度，再改变当前部位及其实际水格动量。</summary>
    /// <param name="node">实际部位。</param><param name="flight">已经检查的投送参数。</param>
    /// <param name="at">真实启动时点。</param>
    private void StartFlight(int node, CompiledFlight flight, float at)
    {
        ref NodeState state = ref nodes[node];
        Vector3 previousVelocity = state.Free ? state.Velocity : state.AttachedVelocity;
        ReleaseNode(node);
        StartFlight(ref state, flight, at);
        AdjustFragmentVelocity(node, previousVelocity);
        if (flows?[node]?.Started == true)
        {
            if (!state.ShapeHeld) flows[node].SetVelocity(state.Velocity);
            state.Velocity = flows[node].Velocity;
        }

        state.Revision++;
        if (ReferenceEquals(flight, Plan.Nodes[node].Flight) && state.SampleEpoch == sampleEpoch) NativeStarted(node, ConstructionOperationKind.Fly);
    }

    /// <summary>从实际姿态解除连接，继承这个点的运动，不读取显示对象。</summary>
    /// <param name="node">实际部位。</param>
    private void ReleaseNode(int node)
    {
        ref NodeState state = ref nodes[node];
        if (state.Free) return;
        // 父体在零时刻留置或收束时，子部位可能尚未采样；先取得真实挂点，不能从默认零矩阵脱离。
        if (state.SampleEpoch == 0)
        {
            SampleNode(node, 0, inputs.Target, true);
            if (!state.Started || !state.Exists || state.Free) return;
        }

        state.Free = true;
        state.Position = state.Transform.GetColumn(3);
        state.Rotation = state.Transform.rotation;
        state.ReleasedAt = Age;
        state.Velocity = state.AttachedVelocity;
    }

    /// <summary>在同一主体上开始完整飞行，保持材料、作用量、身份和内部形状。</summary>
    /// <param name="state">本次实际状态。</param>
    /// <param name="flight">已经复制的完整参数。</param>
    /// <param name="at">真实开始的世界时点。</param>
    private void StartFlight(ref NodeState state, CompiledFlight flight, float at)
    {
        state.GroundSupported = false;
        state.Flight = flight; state.FlightStartedAt = at; state.Flying = true; state.MovementStopped = false;
        state.FlightArrived = state.FlightEnded = false;
        state.SteeringProgress = 0d; state.TransferCount = 0;
        state.FlightOrigin = state.Position;
        state.FlightDistance = 0f;
        state.FlightTarget = flight.Goal?.Anchor == ConstructionFlightAnchor.Direction ? inputs.Forward.normalized :
            flight.Goal?.Anchor == ConstructionFlightAnchor.Source ? inputs.Source :
            state.HasTarget ? state.Target : samplingTarget;
        state.Velocity = (flight.InheritVelocity ? state.Velocity : Vector3.zero) + (flight.WorldVelocity ? flight.Velocity : state.Rotation * flight.Velocity);
        state.FlightDirection = state.Velocity.sqrMagnitude > 1e-8f ? state.Velocity.normalized : state.Rotation * Vector3.forward;
        state.ApproachSpeed = state.Velocity.magnitude;
        state.Acceleration = flight.WorldAcceleration ? flight.Acceleration : state.Rotation * flight.Acceleration;
        if (flight.Path != null) StartPath(ref state);
        if (flight.Transfer != null) state.Velocity = Vector3.zero;
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
        state.Scale = actualScale / Mathf.Max(.00001f, state.GrowthValue * Mathf.Pow(state.MaterialRetained, 1f / 3f));
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
        if (flows?[node]?.Started == true) flows[node].StopMotion();
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

        /// <summary>真实条件已触发但尚未确认的导电请求。</summary>
        internal ConductionRun Conduction;

        /// <summary>真实世界秒数上的执行时点。</summary>
        internal float Due;

        /// <summary>实际条件位置，按时间启动的操作仍采用自身位置。</summary>
        internal Vector3 EventPoint;
    }
}
