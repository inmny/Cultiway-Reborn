using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际作用形状与同时运动的模型三角面的连续接触，不用模型外包代替表面。</summary>
internal static class AbilityTriangleSweep
{
    /// <summary>按距离允许的进度前进，返回最先碰到的真实三角面位置。</summary>
    /// <param name="from">来源前态。</param><param name="to">来源后态。</param>
    /// <param name="a0">三角面前态第一点。</param><param name="b0">前态第二点。</param><param name="c0">前态第三点。</param>
    /// <param name="a1">三角面后态第一点。</param><param name="b1">后态第二点。</param><param name="c1">后态第三点。</param>
    /// <param name="fraction">首次接触进度。</param><param name="point">目标模型上的真实位置。</param>
    /// <param name="normal">由来源指向目标的方向。</param>
    internal static bool Find(in AbilityCapsule from, in AbilityCapsule to, Vector3 a0, Vector3 b0, Vector3 c0,
        Vector3 a1, Vector3 b1, Vector3 c1, out float fraction, out Vector3 point, out Vector3 normal)
    {
        var turn = new AbilitySweepTurn(from.SweepPose, to.SweepPose);
        float speed = from.TravelTo(to, turn, out _) + Mathf.Max((a1 - a0).magnitude, (b1 - b0).magnitude, (c1 - c0).magnitude);
        fraction = 0f;
        point = normal = Vector3.zero;
        for (int iteration = 0; iteration < 128; iteration++)
        {
            Vector3 a = Vector3.Lerp(a0, a1, fraction),
                b = Vector3.Lerp(b0, b1, fraction),
                c = Vector3.Lerp(c0, c1, fraction);
            float distance = Distance(from, to, turn, fraction, a, b, c, out Vector3 source, out point) -
                             Mathf.Lerp(from.Radius, to.Radius, fraction);
            if (distance <= .00001f)
            {
                normal = (point - source).normalized;
                if (normal.sqrMagnitude < 1e-10f)
                {
                    normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Vector3.Dot(normal, (to.A + to.B) * .5f - point) > 0f) normal = -normal;
                }

                return true;
            }

            if (speed <= 1e-10f) return false;
            fraction += distance / speed;
            if (fraction > 1f) return false;
        }

        return false;
    }

    /// <summary>胶囊轴或有限箱体与三角面的距离；先检查穿面，再检查各条有限边。</summary>
    /// <param name="from">来源前态。</param><param name="to">来源后态。</param><param name="at">共同进度。</param>
    /// <param name="a">面第一点。</param><param name="b">面第二点。</param><param name="c">面第三点。</param>
    /// <param name="source">来源基础形状上的最近点。</param><param name="target">三角面上的最近点。</param>
    private static float Distance(in AbilityCapsule from, in AbilityCapsule to, in AbilitySweepTurn turn, float at,
        Vector3 a, Vector3 b, Vector3 c, out Vector3 source, out Vector3 target)
    {
        if (to.Prism != null) return AbilityPrism.Sample(from, to, turn, at).Triangle(a, b, c, out source, out target);
        Vector3 start = turn.Point(from.A, to.A, at), end = turn.Point(from.B, to.B, at);
        if (!to.IsBox) return Segment(start, end, a, b, c, out source, out target);
        Vector3 half = Vector3.Lerp(from.HalfExtents, to.HalfExtents, at), min = start - half, max = start + half;
        Vector3 bestSource = default, bestTarget = default;
        float best = float.PositiveInfinity;
        Edge(a, b);
        Edge(b, c);
        Edge(c, a);
        for (int axis = 0; axis < 3; axis++)
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 p = min, q = min;
            p[(axis + 1) % 3] = q[(axis + 1) % 3] = (corner & 1) == 0 ? min[(axis + 1) % 3] : max[(axis + 1) % 3];
            p[(axis + 2) % 3] = q[(axis + 2) % 3] = (corner & 2) == 0 ? min[(axis + 2) % 3] : max[(axis + 2) % 3];
            q[axis] = max[axis];
            float distance = Segment(p, q, a, b, c, out Vector3 left, out Vector3 right);
            if (distance < best)
            {
                best = distance;
                bestSource = left;
                bestTarget = right;
            }
        }

        source = bestSource;
        target = bestTarget;
        return best;

        /// <summary>比较三角面的一条有限边与箱体。</summary>
        /// <param name="p">边起点。</param><param name="q">边终点。</param>
        void Edge(Vector3 p, Vector3 q)
        {
            AbilityBoxContact.Closest(p, q, min, max, out Vector3 right, out Vector3 left);
            float distance = Vector3.Distance(left, right);
            if (distance < best)
            {
                best = distance;
                bestSource = left;
                bestTarget = right;
            }
        }
    }

    /// <summary>线段与三角面最近点，退化的边仍按有限线段处理。</summary>
    /// <param name="p">轴起点。</param><param name="q">轴终点。</param>
    /// <param name="a">面第一点。</param><param name="b">面第二点。</param><param name="c">面第三点。</param>
    /// <param name="source">轴上的最近点。</param><param name="target">面上的最近点。</param>
    internal static float Segment(Vector3 p, Vector3 q, Vector3 a, Vector3 b, Vector3 c,
        out Vector3 source, out Vector3 target)
    {
        Vector3 n = Vector3.Cross(b - a, c - a), delta = q - p;
        float denominator = Vector3.Dot(n, delta);
        if (Mathf.Abs(denominator) > 1e-12f)
        {
            float at = Vector3.Dot(n, a - p) / denominator;
            Vector3 hit = p + delta * at;
            if (at >= 0f && at <= 1f && Vector3.Dot(n, Vector3.Cross(b - a, hit - a)) >= 0f &&
                Vector3.Dot(n, Vector3.Cross(c - b, hit - b)) >= 0f &&
                Vector3.Dot(n, Vector3.Cross(a - c, hit - c)) >= 0f)
            {
                source = target = hit;
                return 0f;
            }
        }

        Vector3 bestSource = p, bestTarget = Point(p, a, b, c);
        float best = (bestSource - bestTarget).sqrMagnitude;
        Vector3 last = Point(q, a, b, c);
        if ((q - last).sqrMagnitude < best)
        {
            bestSource = q;
            bestTarget = last;
            best = (q - last).sqrMagnitude;
        }

        Edge(a, b);
        Edge(b, c);
        Edge(c, a);
        source = bestSource;
        target = bestTarget;
        return Mathf.Sqrt(best);

        /// <summary>比较目标的一条边与来源轴线。</summary>
        /// <param name="start">边起点。</param><param name="end">边终点。</param>
        void Edge(Vector3 start, Vector3 end)
        {
            AbilityPairSweep.Closest(p, q, start, end, out Vector3 left, out Vector3 right);
            float distance = (left - right).sqrMagnitude;
            if (distance < best)
            {
                bestSource = left;
                bestTarget = right;
                best = distance;
            }
        }
    }

    /// <summary>点在三角面或其有限边上的最近位置。</summary>
    /// <param name="point">查询点。</param><param name="a">面第一点。</param><param name="b">面第二点。</param><param name="c">面第三点。</param>
    internal static Vector3 Point(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, n = Vector3.Cross(ab, ac);
        if (n.sqrMagnitude > 1e-20f)
        {
            Vector3 flat = point - n * (Vector3.Dot(point - a, n) / n.sqrMagnitude);
            if (Vector3.Dot(n, Vector3.Cross(ab, flat - a)) >= 0f &&
                Vector3.Dot(n, Vector3.Cross(c - b, flat - b)) >= 0f &&
                Vector3.Dot(n, Vector3.Cross(a - c, flat - c)) >= 0f) return flat;
        }

        Vector3 best = a;
        float distance = float.PositiveInfinity;
        Edge(a, b);
        Edge(b, c);
        Edge(c, a);
        return best;

        /// <summary>比较查询点与一条有限边。</summary>
        /// <param name="start">边起点。</param><param name="end">边终点。</param>
        void Edge(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            Vector3 candidate = start + delta * (delta.sqrMagnitude <= 1e-20f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(point - start, delta) / delta.sqrMagnitude));
            float squared = (point - candidate).sqrMagnitude;
            if (squared < distance)
            {
                distance = squared;
                best = candidate;
            }
        }
    }
}