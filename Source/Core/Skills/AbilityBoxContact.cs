using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>薄层格与人物胶囊的实际距离，避免以水面宽度扩大竖直作用区。</summary>
internal static class AbilityBoxContact
{
    /// <summary>同一时点求箱体、胶囊的最近表面基础点。</summary>
    /// <param name="a0">左前态。</param><param name="a1">左后态。</param>
    /// <param name="b0">右前态。</param><param name="b1">右后态。</param>
    /// <param name="t">共同时间比例。</param><param name="a">左基础点。</param><param name="b">右基础点。</param>
    internal static float Distance(in AbilityCapsule a0, in AbilityCapsule a1, in AbilityCapsule b0,
        in AbilityCapsule b1, float t, out Vector3 a, out Vector3 b)
    {
        Vector3 ac = Vector3.Lerp(a0.A, a1.A, t), bc = Vector3.Lerp(b0.A, b1.A, t);
        Vector3 ah = Vector3.Lerp(a0.HalfExtents, a1.HalfExtents, t),
            bh = Vector3.Lerp(b0.HalfExtents, b1.HalfExtents, t);
        if (a1.IsBox && b1.IsBox)
        {
            a = Vector3.zero;
            b = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                a[axis] = Mathf.Clamp(bc[axis], ac[axis] - ah[axis], ac[axis] + ah[axis]);
                b[axis] = Mathf.Clamp(a[axis], bc[axis] - bh[axis], bc[axis] + bh[axis]);
            }
        }
        else if (a1.IsBox) Closest(bc, Vector3.Lerp(b0.B, b1.B, t), ac - ah, ac + ah, out b, out a);
        else Closest(ac, Vector3.Lerp(a0.B, a1.B, t), bc - bh, bc + bh, out a, out b);

        return Vector3.Distance(a, b) - Mathf.Lerp(a0.Radius + b0.Radius, a1.Radius + b1.Radius, t);
    }

    /// <summary>线段到轴对齐箱体的距离为分段二次式，按六个越界时点分别求最小。</summary>
    /// <param name="start">段起点。</param><param name="end">段终点。</param>
    /// <param name="min">箱体低角。</param><param name="max">箱体高角。</param>
    /// <param name="line">段上最近点。</param><param name="box">箱体最近点。</param>
    internal static void Closest(Vector3 start, Vector3 end, Vector3 min, Vector3 max, out Vector3 line,
        out Vector3 box)
    {
        Vector3 delta = end - start;
        float at = 0f, best = float.PositiveInfinity, bestAt = 0f;
        for (int interval = 0; interval < 7; interval++)
        {
            float next = 1f;
            for (int axis = 0; axis < 3; axis++)
                if (Mathf.Abs(delta[axis]) > 1e-12f)
                {
                    float lower = (min[axis] - start[axis]) / delta[axis],
                        upper = (max[axis] - start[axis]) / delta[axis];
                    if (lower > at + 1e-7f) next = Mathf.Min(next, lower);
                    if (upper > at + 1e-7f) next = Mathf.Min(next, upper);
                }

            float middle = (at + next) * .5f, aa = 0f, ab = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float p = start[axis] + delta[axis] * middle;
                if (p >= min[axis] && p <= max[axis]) continue;
                float offset = start[axis] - (p < min[axis] ? min[axis] : max[axis]);
                aa += delta[axis] * delta[axis];
                ab += delta[axis] * offset;
            }

            float t = aa > 1e-12f ? Mathf.Clamp(-ab / aa, at, next) : at;
            Vector3 candidate = start + delta * t, clamped = Vector3.Max(min, Vector3.Min(max, candidate));
            float distance = (candidate - clamped).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                bestAt = t;
            }

            if (next >= 1f) break;
            at = next;
        }

        line = start + delta * bestAt;
        box = Vector3.Max(min, Vector3.Min(max, line));
    }
}