using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际曲线与中心飞行共同推进，显示、接触、停止和反射都读取同一姿态。</summary>
public sealed partial class AbilityCast
{
    /// <summary>开始飞行时固定曲线参照，并保留独立中心速度。</summary>
    /// <param name="form">已经编译的飞行资料。</param>
    /// <param name="state">本部位的独占状态。</param>
    private void StartPath(CompiledForm form, ref NodeState state)
    {
        state.PathVelocity = state.Velocity;
        if (form.FlightPath.Space == ConstructionPathSpace.TargetLine)
        {
            state.PathX = inputs.Target - state.Position;
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
        form.FlightPath.Sample(0f, out Vector3 offset, out Vector3 velocity);
        state.PathCentre = state.Position - PathVector(in state, offset);
        state.Velocity += PathVector(in state, velocity);
    }

    /// <summary>先推进可追踪中心线，再加曲线的解析位移差与速度，最后按明确角速度调整朝向。</summary>
    /// <param name="form">已编译的飞行及路径。</param>
    /// <param name="state">本部位实际状态。</param>
    /// <param name="seconds">本步实际飞行时间。</param>
    /// <param name="target">本步实际追踪位置。</param>
    private void MovePath(CompiledForm form, ref NodeState state, float seconds, Vector3 target)
    {
        float time = Mathf.Max(0f, Age - form.FlyAt);
        form.FlightPath.Sample(time, out Vector3 after, out Vector3 derivative);
        Vector3 centre = state.PathCentre;
        if (form.TurnRate > 0f && state.PathVelocity.sqrMagnitude > 1e-8f && (target - centre).sqrMagnitude > 1e-8f)
            state.PathVelocity = Vector3.RotateTowards(state.PathVelocity, (target - centre).normalized * state.PathVelocity.magnitude,
                form.TurnRate * Mathf.Deg2Rad * seconds, 0f);
        state.PathCentre += state.PathVelocity * seconds;
        state.Position = state.PathCentre + PathVector(in state, after);
        state.Velocity = state.PathVelocity + PathVector(in state, derivative);
        if (form.FlightPath.Forward.sqrMagnitude > 0f && state.Velocity.sqrMagnitude > 1e-8f)
        {
            Quaternion facing = Quaternion.FromToRotation(state.Rotation * form.FlightPath.Forward, state.Velocity) * state.Rotation;
            state.Rotation = Quaternion.RotateTowards(state.Rotation, facing, form.FlightPath.TurnRate * seconds);
        }
    }

    /// <summary>把曲线数值映射到真实释放参照；反射可以保留镜像后的左手参照。</summary>
    /// <param name="state">已经确定的实际参照。</param>
    /// <param name="value">曲线坐标。</param>
    private static Vector3 PathVector(in NodeState state, Vector3 value)
        => state.PathX * value.x + state.PathY * value.y + state.PathZ * value.z;

    /// <summary>拐点、循环边界、朝向及曲率一起限制取样，不把弧线的长弦当作真实经过区域。</summary>
    /// <param name="form">实际路径。</param>
    /// <param name="state">当前部位。</param>
    /// <param name="maximum">其他共同变化已经允许的秒数。</param>
    private float PathStep(CompiledForm form, in NodeState state, float maximum)
    {
        if (form.FlightPath == null || !state.Flying || state.Stopped) return maximum;
        float step = Mathf.Min(maximum, form.FlightPath.UntilBoundary(Age, form.FlyAt));
        float angular = form.Spin.magnitude + (form.FlightPath.Forward.sqrMagnitude > 0f ? form.FlightPath.TurnRate : 0f);
        if (angular > 0f) step = Mathf.Min(step, 6f / angular);
        Vector3 bound = form.FlightPath.AccelerationBound;
        float acceleration = state.PathX.magnitude * bound.x + state.PathY.magnitude * bound.y + state.PathZ.magnitude * bound.z;
        if (acceleration > 0f) step = Mathf.Min(step, Mathf.Sqrt(.02f / acceleration));
        return step;
    }
}
