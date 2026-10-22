using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>薄层擦边时距离前进会渐近停住；按箱体面和边求首次真实接触。</summary>
internal static class AbilityBoxSweep
{
    /// <summary>面由端点到箱体分段距离覆盖，内部胶囊轴另检查十二条真实箱边。</summary>
    /// <param name="a0">左前态。</param><param name="a1">左后态。</param>
    /// <param name="b0">右前态。</param><param name="b1">右后态。</param>
    /// <param name="fraction">首次接触比例。</param>
    internal static bool Find(AbilityCapsule a0, AbilityCapsule a1, AbilityCapsule b0, AbilityCapsule b1, out float fraction)
    {
        if (a1.IsBox && b1.IsBox) return Boxes(a0, a1, b0, b1, out fraction);
        AbilityCapsule box0 = a1.IsBox ? a0 : b0, box1 = a1.IsBox ? a1 : b1;
        AbilityCapsule cap0 = a1.IsBox ? b0 : a0, cap1 = a1.IsBox ? b1 : a1;
        float best = float.PositiveInfinity;
        Point(cap0.A, cap1.A); Point(cap0.B, cap1.B);
        for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 4; side++)
        {
            Vector3 signs = Vector3.zero; signs[(axis + 1) % 3] = (side & 1) == 0 ? -1 : 1; signs[(axis + 2) % 3] = (side & 2) == 0 ? -1 : 1;
            Vector3 low = signs, high = signs; low[axis] = -1; high[axis] = 1;
            var edge0 = new AbilityCapsule(0, box0.A + Vector3.Scale(box0.HalfExtents, low), box0.A + Vector3.Scale(box0.HalfExtents, high), 0, null);
            var edge1 = new AbilityCapsule(0, box1.A + Vector3.Scale(box1.HalfExtents, low), box1.A + Vector3.Scale(box1.HalfExtents, high), 0, null);
            if (AbilityPairSweep.Find(edge0, edge1, cap0, cap1, out float at, out _, out _)) Include(at);
        }
        fraction = best; return !float.IsPositiveInfinity(best);
        void Include(float at)
        {
            if (at < best && AbilityBoxContact.Distance(a0, a1, b0, b1, at, out _, out _) <= .00002f) best = at;
        }
        void Point(Vector3 from, Vector3 to)
        {
            Vector3 p = from - box0.A, dp = to - box1.A - p, h = box0.HalfExtents, dh = box1.HalfExtents - h;
            var breaks = new float[8]; int count = 2; breaks[0] = 0; breaks[1] = 1;
            for (int axis = 0; axis < 3; axis++) for (int sign = -1; sign <= 1; sign += 2)
            {
                float denominator = dp[axis] - sign * dh[axis];
                if (Mathf.Abs(denominator) < 1e-12f) continue;
                float at = (sign * h[axis] - p[axis]) / denominator;
                if (at > 0 && at < 1) breaks[count++] = at;
            }
            Array.Sort(breaks, 0, count);
            double radius = cap0.Radius + .00001, dr = cap1.Radius - cap0.Radius;
            for (int interval = 0; interval < count - 1; interval++)
            {
                double start = breaks[interval], end = breaks[interval + 1], middle = (start + end) * .5;
                double aa = -dr * dr, bb = -2 * radius * dr, cc = -radius * radius;
                for (int axis = 0; axis < 3; axis++)
                {
                    double value = p[axis] + dp[axis] * middle, extent = h[axis] + dh[axis] * middle;
                    if (value >= -extent && value <= extent) continue;
                    double side = value < -extent ? -1 : 1, offset = p[axis] - side * h[axis], slope = dp[axis] - side * dh[axis];
                    aa += slope * slope; bb += 2 * offset * slope; cc += offset * offset;
                }
                if ((aa * start + bb) * start + cc <= 0) Include((float)start);
                if (Math.Abs(aa) < 1e-20) { if (Math.Abs(bb) > 1e-20) Root(-cc / bb); continue; }
                double discriminant = bb * bb - 4 * aa * cc;
                if (discriminant < 0) continue;
                double sqrt = Math.Sqrt(discriminant); Root((-bb - sqrt) / (2 * aa)); Root((-bb + sqrt) / (2 * aa));
                void Root(double at) { if (at >= start && at <= end) Include((float)at); }
            }
        }
    }

    /// <summary>两个同时变宽的轴对齐箱体按六个线性不等式求共同接触区间。</summary>
    /// <param name="a0">左前态。</param><param name="a1">左后态。</param>
    /// <param name="b0">右前态。</param><param name="b1">右后态。</param>
    /// <param name="fraction">首次接触比例。</param>
    private static bool Boxes(in AbilityCapsule a0, in AbilityCapsule a1, in AbilityCapsule b0, in AbilityCapsule b1, out float fraction)
    {
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 3; axis++) for (int side = -1; side <= 1; side += 2)
        {
            float before = side * (a0.A[axis] - b0.A[axis]) - a0.HalfExtents[axis] - b0.HalfExtents[axis] - .00001f;
            float after = side * (a1.A[axis] - b1.A[axis]) - a1.HalfExtents[axis] - b1.HalfExtents[axis] - .00001f;
            if (before > 0 && after > 0) { fraction = 1; return false; }
            if (before > 0) enter = Mathf.Max(enter, before / (before - after));
            if (after > 0) exit = Mathf.Min(exit, before / (before - after));
        }
        fraction = enter; return enter <= exit;
    }
}
