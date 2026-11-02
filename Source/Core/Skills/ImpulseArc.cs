using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>按经过的世界时间直接求受力位置，取样次数不会改变轨迹或平地落点。</summary>
public readonly struct ImpulseArc
{
    /// <summary>起始脚下绝对坐标。</summary>
    public readonly Vector3 Origin;

    /// <summary>每世界秒的起始速度，不使用原版与离地力度相乘的存储单位。</summary>
    public readonly Vector3 Velocity;

    /// <summary>每世界秒的向下加速度。</summary>
    public readonly float Gravity;

    /// <summary>复制经过实际抗击退规则后的数字。</summary>
    /// <param name="origin">起始脚下。</param><param name="velocity">实际世界速度。</param>
    /// <param name="gravity">有限且非负的向下加速度。</param>
    public ImpulseArc(Vector3 origin, Vector3 velocity, float gravity)
    {
        if (!Finite(origin) || !Finite(velocity) || float.IsNaN(gravity) || float.IsInfinity(gravity) || gravity < 0f)
            throw new ArgumentException("受力轨迹需要有限位置、速度和非负重力");
        Origin = origin;
        Velocity = velocity;
        Gravity = gravity;
    }

    /// <summary>直接求指定时点的位置，不累加逐帧舍入误差。</summary>
    /// <param name="age">已经经过的非负世界秒数。</param>
    public Vector3 Position(double age) => new((float)(Origin.x + Velocity.x * age),
        (float)(Origin.y + Velocity.y * age),
        (float)(Origin.z + Velocity.z * age - .5d * Gravity * age * age));

    /// <summary>同一轨迹在该时点的真实速度。</summary>
    /// <param name="age">已经经过的世界秒数。</param>
    public Vector3 Speed(double age) => new(Velocity.x, Velocity.y, (float)(Velocity.z - Gravity * age));

    /// <summary>求下降段首次到达水平面的时点；没有下降交点时返回无穷大。</summary>
    /// <param name="height">地表绝对高度。</param>
    public double LandingAt(float height)
    {
        double distance = Origin.z - height;
        if (Gravity == 0f) return Velocity.z < 0f ? Math.Max(0d, -distance / Velocity.z) : double.PositiveInfinity;
        double discriminant = (double)Velocity.z * Velocity.z + 2d * Gravity * distance;
        return discriminant < 0d
            ? double.PositiveInfinity
            : Math.Max(0d, (Velocity.z + Math.Sqrt(discriminant)) / Gravity);
    }

    /// <summary>只在公开数字入口检查一次。</summary>
    /// <param name="value">待发布的向量。</param>
    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
}