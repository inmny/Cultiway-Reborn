using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际部位的转动中心和朝向。没有此姿态的形状沿用双端直移。</summary>
internal readonly struct AbilitySweepPose
{
    internal readonly Vector3 Pivot;
    internal readonly Quaternion Rotation;

    internal AbilitySweepPose(Matrix4x4 matrix) : this(matrix.GetColumn(3),
        Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1))) { }

    internal AbilitySweepPose(Vector3 pivot, Quaternion rotation) { Pivot = pivot; Rotation = rotation; }

    internal AbilitySweepPose Move(Matrix4x4 world) => new(world.MultiplyPoint3x4(Pivot),
        Quaternion.LookRotation(world.MultiplyVector(Rotation * Vector3.forward), world.MultiplyVector(Rotation * Vector3.up)));
}

/// <summary>将两端的实际形变放回随部位转动的坐标；转弯不把实体挤向弧的内侧。</summary>
internal readonly struct AbilitySweepTurn
{
    private readonly Vector3 from, to, axis;
    private readonly Quaternion start, delta, inverse;
    private readonly float radians;

    internal bool Active => radians > 0f;

    internal AbilitySweepTurn(AbilitySweepPose? first, AbilitySweepPose? last)
    {
        this = default;
        if (!first.HasValue || !last.HasValue) return;
        AbilitySweepPose a = first.Value, b = last.Value;
        start = a.Rotation;
        from = a.Pivot;
        to = b.Pivot;
        // 相同方向不走旋转路径。用虚部量小角度，不用会把小转角舍入为零的点积角度。
        if (start.Equals(b.Rotation)) return;
        delta = b.Rotation * Quaternion.Inverse(start);
        if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);
        Vector3 vector = new(delta.x, delta.y, delta.z);
        float sine = vector.magnitude;
        if (sine <= 1e-7f) return;
        axis = vector / sine;
        radians = 2f * Mathf.Atan2(sine, delta.w);
        inverse = Quaternion.Inverse(delta);
    }

    private Quaternion RotationAt(float at) => Quaternion.AngleAxis(radians * Mathf.Rad2Deg * at, axis);

    internal AbilitySweepPose? PoseAt(AbilitySweepPose? first, AbilitySweepPose? last, float at)
    {
        if (!first.HasValue || !last.HasValue) return null;
        return Active ? new AbilitySweepPose(Vector3.LerpUnclamped(from, to, at), RotationAt(at) * start) :
            new AbilitySweepPose(Vector3.LerpUnclamped(first.Value.Pivot, last.Value.Pivot, at), start);
    }

    internal Vector3 Point(Vector3 a, Vector3 b, float at)
        => !Active || at <= 0f || at >= 1f ? Vector3.Lerp(a, b, at) :
            Vector3.LerpUnclamped(from, to, at) + RotationAt(at) * Vector3.LerpUnclamped(a - from, inverse * (b - to), at);

    internal Matrix4x4 Frame(Matrix4x4 a, Matrix4x4 b, float at)
    {
        if (at <= 0f) return a;
        if (at >= 1f) return b;
        var matrix = new Matrix4x4();
        if (!Active)
        {
            for (int column = 0; column < 4; column++)
                matrix.SetColumn(column, Vector4.LerpUnclamped(a.GetColumn(column), b.GetColumn(column), at));
            return matrix;
        }

        Quaternion rotation = RotationAt(at);
        for (int column = 0; column < 3; column++)
            matrix.SetColumn(column, rotation * Vector3.LerpUnclamped(a.GetColumn(column), inverse * (Vector3)b.GetColumn(column), at));
        Vector3 centre = Vector3.LerpUnclamped(from, to, at) + rotation * Vector3.LerpUnclamped((Vector3)a.GetColumn(3) - from,
            inverse * ((Vector3)b.GetColumn(3) - to), at);
        matrix.SetColumn(3, new Vector4(centre.x, centre.y, centre.z, 1f));
        return matrix;
    }

    internal Vector3 Velocity(Vector3 a, Vector3 b, float at)
    {
        if (!Active) return b - a;
        Vector3 left = a - from, right = inverse * (b - to);
        Quaternion rotation = RotationAt(at);
        return to - from + rotation * (right - left + radians * Vector3.Cross(axis, Vector3.LerpUnclamped(left, right, at)));
    }

    /// <summary>转动半径与形变给出安全的速度上界，以及真实弧离首尾连线的最大偏离。</summary>
    internal void Measure(Vector3 a, Vector3 b, out float speed, out float padding)
    {
        if (!Active) { speed = Vector3.Distance(a, b); padding = 0f; return; }
        Vector3 left = a - from, right = inverse * (b - to), change = right - left;
        float radius = Mathf.Max(Vector3.Cross(left, axis).magnitude, Vector3.Cross(right, axis).magnitude);
        speed = Vector3.Distance(from, to) + change.magnitude + radians * radius;
        // 往返起终点的曲线和直线之差，在最大二阶导数的八分之一以内。
        padding = (radians * radius + 2f * Vector3.Cross(change, axis).magnitude) * radians * .125f;
    }
}
