using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>目标偏转只改变同一真实运动的追踪方向，实际到达仍读取原目标。</summary>
public sealed partial class AbilityCast
{
    /// <summary>先取真实目标方向，再叠加它的横向、离地偏转，供同一次中心运动使用。</summary>
    /// <param name="state">本部位真实状态。</param><param name="target">本步实际目标。</param>
    /// <param name="seconds">本步时长，时间曲线从步初取样。</param>
    private Vector3 SteeringAim(in NodeState state, Vector3 target, float seconds)
    {
        Vector3 aim = FlightAim(in state, target);
        CompiledFlightGoal goal = state.Flight?.Goal;
        if (goal != null)
        {
            aim += goal.Offset;
            if (goal.OffsetPath != null)
            {
                goal.OffsetPath.Sample(Mathf.Max(0f, Age - state.FlightStartedAt), out Vector3 offset, out _);
                aim += offset;
            }
        }
        CompiledFlightSteering steering = state.Flight?.Goal?.Steering;
        Vector3 centre = state.Flight?.Path == null ? state.Position : state.PathCentre;
        // 旁侧偏移和方向曲线也遵守沿地规则，不将承托后的高度差变成俯冲。
        if (state.Flight?.Ground == ConstructionGroundResponse.Follow) aim.z = centre.z;
        if (steering == null) return aim;
        Vector3 delta = aim - centre;
        if (delta.sqrMagnitude < 1e-8f) return aim;
        Vector3 forward = delta.normalized;
        Vector3 side = Vector3.Cross(Vector3.forward, forward);
        side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
        Vector3 lift = Vector3.Cross(forward, side).normalized;
        Vector2 bias = steering.Sample(SteeringTime(in state, seconds));
        Vector3 direction = forward + side * bias.x + lift * bias.y;
        if (state.Flight.Ground == ConstructionGroundResponse.Follow) direction.z = 0f;
        return centre + direction.normalized * delta.magnitude;
    }

    /// <summary>方向曲线采用自己的时间或路程进度，不借用位置曲线的周期。</summary>
    /// <param name="state">本次独占飞行状态。</param><param name="seconds">已进入步末时回到步初的时长。</param>
    private float SteeringTime(in NodeState state, float seconds = 0f) => state.Flight.Goal.Steering.Path?.DistancePerSecond > 0f
        ? (float)state.SteeringProgress : Mathf.Max(0f, Age - seconds - state.FlightStartedAt);

    /// <summary>中心完成转向后累计实际路程，方向与位置曲线使用同一真实中心运动。</summary>
    /// <param name="state">本次独占状态。</param><param name="velocity">本步真实中心初速度。</param>
    /// <param name="seconds">本步时长。</param>
    private static void AdvanceSteering(ref NodeState state, Vector3 velocity, float seconds)
    {
        float distance = state.Flight?.Goal?.Steering?.Path?.DistancePerSecond ?? 0f;
        if (distance > 0f) state.SteeringProgress += CentreDistance(velocity, state.Acceleration, seconds) / distance;
    }

    /// <summary>方向曲线关键点与循环边界参与共用步长，不跨过明确的偏转变化。</summary>
    /// <param name="state">当前真实状态。</param><param name="maximum">其他规则允许的时长。</param>
    private float SteeringStep(in NodeState state, float maximum)
    {
        var path = state.Flight?.Goal?.Steering?.Path;
        if (!state.Flying || state.MovementStopped || state.FlightArrived || path == null) return maximum;
        if (path.DistancePerSecond == 0f)
            return Mathf.Min(maximum, path.UntilBoundary(Age, state.FlightStartedAt));
        Vector3 velocity = state.Flight.Path == null ? state.Velocity : state.PathVelocity;
        float rate = (velocity.magnitude + state.Acceleration.magnitude * maximum) / path.DistancePerSecond;
        if (rate == 0f) return maximum;
        float minimum = Mathf.Max(.0000001f, Age * .0000001f);
        return Mathf.Min(maximum, Mathf.Max(minimum, path.UntilBoundary(SteeringTime(in state), 0f) / rate));
    }
}
