using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>飞行距离条件读取实际位置，不把累计路程或固定时间当作外投距离。</summary>
public sealed partial class AbilityCast
{
    /// <summary>用实际速度和加速度上界接近下一距离边界，曲线路程与转向仍由共同步长约束。</summary>
    /// <param name="node">实际飞行来源。</param><param name="maximum">其他边界允许的最长时长。</param>
    private float FlightDistanceStep(int node, float maximum)
    {
        NodeState state = nodes[node];
        float[] thresholds = Plan.Nodes[node].FlightDistanceThresholds;
        if (!state.Flying || state.MovementStopped || thresholds.Length == 0) return maximum;
        int index = NextSizeIndex(thresholds, state.FlightDistance, true);
        if (index >= thresholds.Length) return maximum;
        float remaining = thresholds[index] - Vector3.Distance(state.FlightOrigin, state.Position);
        float speed = state.Velocity.magnitude;
        float acceleration = (state.Flight.Path == null ? state.Acceleration.magnitude : PathAcceleration(in state, maximum)) +
            state.Flight.TurnRate * Mathf.Deg2Rad * (state.Flight.Path == null ? state.Velocity.magnitude : state.PathVelocity.magnitude);
        if (state.Flight.Goal?.Approach == true)
        {
            speed = state.ApproachSpeed;
            var path = state.Flight.Path;
            if (path != null) speed += path.SpeedBudget(path.Space == ConstructionPathSpace.TargetLine ? state.PathX.magnitude : 1f) *
                (path.DistancePerSecond > 0f ? state.ApproachSpeed / path.DistancePerSecond : 1f);
        }
        speed += acceleration * maximum;
        float minimum = Mathf.Max(.000001f, Age * .0000001f);
        return speed > 0f ? Mathf.Min(maximum, Mathf.Max(minimum, remaining / speed)) : maximum;
    }

    /// <summary>在真实姿态完成后派发向外越界事件，只消除边界浮点余差，不移动主体补距离。</summary>
    /// <param name="node">已经完成本步运动的来源。</param>
    private void SignalFlightDistance(int node)
    {
        ref NodeState state = ref nodes[node];
        float[] thresholds = Plan.Nodes[node].FlightDistanceThresholds;
        if (!state.Flying || thresholds.Length == 0) return;
        float before = state.FlightDistance;
        float after = Vector3.Distance(state.FlightOrigin, state.Position);
        int index = NextSizeIndex(thresholds, before, true);
        if (index < thresholds.Length && Mathf.Abs(after - thresholds[index]) <= FlightGoalPrecision(in state))
            after = thresholds[index];
        state.FlightDistance = after;
        if (index < thresholds.Length && before < after && after >= thresholds[index])
            SignalEvent(node, ConstructionChangeTrigger.DistanceFromFlightStart, state.Position, before, after);
    }
}
