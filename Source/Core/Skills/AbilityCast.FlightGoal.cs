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
        Vector3 after = (before.sqrMagnitude > 1e-8f ? before.normalized : state.Rotation * Vector3.forward) * speed;
        if (state.Flight.Path == null) state.Velocity = after;
        else { state.PathVelocity = after; state.Velocity += after - before; }
        state.Revision++;
        return true;
    }

    /// <summary>从已经采集的数值解析本部位当前目标。</summary>
    /// <param name="state">独占飞行状态。</param><param name="target">本步普通目标。</param>
    private Vector3 FlightAim(in NodeState state, Vector3 target)
    {
        CompiledFlightGoal goal = state.Flight?.Goal;
        if (goal == null) return state.HasTarget ? state.Target : target;
        if (goal.Anchor == ConstructionFlightAnchor.Direction) return state.Position + state.FlightTarget;
        if (!goal.Follow) return state.FlightTarget;
        return goal.Anchor == ConstructionFlightAnchor.Source ? inputs.Source : state.HasTarget ? state.Target : target;
    }

    /// <summary>按实际速度和加速度上界接近到达边界，不跨过目标后拉回或瞬移补路程。</summary>
    /// <param name="state">已经开始的飞行。</param><param name="target">本步实际目标。</param>
    /// <param name="maximum">其他共同变化允许的最大步长。</param>
    private float FlightGoalStep(in NodeState state, Vector3 target, float maximum)
    {
        CompiledFlightGoal goal = state.Flight?.Goal;
        if (!state.Free || !state.Flying || state.MovementStopped || state.FlightArrived || goal == null ||
            goal.Arrival == ConstructionFlightArrival.None) return maximum;
        float minimum = Mathf.Max(.000001f, Age * .0000001f);
        float distance = Vector3.Distance(state.Position, FlightAim(in state, target)) - goal.Radius;
        if (distance <= FlightGoalPrecision(in state)) return Mathf.Min(maximum, minimum);
        float acceleration = state.Acceleration.magnitude + state.Flight.TurnRate * Mathf.Deg2Rad *
            (state.Flight.Path == null ? state.Velocity.magnitude : state.PathVelocity.magnitude);
        if (state.Flight.Path != null)
        {
            Vector3 bound = state.Flight.Path.AccelerationBound;
            acceleration += state.PathX.magnitude * bound.x + state.PathY.magnitude * bound.y + state.PathZ.magnitude * bound.z;
        }
        float speed = state.Velocity.magnitude + acceleration * maximum;
        return speed > 0f ? Mathf.Min(maximum, Mathf.Max(minimum, distance / speed)) : maximum;
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
            Vector3.Distance(state.Position, FlightAim(in state, target)) > goal.Radius + FlightGoalPrecision(in state)) return;
        state.FlightArrived = true;
        if (goal.Arrival == ConstructionFlightArrival.Continue) return;
        state.MovementStopped = true;
        state.Velocity = state.Acceleration = state.PathVelocity = Vector3.zero;
        if (goal.Arrival != ConstructionFlightArrival.End) return;
        state.FlightEnded = true;
        EndNode(node);
    }
}
