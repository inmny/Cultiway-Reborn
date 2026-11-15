using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>追逐点只提供本步目的地，实际主体仍受速度限制并保留真实经过路径。</summary>
public sealed partial class AbilityCast
{
    /// <summary>直接接近当前点，到点后停留；巡航速度独立保留，后续移动目标仍能跟上。</summary>
    /// <param name="state">独占的飞行状态。</param><param name="origin">实际主体或外部曲线中心。</param>
    /// <param name="target">已经采样的真实追逐点。</param><param name="seconds">本步世界时长。</param>
    /// <param name="velocity">本步实际位移速度，供接触、路程和外部曲线使用。</param>
    private static Vector3 ApproachPoint(ref NodeState state, Vector3 origin, Vector3 target, float seconds, out Vector3 velocity)
    {
        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 1e-8f)
        {
            velocity = Vector3.zero;
            return origin;
        }
        state.FlightDirection = delta / distance;
        float speed = seconds > 0f ? Mathf.Min(state.ApproachSpeed, distance / seconds) : state.ApproachSpeed;
        velocity = state.FlightDirection * speed;
        return origin + velocity * seconds;
    }

    /// <summary>追逐点的关键边界和曲率参与共同取样，实际主体不跨过整段弯曲来补命中。</summary>
    /// <param name="state">当前运动状态。</param><param name="maximum">其他规则允许的最长时长。</param>
    private float ChasePointStep(in NodeState state, float maximum)
    {
        var path = state.Flight?.Goal?.OffsetPath;
        if (!state.Flying || state.MovementStopped || state.FlightArrived || path == null) return maximum;
        float step = Mathf.Min(maximum, path.UntilBoundary(Age, state.FlightStartedAt));
        float acceleration = path.AccelerationBound.x + path.AccelerationBound.y + path.AccelerationBound.z;
        if (acceleration > 0f) step = Mathf.Min(step, Mathf.Sqrt(.02f / acceleration));
        return step;
    }
}
