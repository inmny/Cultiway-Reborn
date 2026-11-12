using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>携带身体的真实表面首次进入环境时的数字结果。</summary>
internal readonly struct BodySurfaceHit
{
    /// <summary>共同小步中的实际进度，世界环境身份及明确原因。</summary>
    internal readonly float Fraction;

    /// <summary>实际环境身份，地面为零。</summary>
    internal readonly long Owner;

    /// <summary>真实环境接触来源。</summary>
    internal readonly string Reason;

    /// <summary>真实接触点及指向身体外侧的环境法线。</summary>
    internal readonly Vector3 Point, Normal;

    /// <summary>身体上的真实位置；原有重叠时与环境点分开保留。</summary>
    internal readonly Vector3 BodyPoint;

    /// <summary>只记录数值，确认前不移动游戏人物。</summary>
    /// <param name="fraction">首次进度。</param><param name="point">接触点。</param><param name="normal">外侧法线。</param>
    /// <param name="reason">地面或模型接触。</param><param name="owner">环境身份。</param>
    /// <param name="bodyPoint">身体真实位置；无原有重叠时与环境点一致。</param>
    internal BodySurfaceHit(float fraction, Vector3 point, Vector3 normal, string reason, long owner = 0, Vector3? bodyPoint = null)
    { Fraction = fraction; Point = point; Normal = normal; Reason = reason; Owner = owner; BodyPoint = bodyPoint ?? point; }
}

/// <summary>实际刚性表面与地面、环境三角面的首次接触，不使用站立人物胶囊代替。</summary>
internal static class BodySurfaceSweep
{
    /// <summary>沿用共同三角面查询的数值精度。</summary>
    private const float Tolerance = .00001f;

    /// <summary>平面上的刚性旋转以解析轨迹分出单调段，再求第一次进入，不在初始承托处卡死。</summary>
    /// <param name="points">建立时的真实顶点。</param><param name="from">确认前态。</param><param name="to">本步预测后态。</param>
    /// <param name="normal">平面法线。</param><param name="offset">平面常数。</param><param name="hit">真实首次接触。</param>
    internal static bool Plane(Vector3[] points, BodySurfaceTransform from, BodySurfaceTransform to,
        Vector3 normal, float offset, out BodySurfaceHit hit)
    {
        Quaternion delta = to.Rotation * Quaternion.Inverse(from.Rotation);
        delta.ToAngleAxis(out float degrees, out Vector3 axis);
        if (degrees > 180f) { degrees = 360f - degrees; axis = -axis; }
        float angle = degrees * Mathf.Deg2Rad;
        Vector3 translation = to.Point - from.Point;
        float drift = Vector3.Dot(normal, translation), first = float.PositiveInfinity;
        Vector3 winning = default;
        foreach (Vector3 point in points)
        {
            Vector3 v = from.Rotation * (point - from.Pivot);
            Vector3 parallel = angle < 1e-7f ? v : axis * Vector3.Dot(axis, v);
            float a = Vector3.Dot(normal, v - parallel), b = angle < 1e-7f ? 0f : Vector3.Dot(normal, Vector3.Cross(axis, v));
            float c = Vector3.Dot(normal, from.Point + parallel) - offset;
            // 原脚底已经有重叠时可离开或抬起，只有继续压深才形成新的阻挡。
            float baseline = Mathf.Min(0f, c + a);
            float left = 0f;
            for (int segment = 0; segment < 4 && left < first; segment++)
            {
                float right = 1f;
                float amplitude = angle * Mathf.Sqrt(a * a + b * b);
                if (amplitude > Mathf.Abs(drift) && angle > 1e-7f)
                {
                    float phase = Mathf.Atan2(a, b), root = Mathf.Acos(-drift / amplitude);
                    for (int turn = -1; turn <= 1; turn++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float candidate = (sign * root + turn * Mathf.PI * 2f - phase) / angle;
                        if (candidate > left + 1e-7f && candidate < right) right = candidate;
                    }
                }
                if (Value(left) < -Tolerance || Value(right) < -Tolerance)
                {
                    float low = left, high = right;
                    if (Value(low) >= 0f)
                        for (int i = 0; i < 24; i++)
                        {
                            float mid = (low + high) * .5f;
                            if (Value(mid) >= 0f) low = mid; else high = mid;
                        }
                    if (low < first) { first = low; winning = point; }
                    break;
                }
                if (right >= 1f) break;
                left = right;
            }

            /// <summary>真实刚性转动在指定平面上的有符号距离。</summary>
            /// <param name="at">小步进度。</param>
            float Value(float at) => c + drift * at + a * Mathf.Cos(angle * at) + b * Mathf.Sin(angle * at) - baseline;
        }
        hit = default;
        if (float.IsPositiveInfinity(first)) return false;
        Matrix4x4 matrix = BodySurfaceTransform.Lerp(from, to, first).Matrix;
        Vector3 witness = matrix.MultiplyPoint3x4(winning);
        float distance = Vector3.Dot(normal, witness) - offset;
        Vector3 environment = witness - normal * (distance / normal.sqrMagnitude);
        hit = new BodySurfaceHit(first, environment, normal.normalized, "GroundContact", bodyPoint: witness);
        return true;
    }

    /// <summary>地面与真实模型共同选最早时点；转动按曲线误差拆分，不用端点直线穿过中间障碍。</summary>
    /// <param name="body">建立时真实可见几何。</param><param name="from">已确认刚性姿态。</param><param name="to">本步预测姿态。</param>
    /// <param name="terrain">共同地面，空值为原版零平面。</param><param name="obstacles">已经冻结的实际环境。</param>
    /// <param name="hit">共同最早接触。</param><param name="ground">是否同时检查地面；贴地过程自行维持承托。</param>
    internal static bool Find(BodySurfaceMesh body, BodySurfaceTransform from, BodySurfaceTransform to,
        AbilityHeightField terrain, IReadOnlyList<BodySurfaceObstacle> obstacles, out BodySurfaceHit hit, bool ground = true)
    {
        hit = default;
        if (body.Points.Length == 0 || !ground && obstacles.Count == 0) return false;
        float radius = 0f;
        foreach (Vector3 point in body.Points) radius = Mathf.Max(radius, (point - from.Pivot).magnitude);
        float angle = Quaternion.Angle(from.Rotation, to.Rotation) * Mathf.Deg2Rad;
        float speed = (to.Point - from.Point).magnitude + angle * radius;
        var before = Array.ConvertAll(body.Points, from.Matrix.MultiplyPoint3x4);
        var after = new Vector3[before.Length];
        Bounds sweep = new(before[0], Vector3.zero);
        foreach (Vector3 point in before) sweep.Encapsulate(point);
        foreach (Vector3 point in body.Points) sweep.Encapsulate(to.Matrix.MultiplyPoint3x4(point));
        sweep.Expand(radius * angle * angle * .25f + Tolerance * 2f);
        Vector3 normal = Vector3.forward;
        float offset = 0f, maximum = 0f, slope = 0f;
        bool plane = terrain == null || terrain.Plane(new Rect(sweep.min.x, sweep.min.y, sweep.size.x, sweep.size.y),
            out normal, out offset, out maximum, out slope);
        bool found = ground && (plane ? Plane(body.Points, from, to, normal, offset, out hit) :
            Ground(body, from, to, terrain, maximum, speed * (1f + slope), out hit));
        int count = Mathf.Max(1, Mathf.CeilToInt(angle * Mathf.Sqrt(radius / (8f * Tolerance))));
        var nearby = new List<int>();
        for (int slice = 0; slice < count && slice / (float)count < (found ? hit.Fraction : 1f); slice++)
        {
            float start = slice / (float)count, end = (slice + 1f) / count;
            Matrix4x4 matrix = BodySurfaceTransform.Lerp(from, to, end).Matrix;
            for (int i = 0; i < after.Length; i++) after[i] = matrix.MultiplyPoint3x4(body.Points[i]);
            foreach (BodySurfaceObstacle obstacle in obstacles)
            {
                if (!sweep.Intersects(obstacle.Mesh.Bounds)) continue;
                BodySurfaceMesh environment = obstacle.Mesh;
                for (int triangle = 0; triangle < body.Triangles.Length; triangle += 3)
                {
                    int a = body.Triangles[triangle], b = body.Triangles[triangle + 1], c = body.Triangles[triangle + 2];
                    var area = new Bounds(before[a], Vector3.zero);
                    area.Encapsulate(before[b]); area.Encapsulate(before[c]);
                    area.Encapsulate(after[a]); area.Encapsulate(after[b]); area.Encapsulate(after[c]);
                    area.Expand(Tolerance * 4f);
                    environment.Query(area, nearby);
                    foreach (int face in nearby)
                    {
                        int f = face * 3;
                        Vector3 x = environment.Points[environment.Triangles[f]], y = environment.Points[environment.Triangles[f + 1]],
                            z = environment.Points[environment.Triangles[f + 2]];
                        if (!Triangles(before[a], before[b], before[c], after[a], after[b], after[c], x, y, z,
                                out float at, out Vector3 point, out Vector3 outward)) continue;
                        float fraction = Mathf.Lerp(start, end, at);
                        if (found && fraction >= hit.Fraction) continue;
                        hit = new BodySurfaceHit(fraction, point, outward, obstacle.Reason, obstacle.Owner);
                        found = true;
                    }
                }
            }
            (before, after) = (after, before);
        }
        return found;
    }

    /// <summary>正反两组有限边共同核对面接触，初始承托向外或切向移动不当作再次撞入。</summary>
    /// <param name="a0">身体前态第一点。</param><param name="b0">第二点。</param><param name="c0">第三点。</param>
    /// <param name="a1">身体后态第一点。</param><param name="b1">第二点。</param><param name="c1">第三点。</param>
    /// <param name="x">环境第一点。</param><param name="y">第二点。</param><param name="z">第三点。</param>
    /// <param name="at">首次进度。</param><param name="point">实际接触点。</param><param name="normal">环境外侧法线。</param>
    private static bool Triangles(Vector3 a0, Vector3 b0, Vector3 c0, Vector3 a1, Vector3 b1, Vector3 c1,
        Vector3 x, Vector3 y, Vector3 z, out float at, out Vector3 point, out Vector3 normal)
    {
        Vector3 outward = Vector3.Cross(y - x, z - x).normalized;
        if (Vector3.Dot(outward, (a0 + b0 + c0) / 3f - x) < 0f) outward = -outward;
        float first = float.PositiveInfinity;
        Vector3 contact = default;
        Edge(a0, b0, a1, b1, x, y, z, x, y, z);
        Edge(b0, c0, b1, c1, x, y, z, x, y, z);
        Edge(c0, a0, c1, a1, x, y, z, x, y, z);
        Edge(x, y, x, y, a0, b0, c0, a1, b1, c1);
        Edge(y, z, y, z, a0, b0, c0, a1, b1, c1);
        Edge(z, x, z, x, a0, b0, c0, a1, b1, c1);
        at = first; point = contact; normal = outward;
        return !float.IsPositiveInfinity(first);

        /// <summary>沿现有共同三角面扫掠查询，不另加更粗的身体或碰撞壳。</summary>
        /// <param name="p0">边前态起点。</param><param name="q0">终点。</param><param name="p1">后态起点。</param><param name="q1">终点。</param>
        /// <param name="u0">面前态第一点。</param><param name="v0">第二点。</param><param name="w0">第三点。</param>
        /// <param name="u1">面后态第一点。</param><param name="v1">第二点。</param><param name="w1">第三点。</param>
        void Edge(Vector3 p0, Vector3 q0, Vector3 p1, Vector3 q1, Vector3 u0, Vector3 v0, Vector3 w0, Vector3 u1, Vector3 v1, Vector3 w1)
        {
            var begin = new AbilityCapsule(0, p0, q0, 0f, null);
            var end = new AbilityCapsule(0, p1, q1, 0f, null);
            if (!AbilityTriangleSweep.Find(begin, end, u0, v0, w0, u1, v1, w1, out float time, out Vector3 hit, out _)) return;
            if (time <= 1e-6f && Vector3.Dot(a1 - a0, outward) >= 0f &&
                Vector3.Dot(b1 - b0, outward) >= 0f && Vector3.Dot(c1 - c0, outward) >= 0f) return;
            if (time < first) { first = time; contact = hit; }
        }
    }

    /// <summary>非平面地面先求真实边的最小地面差，再用运动与坡度上界排除不可能穿入的小段。</summary>
    /// <param name="body">真实身体。</param><param name="from">前态。</param><param name="to">后态。</param>
    /// <param name="terrain">双线性地面。</param><param name="maximum">局部最高高度。</param><param name="speed">地面差变化上界。</param>
    /// <param name="hit">实际首次进入。</param>
    private static bool Ground(BodySurfaceMesh body, BodySurfaceTransform from, BodySurfaceTransform to,
        AbilityHeightField terrain, float maximum, float speed, out BodySurfaceHit hit)
    {
        var points = new Vector3[body.Points.Length];
        BodySurfaceHit result = default;
        float baseline = 0f;
        baseline = Mathf.Min(0f, Gap(0f, out _));
        float a = Gap(0f, out _), b = Gap(1f, out _);
        bool found = Search(0f, 1f, a, b);
        hit = result;
        return found;

        /// <summary>按时间从前到后细分；只有最大可能穿入已低于共同容差才舍弃。</summary>
        /// <param name="left">区间起点。</param><param name="right">终点。</param><param name="dl">起点地面差。</param><param name="dr">终点地面差。</param>
        bool Search(float left, float right, float dl, float dr)
        {
            if (Mathf.Min(dl, dr) - speed * (right - left) * .5f >= -Tolerance) return false;
            if (dl < -Tolerance || speed * (right - left) <= Tolerance * .5f)
            {
                if (dl >= -Tolerance && dr >= -Tolerance) return false;
                float at = dl < -Tolerance ? left : right;
                Gap(at, out Vector3 point);
                float step = .001f;
                Vector3 normal = new(terrain.Sample(point.x - step, point.y) - terrain.Sample(point.x + step, point.y),
                    terrain.Sample(point.x, point.y - step) - terrain.Sample(point.x, point.y + step), step * 2f);
                result = new BodySurfaceHit(at, new Vector3(point.x, point.y, terrain.Sample(point.x, point.y)), normal.normalized, "GroundContact", bodyPoint: point);
                return true;
            }
            float middle = (left + right) * .5f, dm = Gap(middle, out _);
            return Search(left, middle, dl, dm) || Search(middle, right, dm, dr);
        }

        /// <summary>只检查可能接近地面的实际边，不从包围盒生成额外接触面。</summary>
        /// <param name="at">共同进度。</param><param name="witness">实际最小点。</param>
        float Gap(float at, out Vector3 witness)
        {
            Matrix4x4 matrix = BodySurfaceTransform.Lerp(from, to, at).Matrix;
            for (int i = 0; i < points.Length; i++) points[i] = matrix.MultiplyPoint3x4(body.Points[i]);
            float minimum = float.PositiveInfinity;
            witness = default;
            for (int i = 0; i < body.Edges.Length; i += 2)
            {
                Vector3 a = points[body.Edges[i]], b = points[body.Edges[i + 1]];
                if (Mathf.Min(a.z, b.z) > maximum + Tolerance)
                {
                    float lower = Mathf.Min(a.z, b.z) - maximum;
                    if (lower < minimum) { minimum = lower; witness = a.z < b.z ? a : b; }
                    continue;
                }
                float gap = EdgeGround(a, b, terrain, out Vector3 closest);
                if (gap < minimum) { minimum = gap; witness = closest; }
            }
            for (int face = 0; face < body.Triangles.Length; face += 3)
            {
                Vector3 a = points[body.Triangles[face]], b = points[body.Triangles[face + 1]], c = points[body.Triangles[face + 2]];
                if (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > maximum + Tolerance) continue;
                float gap = FaceGround(a, b, c, terrain, out Vector3 closest);
                if (gap < minimum) { minimum = gap; witness = closest; }
            }
            return minimum - baseline;
        }
    }

    /// <summary>补查实际面内部的原地面格点；边界已由真实边覆盖，双线性地面不会在格内产生独立极小值。</summary>
    /// <param name="a">表面第一点。</param><param name="b">第二点。</param><param name="c">第三点。</param>
    /// <param name="terrain">同一地面。</param><param name="point">面内实际最小地面差位置。</param>
    internal static float FaceGround(Vector3 a, Vector3 b, Vector3 c, AbilityHeightField terrain, out Vector3 point)
    {
        Vector3 ab = b - a, ac = c - a;
        float determinant = ab.x * ac.y - ab.y * ac.x;
        float minimum = float.PositiveInfinity;
        point = default;
        if (Mathf.Abs(determinant) < 1e-12f) return minimum;
        float dx = terrain.Size.x / terrain.Width, dy = terrain.Size.y / terrain.Height;
        int left = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / dx - .5f)),
            right = Mathf.Min(terrain.Width - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / dx - .5f)),
            bottom = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) / dy - .5f)),
            top = Mathf.Min(terrain.Height - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) / dy - .5f));
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
        {
            float px = (x + .5f) * dx, py = (y + .5f) * dy;
            float u = ((px - a.x) * ac.y - (py - a.y) * ac.x) / determinant,
                v = (ab.x * (py - a.y) - ab.y * (px - a.x)) / determinant;
            if (u < 0f || v < 0f || u + v > 1f) continue;
            Vector3 candidate = new(px, py, a.z + ab.z * u + ac.z * v);
            float gap = candidate.z - terrain.Sample(px, py);
            if (gap < minimum) { minimum = gap; point = candidate; }
        }
        return minimum;
    }

    /// <summary>沿一条真实表面边按原格边界求地面差的二次最小值，不漏掉边中间的隆起。</summary>
    /// <param name="a">实际边起点。</param><param name="b">实际边终点。</param><param name="terrain">同一双线性地面。</param>
    /// <param name="point">最接近地面的真实表面位置。</param>
    internal static float EdgeGround(Vector3 a, Vector3 b, AbilityHeightField terrain, out Vector3 point)
    {
        Vector3 delta = b - a;
        float left = 0f, best = float.PositiveInfinity;
        Vector3 closest = a;
        while (left < 1f)
        {
            float right = 1f;
            Boundary(a.x, delta.x, terrain.Size.x / terrain.Width);
            Boundary(a.y, delta.y, terrain.Size.y / terrain.Height);
            float f0 = Gap(left), f1 = Gap(right), fm = Gap((left + right) * .5f);
            Include(left, f0); Include(right, f1);
            float quadratic = 2f * (f0 + f1 - 2f * fm), linear = f1 - f0 - quadratic;
            if (quadratic > 0f)
            {
                float at = -linear / (2f * quadratic);
                if (at > 0f && at < 1f)
                {
                    float t = Mathf.Lerp(left, right, at);
                    Include(t, Gap(t));
                }
            }
            left = right;

            /// <summary>每次只走到下一条原格中心分界。</summary>
            /// <param name="origin">该轴起点。</param><param name="movement">该轴变化。</param><param name="step">原格间距。</param>
            void Boundary(float origin, float movement, float step)
            {
                if (Mathf.Abs(movement) < 1e-12f) return;
                float coordinate = origin + movement * left;
                float cell = coordinate / step - .5f;
                float next = movement > 0f ? Mathf.Floor(cell + 1e-6f) + 1f : Mathf.Ceil(cell - 1e-6f) - 1f;
                float time = ((next + .5f) * step - origin) / movement;
                if (time > left + 1e-7f) right = Mathf.Min(right, time);
            }
        }
        point = closest;
        return best;

        /// <summary>同一边上的真实地面差。</summary>
        /// <param name="at">边内进度。</param>
        float Gap(float at)
        {
            Vector3 p = a + delta * at;
            return p.z - terrain.Sample(p.x, p.y);
        }
        /// <summary>保留实际最小点。</summary>
        /// <param name="at">边内位置。</param><param name="gap">实际地面差。</param>
        void Include(float at, float gap) { if (gap < best) { best = gap; closest = a + delta * at; } }
    }
}
