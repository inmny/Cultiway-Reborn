using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际曲线与中心飞行共同推进，显示、接触、停止和反射都读取同一姿态。</summary>
public sealed partial class AbilityCast
{
    /// <summary>开始飞行时固定曲线参照，并保留独立中心速度。</summary>
    /// <param name="state">本部位的独占状态。</param>
    private void StartPath(ref NodeState state)
    {
        state.PathVelocity = state.Velocity;
        state.PathProgress = 0d;
        if (state.Flight.Path.Space == ConstructionPathSpace.TargetLine)
        {
            state.PathX = (state.Flight.Goal == null ? inputs.Target : FlightAim(in state, samplingTarget)) - state.Position;
            if (state.PathX.magnitude > CompiledConstructionPath.MaximumTargetSpan)
                throw new ArgumentException("目标相对路径超过一万格的已声明坐标预算");
            Vector3 horizontal = new(state.PathX.x, state.PathX.y, 0f);
            state.PathY = horizontal.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.forward, horizontal).normalized : Vector3.up;
            state.PathZ = Vector3.forward;
        }
        else
        {
            state.PathX = state.Rotation * Vector3.right;
            state.PathY = state.Rotation * Vector3.up;
            state.PathZ = state.Rotation * Vector3.forward;
        }
        state.Flight.Path.Sample(0f, out Vector3 offset, out Vector3 velocity);
        state.PathCentre = state.Position - PathVector(in state, offset);
        state.Velocity += PathVector(in state, velocity) * PathRate(in state);
    }

    /// <summary>先推进可追踪中心线，再加曲线的解析位移差与速度，最后按明确角速度调整朝向。</summary>
    /// <param name="form">已编译的飞行及路径。</param>
    /// <param name="state">本部位实际状态。</param>
    /// <param name="seconds">本步实际飞行时间。</param>
    /// <param name="target">本步实际追踪位置。</param>
    private void MovePath(CompiledForm form, ref NodeState state, float seconds, Vector3 target)
    {
        Vector3 centre = state.PathCentre;
        if (!state.FlightArrived && state.Flight.TurnRate > 0f && state.PathVelocity.sqrMagnitude > 1e-8f && (target - centre).sqrMagnitude > 1e-8f)
            state.PathVelocity = Vector3.RotateTowards(state.PathVelocity, (target - centre).normalized * state.PathVelocity.magnitude,
                state.Flight.TurnRate * Mathf.Deg2Rad * seconds, 0f);
        AdvanceSteering(ref state, state.PathVelocity, seconds);
        if (state.Flight.Path.DistancePerSecond > 0f)
            state.PathProgress += CentreDistance(state.PathVelocity, state.Acceleration, seconds) / state.Flight.Path.DistancePerSecond;
        state.PathCentre += state.PathVelocity * seconds + state.Acceleration * (.5f * seconds * seconds);
        state.PathVelocity += state.Acceleration * seconds;
        state.Flight.Path.Sample(PathTime(in state), out Vector3 after, out Vector3 derivative);
        state.Position = state.PathCentre + PathVector(in state, after);
        state.Velocity = state.PathVelocity + PathVector(in state, derivative) * PathRate(in state);
        if (state.Flight.Path.Forward.sqrMagnitude > 0f && state.Velocity.sqrMagnitude > 1e-8f)
        {
            Quaternion facing = Quaternion.FromToRotation(state.Rotation * state.Flight.Path.Forward, state.Velocity) * state.Rotation;
            state.Rotation = Quaternion.RotateTowards(state.Rotation, facing, state.Flight.Path.TurnRate * seconds);
        }
    }

    /// <summary>把曲线数值映射到真实释放参照；反射可以保留镜像后的左手参照。</summary>
    /// <param name="state">已经确定的实际参照。</param>
    /// <param name="value">曲线坐标。</param>
    private static Vector3 PathVector(in NodeState state, Vector3 value)
        => state.PathX * value.x + state.PathY * value.y + state.PathZ * value.z;

    /// <summary>同一已保存进度供推进、转折和反射使用，时间路径保留原来的时间计算。</summary>
    /// <param name="state">当前真实运动。</param>
    private float PathTime(in NodeState state) => state.Flight.Path.DistancePerSecond > 0f
        ? (float)state.PathProgress : Mathf.Max(0f, Age - state.FlightStartedAt);

    /// <summary>解析导数换算为世界秒，中心停止时路程路径也停止。</summary>
    /// <param name="state">当前真实中心速度。</param>
    private static float PathRate(in NodeState state) => state.Flight.Path.DistancePerSecond > 0f
        ? state.PathVelocity.magnitude / state.Flight.Path.DistancePerSecond : 1f;

    /// <summary>先换算曲线二阶变化，再加中心加速度改变曲线进度的变化，供真实接触取样。</summary>
    /// <param name="state">实际曲线及速度。</param><param name="seconds">本步最长时间。</param>
    private static float PathAcceleration(in NodeState state, float seconds)
    {
        var path = state.Flight.Path;
        Vector3 bound = path.AccelerationBound;
        float shape = state.PathX.magnitude * bound.x + state.PathY.magnitude * bound.y + state.PathZ.magnitude * bound.z;
        float acceleration = state.Acceleration.magnitude;
        if (path.DistancePerSecond == 0f) return shape + acceleration;
        float rate = (state.PathVelocity.magnitude + acceleration * seconds) / path.DistancePerSecond;
        if (acceleration == 0f) return shape * rate * rate;
        float span = path.Space == ConstructionPathSpace.TargetLine ? state.PathX.magnitude : 1f;
        return shape * rate * rate + path.SpeedBudget(span) * acceleration / path.DistancePerSecond + acceleration;
    }

    /// <summary>积分本步匀加速中心速度的长度，反向穿过零速也累计真实路程，不用首尾距离抵消。</summary>
    /// <param name="velocity">步初中心速度。</param><param name="acceleration">步内固定加速度。</param>
    /// <param name="seconds">本步时间。</param>
    private static double CentreDistance(Vector3 velocity, Vector3 acceleration, float seconds)
    {
        double a = Math.Sqrt((double)acceleration.x * acceleration.x + (double)acceleration.y * acceleration.y +
                             (double)acceleration.z * acceleration.z);
        if (a < 1e-8d) return (double)velocity.magnitude * seconds;
        double parallel = ((double)velocity.x * acceleration.x + (double)velocity.y * acceleration.y +
                           (double)velocity.z * acceleration.z) / a;
        double square = Math.Max(0d, (double)velocity.x * velocity.x + (double)velocity.y * velocity.y +
                                     (double)velocity.z * velocity.z - parallel * parallel);
        return Math.Max(0d, (Primitive(parallel + a * seconds) - Primitive(parallel)) / a);

        double Primitive(double value)
        {
            if (square < 1e-12d) return .5d * value * Math.Abs(value);
            double root = Math.Sqrt(value * value + square);
            double logarithm = Math.Sign(value) * Math.Log((Math.Abs(value) + root) / Math.Sqrt(square));
            return .5d * (value * root + square * logarithm);
        }
    }

    /// <summary>拐点、循环边界、朝向及曲率一起限制取样，不把弧线的长弦当作真实经过区域。</summary>
    /// <param name="form">实际路径。</param>
    /// <param name="state">当前部位。</param>
    /// <param name="maximum">其他共同变化已经允许的秒数。</param>
    private float PathStep(CompiledForm form, in NodeState state, float maximum)
    {
        if (!state.Flying || state.Flight.Path == null || state.MovementStopped) return maximum;
        var path = state.Flight.Path;
        float rate = path.DistancePerSecond == 0f ? 1f :
            (state.PathVelocity.magnitude + state.Acceleration.magnitude * maximum) / path.DistancePerSecond;
        float step;
        if (path.DistancePerSecond == 0f)
            step = Mathf.Min(maximum, path.UntilBoundary(Age, state.FlightStartedAt));
        else
        {
            float boundary = path.UntilBoundary(PathTime(in state), 0f);
            // 路程边界换回世界秒后，至少推进一个可表示的时刻，避免延迟出手后停在同一浮点数上。
            float minimum = Mathf.Max(.0000001f, Age * .0000001f);
            step = rate > 0f ? Mathf.Min(maximum, Mathf.Max(minimum, boundary / rate)) : maximum;
        }
        float angular = state.Spin.magnitude + (state.Flight.Path.Forward.sqrMagnitude > 0f ? state.Flight.Path.TurnRate : 0f);
        if (angular > 0f) step = Mathf.Min(step, 6f / angular);
        float acceleration = PathAcceleration(in state, maximum);
        if (acceleration > 0f) step = Mathf.Min(step, Mathf.Sqrt(.02f / acceleration));
        return step;
    }
}
