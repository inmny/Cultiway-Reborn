using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>原版前景不透明轮廓的数值投影；只裁掉真实盖住的表面，不创建新的封口。</summary>
internal sealed class BodySurfaceOcclusion
{
    /// <summary>原像素面的固定顶点顺序，运动中即使折叠或翻面也不重排对应。</summary>
    private readonly Vector2[][] originals;

    /// <summary>按共同图片投影排列的逆时针凸多边形。</summary>
    private readonly Vector2[][] polygons;

    /// <summary>每块实际遮挡的投影范围，先跳过无关区域。</summary>
    private readonly Rect[] bounds;

    /// <summary>接收已经采集的投影轮廓。</summary>
    /// <param name="polygons">各块真实不透明区域。</param>
    private BodySurfaceOcclusion(Vector2[][] polygons)
    {
        originals = polygons;
        var visible = new List<Vector2[]>();
        foreach (Vector2[] source in polygons)
        {
            if (source.Length == 4)
            {
                float direction = Cross(source[1] - source[0], source[2] - source[1]);
                if (direction * Cross(source[2] - source[1], source[3] - source[2]) <= 0f ||
                    direction * Cross(source[3] - source[2], source[0] - source[3]) <= 0f ||
                    direction * Cross(source[0] - source[3], source[1] - source[0]) <= 0f)
                {
                    Add(new[] { source[0], source[1], source[2] });
                    Add(new[] { source[0], source[2], source[3] });
                    continue;
                }
            }

            Add(source);
        }

        this.polygons = visible.ToArray();
        bounds = new Rect[this.polygons.Length];
        for (int i = 0; i < this.polygons.Length; i++)
        {
            Vector2 min = this.polygons[i][0], max = min;
            foreach (Vector2 point in this.polygons[i])
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            bounds[i] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>实际当前姿态统一逆时针，退化投影只在本次查询中没有遮挡，不删除运动对应。</summary>
        /// <param name="source">当前原面投影。</param>
        void Add(Vector2[] source)
        {
            float area = 0f;
            for (int i = 1; i + 1 < source.Length; i++) area += Cross(source[i] - source[0], source[i + 1] - source[0]);
            if (Mathf.Abs(area) < 1e-12f) return;
            Vector2[] polygon = (Vector2[])source.Clone();
            if (area < 0f) Array.Reverse(polygon);
            visible.Add(polygon);
        }
    }

    /// <summary>从同轮真实不透明表面取得投影；相邻两个三角形能形成凸四边形时合并，避免内部接缝影响裁切。</summary>
    /// <param name="pose">已经完成变形的不透明身体表面。</param>
    internal static BodySurfaceOcclusion Create(BodySurfacePose pose) => Build(pose, float.PositiveInfinity);

    /// <summary>身体被带动时，遮挡轮廓与真实表面沿同一世界位移移动。</summary>
    /// <param name="offset">共同世界位移，竖直高度采用原版相同投影。</param>
    internal BodySurfaceOcclusion Translated(Vector3 offset)
    {
        Vector2 move = Project(offset);
        return new BodySurfaceOcclusion(Array.ConvertAll(originals,
            polygon => Array.ConvertAll(polygon, point => point + move)));
    }

    /// <summary>图片仍按原版固定层深，出招模型只取真正位于持物前方的部分。</summary>
    /// <param name="pose">同轮身体图片与实际臂链。</param><param name="depth">持物在共同空间中的真实层深。</param>
    internal static BodySurfaceOcclusion CreateBefore(BodySurfacePose pose, float depth) => Build(pose, depth);

    /// <summary>先按需要裁切实际模型的前后深度，再取同一姿态的可见投影。</summary>
    /// <param name="pose">实际不透明表面。</param><param name="depth">模型不得超过的层深；图片使用已有绘制顺序。</param>
    private static BodySurfaceOcclusion Build(BodySurfacePose pose, float depth)
    {
        var result = new List<Vector2[]>();
        for (int part = 0; part < pose.Parts.Length; part++)
        {
            BodySurfacePart mesh = pose.Parts[part];
            Matrix4x4 matrix = pose.Matrices[mesh.TransformIndex < 0 ? part : mesh.TransformIndex];
            Vector3[] vertices = pose.Vertices(part);
            for (int face = 0; face < mesh.Triangles.Length; face += 3)
            {
                int a = mesh.Triangles[face], b = mesh.Triangles[face + 1], c = mesh.Triangles[face + 2];
                if (mesh.Id != "opaque" && !float.IsPositiveInfinity(depth))
                {
                    var world = new List<Vector3>
                    {
                        matrix.MultiplyPoint3x4(vertices[a]), matrix.MultiplyPoint3x4(vertices[b]),
                        matrix.MultiplyPoint3x4(vertices[c])
                    };
                    if (face + 5 < mesh.Triangles.Length && mesh.Triangles[face + 3] == a &&
                        mesh.Triangles[face + 4] == c)
                    {
                        world.Add(matrix.MultiplyPoint3x4(vertices[mesh.Triangles[face + 5]]));
                        face += 3;
                    }
                    var clipped = new List<Vector3>();
                    for (int edge = 0; edge < world.Count; edge++)
                    {
                        Vector3 before = world[(edge + world.Count - 1) % world.Count], after = world[edge];
                        bool beforeIn = before.y <= depth, afterIn = after.y <= depth;
                        if (beforeIn != afterIn)
                            clipped.Add(Vector3.LerpUnclamped(before, after, (depth - before.y) / (after.y - before.y)));
                        if (afterIn) clipped.Add(after);
                    }
                    if (clipped.Count >= 3) result.Add(Array.ConvertAll(clipped.ToArray(), Project));
                    continue;
                }
                Vector2 pa = Project(matrix.MultiplyPoint3x4(vertices[a])),
                    pb = Project(matrix.MultiplyPoint3x4(vertices[b])),
                    pc = Project(matrix.MultiplyPoint3x4(vertices[c]));
                if (face + 5 < mesh.Triangles.Length && mesh.Triangles[face + 3] == a &&
                    mesh.Triangles[face + 4] == c)
                {
                    Vector2 pd = Project(matrix.MultiplyPoint3x4(vertices[mesh.Triangles[face + 5]]));
                    result.Add(new[] { pa, pb, pc, pd });
                    face += 3;
                    continue;
                }

                result.Add(new[] { pa, pb, pc });
            }
        }

        return new BodySurfaceOcclusion(result.ToArray());
    }

    /// <summary>原版共同空间到图片平面的同一投影。</summary>
    /// <param name="point">实际三维位置。</param>
    private static Vector2 Project(Vector3 point) => new(point.x, point.y + point.z);

    /// <summary>两条平面向量的有向面积。</summary>
    /// <param name="a">第一向量。</param><param name="b">第二向量。</param>
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    /// <summary>保留原三角面中实际露出的多边形，顶点仍位于原面上。</summary>
    /// <param name="a">第一点。</param><param name="b">第二点。</param><param name="c">第三点。</param>
    internal List<Vector3[]> Clip(Vector3 a, Vector3 b, Vector3 c) => Cut(new[] { a, b, c });

    /// <summary>逐块扣除真实遮挡；每次只沿原面的边插值，不挪动接触表面。</summary>
    /// <param name="source">原面上的多边形。</param>
    private List<Vector3[]> Cut(Vector3[] source)
    {
        var visible = new List<Vector3[]> { source };
        for (int mask = 0; mask < polygons.Length && visible.Count != 0; mask++)
        {
            var next = new List<Vector3[]>();
            foreach (Vector3[] polygon in visible)
            {
                Vector2 min = Project(polygon[0]), max = min;
                foreach (Vector3 point in polygon)
                {
                    min = Vector2.Min(min, Project(point));
                    max = Vector2.Max(max, Project(point));
                }

                Rect bound = bounds[mask];
                if (max.x < bound.xMin || min.x > bound.xMax || max.y < bound.yMin || min.y > bound.yMax)
                {
                    next.Add(polygon);
                    continue;
                }

                Vector3[] remainder = polygon;
                Vector2[] cover = polygons[mask];
                for (int edge = 0; edge < cover.Length && remainder.Length != 0; edge++)
                {
                    Vector2 a = cover[edge], b = cover[(edge + 1) % cover.Length];
                    Vector3[] outside = Half(remainder, a, b, false);
                    if (HasArea(outside)) next.Add(outside);
                    remainder = Half(remainder, a, b, true);
                }
            }

            visible = next;
        }

        return visible;
    }

    /// <summary>裁到一条投影边的内侧或外侧，仍在原三维面内插值。</summary>
    /// <param name="source">当前多边形。</param><param name="a">遮挡边起点。</param><param name="b">遮挡边终点。</param>
    /// <param name="inside">是否保留内侧。</param><param name="inset">内侧进一步收进的距离。</param>
    private static Vector3[] Half(Vector3[] source, Vector2 a, Vector2 b, bool inside, float inset = 0f)
    {
        var result = new List<Vector3>(source.Length + 1);
        Vector2 edge = b - a;
        float threshold = inset * edge.magnitude;
        for (int i = 0; i < source.Length; i++)
        {
            Vector3 p = source[(i + source.Length - 1) % source.Length], q = source[i];
            float dp = Cross(edge, Project(p) - a) - threshold, dq = Cross(edge, Project(q) - a) - threshold;
            bool pIn = inside ? dp >= 0f : dp < 0f, qIn = inside ? dq >= 0f : dq < 0f;
            if (pIn != qIn) result.Add(Vector3.LerpUnclamped(p, q, dp / (dp - dq)));
            if (qIn) result.Add(q);
        }

        return result.ToArray();
    }

    /// <summary>真实三维面有面积时才保留，投影成线的侧面仍可参与接触。</summary>
    /// <param name="polygon">裁切后的原面。</param>
    private static bool HasArea(Vector3[] polygon)
    {
        for (int i = 1; i + 1 < polygon.Length; i++)
            if (Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]).sqrMagnitude > 1e-20f)
                return true;
        return false;
    }

    /// <summary>同一步的遮挡也沿共同端点插值，不另推进身体或读取相机。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param><param name="at">共同进度。</param>
    internal static BodySurfaceOcclusion Lerp(BodySurfaceOcclusion from, BodySurfaceOcclusion to, float at)
    {
        if (ReferenceEquals(from, to) || at <= 0f) return from;
        if (at >= 1f) return to;
        var result = new Vector2[to.originals.Length][];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new Vector2[to.originals[i].Length];
            for (int j = 0; j < result[i].Length; j++)
                result[i][j] = Vector2.Lerp(from.originals[i][j], to.originals[i][j], at);
        }

        return new BodySurfaceOcclusion(result);
    }

    /// <summary>判断是否只是整体平移，用于精确复用同一遮挡裁切。</summary>
    /// <param name="other">后态。</param><param name="shift">共同投影平移。</param>
    private bool TranslationTo(BodySurfaceOcclusion other, out Vector2 shift)
    {
        shift = Vector2.zero;
        if (originals.Length != other.originals.Length) return false;
        if (originals.Length == 0) return true;
        shift = other.originals[0][0] - originals[0][0];
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i].Length != other.originals[i].Length) return false;
            for (int j = 0; j < originals[i].Length; j++)
                if (!(other.originals[i][j] - originals[i][j]).Equals(shift))
                    return false;
        }

        return true;
    }

    /// <summary>同一区间中遮挡轮廓的最大投影位移。</summary>
    /// <param name="other">区间末态。</param>
    private float MotionTo(BodySurfaceOcclusion other)
    {
        float distance = 0f;
        for (int i = 0; i < originals.Length; i++)
        for (int j = 0; j < originals[i].Length; j++)
            distance = Mathf.Max(distance, Vector2.Distance(originals[i][j], other.originals[i][j]));
        return distance;
    }

    /// <summary>取得整个区间始终被盖住的保守内部；向内收进最大顶点位移，不将将来露出的地方误删。</summary>
    /// <param name="other">区间末态。</param>
    private BodySurfaceOcclusion GuaranteedUntil(BodySurfaceOcclusion other)
    {
        float distance = MotionTo(other);
        if (distance == 0f) return this;
        var result = new List<Vector2[]>();
        foreach (Vector2[] polygon in polygons)
        {
            var inside = Array.ConvertAll(polygon, p => new Vector3(p.x, 0f, p.y));
            for (int edge = 0; edge < polygon.Length && inside.Length != 0; edge++)
                inside = Half(inside, polygon[edge], polygon[(edge + 1) % polygon.Length], true, distance);
            if (HasArea(inside)) result.Add(Array.ConvertAll(inside, Project));
        }

        return new BodySurfaceOcclusion(result.ToArray());
    }

    /// <summary>包围体中可能露出的范围；只用于排除，不当作真实接触面。</summary>
    /// <param name="box">整个区间的原表面范围。</param><param name="visible">仍可能露出的范围。</param>
    private bool VisibleBounds(Bounds box, out Bounds visible)
    {
        Vector3 min = box.min, max = box.max;
        visible = default;
        bool found = false;
        for (int axis = 0; axis < 3; axis++)
        for (int side = 0; side < 2; side++)
        {
            var face = new Vector3[4];
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 point = min;
                point[axis] = side == 0 ? min[axis] : max[axis];
                point[(axis + 1) % 3] = (corner is 1 or 2) ? max[(axis + 1) % 3] : min[(axis + 1) % 3];
                point[(axis + 2) % 3] = corner >= 2 ? max[(axis + 2) % 3] : min[(axis + 2) % 3];
                face[corner] = point;
            }

            foreach (Vector3[] polygon in Cut(face))
            foreach (Vector3 point in polygon)
                if (!found)
                {
                    visible = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else visible.Encapsulate(point);
        }

        return found;
    }

    /// <summary>只排除整个区间都够不到的片段；轴线范围与真实半径分开，避免切线接触退化成大量方盒候选。</summary>
    /// <param name="from">作用前态。</param><param name="to">作用后态。</param><param name="target">可能露出的范围。</param>
    private static bool CanReach(AbilityCapsule from, AbilityCapsule to, Bounds target)
    {
        if (to.Prism != null) return AbilityContactIndex.Swept(from, to).Intersects(target);
        Bounds axes = new(from.A, Vector3.zero);
        axes.Encapsulate(from.B);
        axes.Encapsulate(to.A);
        axes.Encapsulate(to.B);
        axes.Expand(Vector3.Max(from.HalfExtents, to.HalfExtents) * 2f);
        Vector3 gap = Vector3.Max(Vector3.zero, Vector3.Max(target.min - axes.max, axes.min - target.max));
        float radius = Mathf.Max(from.Radius, to.Radius) + .00001f;
        return gap.sqrMagnitude <= radius * radius;
    }

    /// <summary>原作用形状在同一步中间的数值，保持球、胶囊和箱体的实际种类。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param><param name="at">共同进度。</param>
    /// <param name="shift">只用于共同参考系的平移。</param>
    private static AbilityCapsule Mix(AbilityCapsule from, AbilityCapsule to, float at, Vector3 shift = default) =>
        from.At(to, at, shift);

    /// <summary>先连续查询原面，再只在可能露出的区间细分；固定点仍属于原三角面。</summary>
    /// <param name="from">遮挡前态。</param><param name="to">遮挡后态。</param>
    /// <param name="sourceFrom">作用前态。</param><param name="sourceTo">作用后态。</param>
    /// <param name="a0">原面前态第一点。</param><param name="b0">第二点。</param><param name="c0">第三点。</param>
    /// <param name="a1">原面后态第一点。</param><param name="b1">第二点。</param><param name="c1">第三点。</param>
    /// <param name="at">最早进度。</param><param name="point">原面上的接触点。</param><param name="normal">接触方向。</param>
    internal static bool Find(BodySurfaceOcclusion from, BodySurfaceOcclusion to,
        AbilityCapsule sourceFrom, AbilityCapsule sourceTo, Vector3 a0, Vector3 b0, Vector3 c0,
        Vector3 a1, Vector3 b1, Vector3 c1, out float at, out Vector3 point, out Vector3 normal)
    {
        float speed = sourceFrom.TravelTo(sourceTo) +
                      Mathf.Max((a1 - a0).magnitude, (b1 - b0).magnitude, (c1 - c0).magnitude) + from.MotionTo(to);
        return Search(0f, 1f, 0, out at, out point, out normal);

        /// <summary>按实际连续接触裁去无效时段，先找较早的一半。</summary>
        /// <param name="left">区间起点。</param><param name="right">区间终点。</param><param name="depth">细分层数。</param>
        /// <param name="hitAt">实际进度。</param><param name="hit">真实面内位置。</param><param name="direction">接触方向。</param>
        bool Search(float left, float right, int depth, out float hitAt, out Vector3 hit, out Vector3 direction)
        {
            hitAt = 0f;
            hit = direction = default;
            var sl = Mix(sourceFrom, sourceTo, left);
            var sr = Mix(sourceFrom, sourceTo, right);
            Vector3 al = Vector3.Lerp(a0, a1, left),
                bl = Vector3.Lerp(b0, b1, left),
                cl = Vector3.Lerp(c0, c1, left),
                ar = Vector3.Lerp(a0, a1, right),
                br = Vector3.Lerp(b0, b1, right),
                cr = Vector3.Lerp(c0, c1, right);
            if (!AbilityTriangleSweep.Find(sl, sr, al, bl, cl, ar, br, cr, out float raw, out _, out _)) return false;
            left = Mathf.Lerp(left, right, raw);
            BodySurfaceOcclusion maskLeft = Lerp(from, to, left), maskRight = Lerp(from, to, right);
            al = Vector3.Lerp(a0, a1, left);
            bl = Vector3.Lerp(b0, b1, left);
            cl = Vector3.Lerp(c0, c1, left);
            sl = Mix(sourceFrom, sourceTo, left);
            if (maskLeft.TranslationTo(maskRight, out Vector2 moved))
            {
                Vector3 shift = new(moved.x, 0f, moved.y);
                ar -= shift;
                br -= shift;
                cr -= shift;
                sr = Mix(sourceFrom, sourceTo, right, shift);
                if (al.Equals(ar) && bl.Equals(br) && cl.Equals(cr))
                {
                    float first = float.PositiveInfinity;
                    foreach (Vector3[] polygon in maskLeft.Clip(al, bl, cl))
                        for (int i = 1; i + 1 < polygon.Length; i++)
                            if (AbilityTriangleSweep.Find(sl, sr, polygon[0], polygon[i], polygon[i + 1],
                                    polygon[0], polygon[i], polygon[i + 1], out float local, out Vector3 p,
                                    out Vector3 n) && local < first)
                            {
                                first = local;
                                hit = p + shift * local;
                                direction = n;
                            }

                    if (!float.IsPositiveInfinity(first))
                    {
                        hitAt = Mathf.Lerp(left, right, first);
                        return true;
                    }
                    // 原连续查询在切边相切时可能用尽推进次数；继续分段核对，不能把未收敛当作落空。
                }

                Bounds swept = new(al, Vector3.zero);
                swept.Encapsulate(bl);
                swept.Encapsulate(cl);
                swept.Encapsulate(ar);
                swept.Encapsulate(br);
                swept.Encapsulate(cr);
                if (!maskLeft.VisibleBounds(swept, out Bounds possible)) return false;
                possible.Expand(.00002f);
                if (!CanReach(sl, sr, possible)) return false;
            }
            else
            {
                Bounds swept = new(al, Vector3.zero);
                swept.Encapsulate(bl);
                swept.Encapsulate(cl);
                swept.Encapsulate(ar);
                swept.Encapsulate(br);
                swept.Encapsulate(cr);
                if (!maskLeft.GuaranteedUntil(maskRight).VisibleBounds(swept, out Bounds possible)) return false;
                possible.Expand(.00002f);
                if (!CanReach(sl, sr, possible)) return false;
            }

            if (Instant(left, out hit, out direction))
            {
                hitAt = left;
                return true;
            }

            if (left >= right) return false;
            if (depth >= 24 || speed * (right - left) <= .0000025f)
            {
                for (int sample = 1; sample <= 4; sample++)
                {
                    float time = Mathf.Lerp(left, right, sample * .25f);
                    if (Instant(time, out hit, out direction))
                    {
                        hitAt = time;
                        return true;
                    }
                }

                return false;
            }

            float middle = (left + right) * .5f;
            return Search(left, middle, depth + 1, out hitAt, out hit, out direction) ||
                   Search(middle, right, depth + 1, out hitAt, out hit, out direction);
        }

        /// <summary>同一时点只接触露出的原面片段。</summary>
        /// <param name="time">共同进度。</param><param name="hit">实际面内点。</param><param name="direction">接触方向。</param>
        bool Instant(float time, out Vector3 hit, out Vector3 direction)
        {
            AbilityCapsule shape = Mix(sourceFrom, sourceTo, time);
            foreach (Vector3[] polygon in Lerp(from, to, time).Clip(Vector3.Lerp(a0, a1, time),
                         Vector3.Lerp(b0, b1, time), Vector3.Lerp(c0, c1, time)))
                for (int i = 1; i + 1 < polygon.Length; i++)
                    if (AbilityTriangleSweep.Find(shape, shape, polygon[0], polygon[i], polygon[i + 1],
                            polygon[0], polygon[i], polygon[i + 1], out _, out hit, out direction))
                        return true;
            hit = direction = default;
            return false;
        }
    }
}
