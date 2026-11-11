using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>离手身体的实际平移与转动；按世界时间直接取样，不累计逐帧误差。</summary>
internal readonly struct BodyRigidFlight
{
    /// <summary>原表面的面积中心，作为离手后的转动中心。</summary>
    internal readonly Vector3 Pivot;

    /// <summary>离手时相对原身体的实际转角。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>世界坐标中的角速度，单位为每世界秒的弧度。</summary>
    internal readonly Vector3 AngularVelocity;

    /// <summary>面积中心的同一重力轨迹。</summary>
    internal readonly ImpulseArc Arc;

    /// <summary>只接收已经确认的身体运动，不读取施放预测。</summary>
    /// <param name="pivot">原几何中心。</param><param name="motion">离手时的确认变换。</param>
    /// <param name="velocity">中心的实际线速度。</param><param name="angular">实际角速度。</param>
    /// <param name="gravity">当前人物的实际重力。</param>
    internal BodyRigidFlight(Vector3 pivot, BodySurfaceTransform motion, Vector3 velocity, Vector3 angular, float gravity)
    {
        Pivot = pivot;
        Rotation = motion.Rotation;
        AngularVelocity = angular;
        Arc = new ImpulseArc(motion.Matrix.MultiplyPoint3x4(pivot), velocity, gravity);
    }

    /// <summary>由两份已确认姿态取得中心速度与世界角速度。</summary>
    /// <param name="center">同一原几何中心。</param><param name="from">前次确认。</param>
    /// <param name="to">本次确认。</param><param name="seconds">两次实际世界时间之差。</param>
    internal static (Vector3 Linear, Vector3 Angular) Velocity(Vector3 center, BodySurfaceTransform from,
        BodySurfaceTransform to, double seconds)
    {
        Vector3 linear = (to.Matrix.MultiplyPoint3x4(center) - from.Matrix.MultiplyPoint3x4(center)) / (float)seconds;
        Quaternion delta = to.Rotation * Quaternion.Inverse(from.Rotation);
        delta.ToAngleAxis(out float degrees, out Vector3 axis);
        if (degrees > 180f) { degrees -= 360f; }
        Vector3 angular = Mathf.Abs(degrees) < .000001f ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad / (float)seconds);
        return (linear, angular);
    }

    /// <summary>身体绕自己的同一中心转动，根点与任意部位都由这份变换产生。</summary>
    /// <param name="age">离手后的世界秒数。</param>
    internal BodySurfaceTransform Sample(double age)
    {
        float speed = AngularVelocity.magnitude;
        Quaternion spin = speed == 0f ? Quaternion.identity :
            Quaternion.AngleAxis((float)(speed * age * Mathf.Rad2Deg), AngularVelocity / speed);
        return new BodySurfaceTransform(Pivot, Arc.Position(age), spin * Rotation);
    }

    /// <summary>整个时段的保守范围，包括重力弧线中途的最高点和完整转动。</summary>
    /// <param name="body">同一真实几何。</param><param name="from">时段起点。</param><param name="to">终点。</param>
    internal Bounds Bounds(BodySurfaceMesh body, double from, double to)
    {
        var bounds = new Bounds(Arc.Position(from), Vector3.zero);
        bounds.Encapsulate(Arc.Position(to));
        if (Arc.Gravity > 0f)
        {
            double top = Arc.Velocity.z / Arc.Gravity;
            if (top > from && top < to) bounds.Encapsulate(Arc.Position(top));
        }
        float radius = 0f;
        foreach (Vector3 point in body.Points) radius = Mathf.Max(radius, Vector3.Distance(point, Pivot));
        bounds.Expand(radius * 2f + .00004f);
        return bounds;
    }

    /// <summary>重力曲线按原三角面精度分段，仍使用已有真实表面查询；不会只检查飞行终点。</summary>
    /// <param name="body">离手时的真实几何。</param><param name="from">本段起点。</param><param name="to">终点。</param>
    /// <param name="terrain">共同地表。</param><param name="environment">冻结的实际环境。</param>
    /// <param name="hit">整个时段的首次接触。</param>
    internal bool Find(BodySurfaceMesh body, double from, double to, AbilityHeightField terrain,
        IReadOnlyList<BodySurfaceObstacle> environment, out BodySurfaceHit hit)
    {
        hit = default;
        double stride = Arc.Gravity > 0f ? Math.Sqrt(.00008d / Arc.Gravity) : double.PositiveInfinity;
        float angular = AngularVelocity.magnitude;
        if (angular > 0f) stride = Math.Min(stride, Math.PI / (3d * angular));
        double begin = from;
        while (begin < to)
        {
            double end = Math.Min(to, begin + stride);
            BodySurfaceTransform a = Sample(begin), b = Sample(end);
            if (BodySurfaceSweep.Find(body, a, b, terrain, environment, out BodySurfaceHit contact))
            {
                double at = begin + (end - begin) * contact.Fraction;
                Vector3 local = BodySurfaceTransform.Lerp(a, b, contact.Fraction).Matrix.inverse.MultiplyPoint3x4(contact.BodyPoint);
                hit = new BodySurfaceHit((float)((at - from) / (to - from)), contact.Point, contact.Normal,
                    contact.Reason, contact.Owner, Sample(at).Matrix.MultiplyPoint3x4(local));
                return true;
            }
            begin = end;
        }
        return false;
    }
}
