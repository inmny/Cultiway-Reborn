using UnityEngine;

namespace Cultiway.Core.Combat;

/// <summary>
/// 战斗规划与实体碰撞共用的二维几何判定，确保预判和实际拦截采用相同边界语义。
/// </summary>
internal static class CombatGeometry
{
    /// <summary>判断线段是否穿过或接触指定圆形。</summary>
    internal static bool SegmentIntersectsCircle(
        Vector2 start,
        Vector2 end,
        Vector2 center,
        float radius)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(center - start, segment) / lengthSquared)
            : 0f;
        return (center - (start + segment * t)).sqrMagnitude <= radius * radius;
    }

    /// <summary>返回两条有限线段之间的最短距离平方；相交、共线接触时返回零。</summary>
    internal static float SegmentDistanceSquared(
        Vector2 a0,
        Vector2 a1,
        Vector2 b0,
        Vector2 b1)
    {
        if (SegmentsIntersect(a0, a1, b0, b1)) return 0f;
        return Mathf.Min(
            Mathf.Min(
                PointSegmentDistanceSquared(a0, b0, b1),
                PointSegmentDistanceSquared(a1, b0, b1)),
            Mathf.Min(
                PointSegmentDistanceSquared(b0, a0, a1),
                PointSegmentDistanceSquared(b1, a0, a1)));
    }

    private static bool SegmentsIntersect(
        Vector2 a0,
        Vector2 a1,
        Vector2 b0,
        Vector2 b1)
    {
        const float epsilon = 0.0001f;
        float d1 = Cross(a1 - a0, b0 - a0);
        float d2 = Cross(a1 - a0, b1 - a0);
        float d3 = Cross(b1 - b0, a0 - b0);
        float d4 = Cross(b1 - b0, a1 - b0);
        if (Mathf.Abs(d1) <= epsilon && IsOnSegment(b0, a0, a1)) return true;
        if (Mathf.Abs(d2) <= epsilon && IsOnSegment(b1, a0, a1)) return true;
        if (Mathf.Abs(d3) <= epsilon && IsOnSegment(a0, b0, b1)) return true;
        if (Mathf.Abs(d4) <= epsilon && IsOnSegment(a1, b0, b1)) return true;
        return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
    }

    private static bool IsOnSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        const float epsilon = 0.0001f;
        return point.x >= Mathf.Min(start.x, end.x) - epsilon &&
               point.x <= Mathf.Max(start.x, end.x) + epsilon &&
               point.y >= Mathf.Min(start.y, end.y) - epsilon &&
               point.y <= Mathf.Max(start.y, end.y) + epsilon;
    }

    private static float PointSegmentDistanceSquared(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared)
            : 0f;
        return (point - (start + segment * t)).sqrMagnitude;
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }
}
