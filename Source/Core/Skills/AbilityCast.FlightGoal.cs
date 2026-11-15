using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>目标、到达和返回都作用于同一真实飞行，保存前态不另建运行对象。</summary>
public sealed partial class AbilityCast
{
    /// <summary>独立运动中调整中心速度，不重新开始飞行或重置已取得的方向和命中记录。</summary>
    /// <param name="node">实际部位。</param><param name="speed">新的中心速度。</param>
    private bool ChangeFlightSpeed(int node, float speed)
    {
        ref NodeState state = ref nodes[node];
        if (!state.Exists || !state.Free || !state.Flying || state.MovementStopped) return false;
        Vector3 before = state.Flight.Path == null ? state.Velocity : state.PathVelocity;
        if (before.sqrMagnitude > 1e-8f) state.FlightDirection = before.normalized;
        state.ApproachSpeed = speed;
        Vector3 after = state.FlightDirection * speed;
        if (state.Flight.Path == null) state.Velocity = state.Flight.Transfer == null ? after : Vector3.zero;
        else
        {
            state.PathVelocity = after;
            state.Flight.Path.Sample(PathTime(in state), out _, out Vector3 derivative);
            state.Velocity = after + PathVector(in state, derivative) * PathRate(in state);
        }
        state.Revision++;
        return true;
    }

    /// <summary>从已经采集的数值解析本部位当前目标。</summary>
    /// <param name="state">独占飞行状态。</param><param name="target">本步普通目标。</param>
    private Vector3 FlightAim(in NodeState state, Vector3 target)
    {
        CompiledFlightGoal goal = state.Flight?.Goal;
        Vector3 aim = goal == null ? state.HasTarget ? state.Target : target :
            goal.Anchor == ConstructionFlightAnchor.Direction ?
                (state.Flight.Path == null ? state.Position : state.PathCentre) + state.FlightTarget :
            !goal.Follow ? state.FlightTarget :
            goal.Anchor == ConstructionFlightAnchor.Source ? inputs.Source : state.HasTarget ? state.Target : target;
        // 沿地追踪只改变水平位置，离地高度仍由真实形状和地表共同决定。
        if (state.Flight?.Ground == ConstructionGroundResponse.Follow) aim.z = state.Position.z;
        return aim;
    }

    /// <summary>按实际速度和加速度上界接近到达边界，不跨过目标后拉回或瞬移补路程。</summary>
    /// <param name="state">已经开始的飞行。</param><param name="target">本步实际目标。</param>
    /// <param name="maximum">其他共同变化允许的最大步长。</param>
    private float FlightGoalStep(in NodeState state, Vector3 target, float maximum)
    {
        CompiledFlightGoal goal = state.Flight?.Goal;
        if (!state.Free || !state.Flying || state.MovementStopped || state.FlightArrived || goal == null ||
            goal.Arrival == ConstructionFlightArrival.None || state.Flight.Transfer != null) return maximum;
        float minimum = Mathf.Max(.000001f, Age * .0000001f);
        if (goal.DirectArrival)
        {
            if (goal.Arrival != ConstructionFlightArrival.Continue || state.Velocity.sqrMagnitude < 1e-8f) return maximum;
            // 抵达时点单独收步，余下时间沿抵达方向继续，不吞掉路程或绕目标盘旋。
            return Mathf.Min(maximum, Mathf.Max(minimum,
                Vector3.Distance(state.Position, FlightAim(in state, target)) / state.Velocity.magnitude));
        }
        float distance = Vector3.Distance(state.Position, FlightAim(in state, target)) - goal.Radius;
        if (distance <= FlightGoalPrecision(in state)) return Mathf.Min(maximum, minimum);
        float acceleration = (state.Flight.Path == null ? state.Acceleration.magnitude : PathAcceleration(in state, maximum)) + state.Flight.TurnRate * Mathf.Deg2Rad *
            (state.Flight.Path == null ? state.Velocity.magnitude : state.PathVelocity.magnitude);
        float speed = state.Velocity.magnitude;
        if (goal.Approach)
        {
            speed = state.ApproachSpeed;
            var path = state.Flight.Path;
            if (path != null)
                speed += path.SpeedBudget(path.Space == ConstructionPathSpace.TargetLine ? state.PathX.magnitude : 1f) *
                    (path.DistancePerSecond > 0f ? state.ApproachSpeed / path.DistancePerSecond : 1f);
        }
        speed += acceleration * maximum;
        return speed > 0f ? Mathf.Min(maximum, Mathf.Max(minimum, distance / speed)) : maximum;
    }

    /// <summary>只在最后一步可达时沿真实线段到达，姿态和速度交给原接触流程继续采样。</summary>
    /// <param name="state">当前真实状态。</param><param name="target">真实到达点，不采用方向或旁侧偏移。</param>
    /// <param name="seconds">本步实际时长。</param>
    private bool FinishDirectFlight(ref NodeState state, Vector3 target, float seconds)
    {
        if (!state.Flying || state.FlightArrived || state.Flight.Goal?.DirectArrival != true || seconds <= 0f) return false;
        Vector3 delta = target - state.Position;
        bool continues = state.Flight.Goal.Arrival == ConstructionFlightArrival.Continue;
        float precision = continues ? FlightGoalPrecision(in state) : 0f;
        if (delta.magnitude > state.Velocity.magnitude * seconds + precision) return false;
        Vector3 before = state.Velocity;
        state.Velocity = continues
            ? (delta.sqrMagnitude > 1e-8f ? delta.normalized * before.magnitude : before)
            : delta / seconds;
        if (continues)
        {
            AdvanceSteering(ref state, state.Velocity, seconds);
            state.FlightArrived = true;
        }
        if (delta.sqrMagnitude > 1e-8f)
            state.Rotation = Quaternion.FromToRotation(before.sqrMagnitude > 1e-8f ? before : state.Rotation * Vector3.forward, delta) * state.Rotation;
        state.Position = target;
        return true;
    }

    /// <summary>以浮点时间能够表达的最小位移容差确认到达，不改变实际姿态。</summary>
    /// <param name="state">本次实际速度。</param>
    private float FlightGoalPrecision(in NodeState state)
        => .00001f + state.Velocity.magnitude * Mathf.Max(.0000001f, Age * .0000001f);

    /// <summary>在地表及实际姿态完成后处理到达，内部动作和其他分支沿原生命周期结束。</summary>
    /// <param name="node">实际飞行部位。</param><param name="target">本步实际目标。</param>
    private void CompleteFlightGoal(int node, Vector3 target)
    {
        ref NodeState state = ref nodes[node];
        CompiledFlightGoal goal = state.Flight?.Goal;
        if (!state.Exists || !state.Flying || state.FlightArrived || state.MovementStopped || goal == null ||
            goal.Arrival == ConstructionFlightArrival.None ||
            Vector3.Distance(state.Position, FlightAim(in state, target)) > (goal.DirectArrival ? 0f : goal.Radius) + FlightGoalPrecision(in state)) return;
        state.FlightArrived = true;
        if (goal.Arrival == ConstructionFlightArrival.Continue) return;
        state.MovementStopped = true;
        state.Velocity = state.Acceleration = state.PathVelocity = Vector3.zero;
        if (goal.Arrival != ConstructionFlightArrival.End) return;
        state.FlightEnded = true;
        EndNode(node);
    }
}
