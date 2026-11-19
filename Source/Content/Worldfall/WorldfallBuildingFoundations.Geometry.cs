#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using FirstPerson.Core;
using Tessellation = LibTessDotNet;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallBuildingFoundations
{
    /// <summary>每份正式网格只提取一次真实底面。</summary>
    private static readonly Dictionary<MeshModel, FootingShape> FootingShapes = new();

    /// <summary>底座向下的表面合并后得到的轮廓，保留凹口、孔洞和分开的支座。</summary>
    private sealed class FootingShape
    {
        /// <summary>材质和入口所来自的主体。</summary>
        internal MeshModel Source;
        /// <summary>底面占比最大的材质。</summary>
        internal int Slot;
        /// <summary>三角形顺序排列的地基上表面。</summary>
        internal Vec3[] Top = Array.Empty<Vec3>();
        /// <summary>所有外边界和孔洞边界。</summary>
        internal Vec3[][] Loops = Array.Empty<Vec3[]>();
        /// <summary>实际底座范围的最小坐标。</summary>
        internal Vec3 Min;
        /// <summary>实际底座范围的最大坐标。</summary>
        internal Vec3 Max;
        /// <summary>每个独立门面对应的底座外沿、落脚高度和朝向。</summary>
        internal readonly List<Entrance> Entrances = new();
    }

    /// <summary>一个真实入口；Side为负一时朝正面，为正一时朝背面。</summary>
    private struct Entrance
    {
        /// <summary>对应底座外沿的落脚位置。</summary>
        internal Vec3 Position;
        /// <summary>沿模型纵向离开建筑的方向。</summary>
        internal float Side;
    }

    /// <summary>合并底座的实际投影，再把每个边界点还原到原模型底面高度。</summary>
    /// <param name="model">本次真实绘制的距离档或建筑状态。</param>
    /// <returns>可复用的底形；施工尚无底面时为空。</returns>
    private static FootingShape ShapeOf(MeshModel model)
    {
        if (FootingShapes.TryGetValue(model, out FootingShape shape)) return shape;
        shape = new FootingShape { Source = model, Min = model.Min, Max = model.Max };
        FootingShapes.Add(model, shape);
        List<MeshPart> grounded = new();
        foreach (MeshPart part in model.Parts)
            if (part.TriangleCount > 0 && (part.Name == "base" || part.Name == "foundation" ||
                part.Name.StartsWith("base@", StringComparison.Ordinal))) grounded.Add(part);
        MeshPart[] parts = grounded.Count > 0 ? grounded.ToArray() : model.Parts;
        float floor = float.PositiveInfinity, high = float.NegativeInfinity;
        foreach (MeshPart part in parts)
            for (int i = 2; i < part.Pos.Length; i += 3) { floor = Math.Min(floor, part.Pos[i]); high = Math.Max(high, part.Pos[i]); }
        float half = (floor + high) * 0.5f;
        List<(Vec3 A, Vec3 B, Vec3 C)> lower = new(), upper = new();
        List<(Vec3 A, Vec3 B, Vec3 C, int Slot)> candidates = new();
        List<(float Height, float Area)> levels = new();
        float[] areas = new float[model.SlotNames.Length];
        Tessellation.Tess union = new() { NoEmptyPolygons = true };
        foreach (MeshPart part in parts)
        for (int t = 0; t < part.Tri.Length; t += 3)
        {
            Vec3 a = Point(part, part.Tri[t]), b = Point(part, part.Tri[t + 1]), c = Point(part, part.Tri[t + 2]);
            float area = Vec3.Cross(b - a, c - a).Z;
            if (area > 0.000000001f) upper.Add((a, b, c));
            if (area >= -0.000000001f) continue;
            // 同一部件可能含屋檐或树枝，只沿真正接触地面的最低层补齐。
            Vec3[] clipped = ClipBottom(a, b, c, half);
            for (int v = 1; v + 1 < clipped.Length; v++)
            {
                Vec3 ca = clipped[0], cb = clipped[v], cc = clipped[v + 1];
                float clippedArea = Vec3.Cross(cb - ca, cc - ca).Z;
                if (clippedArea >= -0.000000001f) continue;
                candidates.Add((ca, cb, cc, part.Slot[t / 3]));
                levels.Add(((ca.Z + cb.Z + cc.Z) / 3f, -clippedArea));
            }
        }
        if (candidates.Count == 0) return shape;
        // 底座表面本身可能有碎石起伏，按面积找主要接地层，不把最低一粒碎石当作整块底座。
        levels.Sort((a, b) => a.Height.CompareTo(b.Height));
        float total = 0, accumulated = 0, contact = floor;
        foreach (var level in levels) total += level.Area;
        foreach (var level in levels) { accumulated += level.Area; contact = level.Height; if (accumulated >= total * 0.5f) break; }
        float ceiling = Math.Min(half, contact + Math.Min(0.025f, (model.Max.Z - model.Min.Z) * 0.025f));
        foreach (var face in candidates)
        {
            Vec3[] clipped = ClipBottom(face.A, face.B, face.C, ceiling);
            for (int v = 1; v + 1 < clipped.Length; v++)
            {
                Vec3 a = clipped[0], b = clipped[v], c = clipped[v + 1];
                float area = Vec3.Cross(b - a, c - a).Z;
                if (area >= -0.000000001f) continue;
                lower.Add((a, b, c));
                areas[face.Slot] -= area;
                union.AddContour([Vertex(a), Vertex(c), Vertex(b)], Tessellation.ContourOrientation.CounterClockwise);
            }
        }
        for (int i = 1; i < areas.Length; i++) if (areas[i] > areas[shape.Slot]) shape.Slot = i;
        union.Tessellate(Tessellation.WindingRule.NonZero, Tessellation.ElementType.BoundaryContours,
            3, null, new Tessellation.Vec3(0, 0, 1));
        List<Vec3[]> loops = new();
        Tessellation.Tess cap = new() { NoEmptyPolygons = true };
        shape.Min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
        shape.Max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < union.ElementCount; i++)
        {
            int start = union.Elements[i * 2], count = union.Elements[i * 2 + 1];
            Vec3[] loop = new Vec3[count];
            Tessellation.ContourVertex[] contour = new Tessellation.ContourVertex[count];
            for (int v = 0; v < count; v++)
            {
                Tessellation.Vec3 p = union.Vertices[start + v].Position;
                loop[v] = new Vec3(p.X, p.Y, SurfaceHeight(lower, p.X, p.Y));
                contour[v] = Vertex(loop[v]);
                shape.Min = new Vec3(Math.Min(shape.Min.X, p.X), Math.Min(shape.Min.Y, p.Y), Math.Min(shape.Min.Z, loop[v].Z));
                shape.Max = new Vec3(Math.Max(shape.Max.X, p.X), Math.Max(shape.Max.Y, p.Y), Math.Max(shape.Max.Z, loop[v].Z));
            }
            loop = SimplifyLoop(loop);
            double area = 0;
            for (int v = 0; v < loop.Length; v++)
                area += (double)loop[v].X * loop[(v + 1) % loop.Length].Y - (double)loop[(v + 1) % loop.Length].X * loop[v].Y;
            if (Math.Abs(area) < 0.00000008) continue;
            loops.Add(loop);
            contour = new Tessellation.ContourVertex[loop.Length];
            for (int v = 0; v < loop.Length; v++) contour[v] = Vertex(loop[v]);
            cap.AddContour(contour, Tessellation.ContourOrientation.Original);
        }
        cap.Tessellate(Tessellation.WindingRule.NonZero, Tessellation.ElementType.Polygons,
            3, null, new Tessellation.Vec3(0, 0, 1));
        List<Vec3> top = new();
        for (int i = 0; i < cap.Elements.Length; i += 3)
        {
            Tessellation.Vec3 a = cap.Vertices[cap.Elements[i]].Position,
                b = cap.Vertices[cap.Elements[i + 1]].Position, c = cap.Vertices[cap.Elements[i + 2]].Position;
            if (Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) < 0.0000000001f) continue;
            foreach (Tessellation.Vec3 p in new[] { a, b, c })
                top.Add(new Vec3(p.X, p.Y, SurfaceHeight(lower, p.X, p.Y)));
        }
        shape.Top = top.ToArray();
        shape.Loops = loops.ToArray();
        FindEntrance(shape, upper.Count > 0 ? upper : lower);
        return shape;
    }

    /// <summary>合并万分之一模型单位内的碎边；孔洞和分离支座不合并成包围框。</summary>
    /// <param name="source">按顺序排列的闭合边界。</param><returns>保留形状与上下接合高度的边界。</returns>
    private static Vec3[] SimplifyLoop(Vec3[] source)
    {
        List<Vec3> points = new(source);
        bool changed = true;
        while (changed && points.Count > 3)
        {
            changed = false;
            for (int i = points.Count - 1; i >= 0 && points.Count > 3; i--)
            {
                Vec3 a = points[(i + points.Count - 1) % points.Count], b = points[i], c = points[(i + 1) % points.Count];
                float dx = c.X - a.X, dy = c.Y - a.Y, span = dx * dx + dy * dy;
                if (span == 0) continue;
                float t = Math.Max(0, Math.Min(1, ((b.X - a.X) * dx + (b.Y - a.Y) * dy) / span));
                float ex = b.X - a.X - dx * t, ey = b.Y - a.Y - dy * t;
                if (ex * ex + ey * ey > 0.00000001f || Math.Abs(b.Z - a.Z - (c.Z - a.Z) * t) > 0.0001f) continue;
                points.RemoveAt(i);
                changed = true;
            }
        }
        return points.ToArray();
    }

    /// <summary>把向下表面裁到接地层，保留原模型切面，不把高处悬挑投影成支柱。</summary>
    /// <param name="a">三角形第一点。</param><param name="b">第二点。</param><param name="c">第三点。</param>
    /// <param name="ceiling">接地层的上沿。</param><returns>裁切后按原绕序排列的多边形。</returns>
    private static Vec3[] ClipBottom(Vec3 a, Vec3 b, Vec3 c, float ceiling)
    {
        Vec3[] points = [a, b, c];
        List<Vec3> clipped = new(4);
        for (int i = 0; i < 3; i++)
        {
            Vec3 p = points[i], q = points[(i + 1) % 3];
            bool inside = p.Z <= ceiling, nextInside = q.Z <= ceiling;
            if (inside) clipped.Add(p);
            if (inside != nextInside) clipped.Add(p + (q - p) * ((ceiling - p.Z) / (q.Z - p.Z)));
        }
        return clipped.ToArray();
    }

    /// <summary>读取原始顶点坐标，保留原模型的部件和转轴。</summary>
    /// <param name="part">来源部件。</param><param name="index">顶点序号。</param>
    /// <returns>模型坐标。</returns>
    private static Vec3 Point(MeshPart part, int index) => new(part.Pos[index * 3], part.Pos[index * 3 + 1], part.Pos[index * 3 + 2]);

    /// <summary>平面轮廓计算只使用横向坐标，原有高度另行还原。</summary>
    /// <param name="point">原始坐标。</param><returns>平面轮廓顶点。</returns>
    private static Tessellation.ContourVertex Vertex(Vec3 point) => new(new Tessellation.Vec3(point.X, point.Y, 0));

    /// <summary>在原始表面插值得到高度，合并边界有微小误差时取最近边缘，避免细碎面被外推成尖刺。</summary>
    /// <param name="faces">原模型的同向表面。</param><param name="x">横坐标。</param><param name="y">纵坐标。</param>
    /// <returns>重叠表面的最低高度，避免把上方装饰当作底面。</returns>
    private static float SurfaceHeight(List<(Vec3 A, Vec3 B, Vec3 C)> faces, float x, float y)
    {
        float best = float.PositiveInfinity, height = float.PositiveInfinity;
        foreach (var face in faces)
        {
            Vec3 u = face.B - face.A, v = face.C - face.A;
            float det = u.X * v.Y - u.Y * v.X;
            float a = ((x - face.A.X) * v.Y - (y - face.A.Y) * v.X) / det;
            float b = (u.X * (y - face.A.Y) - u.Y * (x - face.A.X)) / det;
            if (a >= 0 && b >= 0 && a + b <= 1)
            {
                float z = face.A.Z + u.Z * a + v.Z * b;
                if (best > 0) { best = 0; height = z; }
                else height = Math.Min(height, z);
                continue;
            }
            SurfaceEdge(face.A, face.B, x, y, ref best, ref height);
            SurfaceEdge(face.B, face.C, x, y, ref best, ref height);
            SurfaceEdge(face.C, face.A, x, y, ref best, ref height);
        }
        return height;
    }

    /// <summary>按真实水平距离取最近边缘，并沿这条边插值高度。</summary>
    /// <param name="a">边的起点。</param><param name="b">边的终点。</param>
    /// <param name="x">目标横坐标。</param><param name="y">目标纵坐标。</param>
    /// <param name="best">已找到的最小距离平方。</param><param name="height">对应的表面高度。</param>
    private static void SurfaceEdge(Vec3 a, Vec3 b, float x, float y, ref float best, ref float height)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float t = Math.Max(0, Math.Min(1, ((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy)));
        float ex = x - a.X - dx * t, ey = y - a.Y - dy * t;
        float distance = ex * ex + ey * ey, z = a.Z + (b.Z - a.Z) * t;
        if (distance < best - 0.000000000001f) { best = distance; height = z; }
        else if (Math.Abs(distance - best) <= 0.000000000001f) height = Math.Min(height, z);
    }

    /// <summary>沿实际外沿向下延伸，地基上沿逐点与主体相接，不加矩形包边。</summary>
    /// <param name="shape">已合并的真实底形。</param><param name="bottom">埋入最低地面的模型高度。</param>
    /// <returns>本座建筑当前需要的地基网格。</returns>
    private static MeshModel CreateFoundation(FootingShape shape, float bottom)
    {
        MeshBuilder builder = new("cultiway_footing_" + shape.Source.Name);
        int slot = builder.Slot("foundation", shape.Source.SlotKind[shape.Slot], shape.Source.SlotColor[shape.Slot]);
        builder.Part("foundation", -1, new Vec3(0, 0, 0));
        // 上沿由原建筑底面盖住，下沿始终埋在最低地面以下，不重复绘制这两层隐藏封面。
        foreach (Vec3[] loop in shape.Loops)
        for (int i = 0; i < loop.Length; i++)
        {
            Vec3 a = loop[i], b = loop[(i + 1) % loop.Length];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (dx * dx + dy * dy < 0.00000000000001f) continue;
            builder.Face(slot, a, new Vec3(a.X, a.Y, bottom), new Vec3(b.X, b.Y, bottom), b);
        }
        MeshModel result = builder.Build();
        // 原生构面会缩短极细三角面的法线；保留真实方向重新归一，避免底沿出现黑线。
        foreach (MeshPart part in result.Parts)
        for (int i = 0; i < part.Nrm.Length; i += 3)
        {
            double x = part.Nrm[i], y = part.Nrm[i + 1], z = part.Nrm[i + 2];
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            part.Nrm[i] /= length; part.Nrm[i + 1] /= length; part.Nrm[i + 2] /= length;
        }
        return result;
    }

    /// <summary>从实际门面取横向位置，并在同一位置找到底座前沿和可落脚的表面。</summary>
    /// <param name="shape">来源模型与底部轮廓。</param><param name="upper">底座朝上的实际表面。</param>
    private static void FindEntrance(FootingShape shape, List<(Vec3 A, Vec3 B, Vec3 C)> upper)
    {
        MeshModel model = shape.Source;
        if (model.Name.StartsWith("site_", StringComparison.Ordinal)) return;
        int doorPart = model.PartIndex("door"), doorSlot = model.SlotIndex("door");
        MeshPart doorMesh = doorPart >= 0 && model.Parts[doorPart].TriangleCount > 0 ? model.Parts[doorPart] : null;
        List<(float Left, float Right, float Front, float Back, float Bottom, float Top)> doors = new();
        float middle = model.Min.Y + model.Max.Y;
        foreach (MeshPart part in model.Parts)
        for (int t = 0; t < part.TriangleCount; t++)
        {
            // 独立门部件包含门框和门扉；墙窗也可能借用深色门槽，不能把窗洞当入口。
            if (doorMesh != null ? part != doorMesh : part.Slot[t] != doorSlot) continue;
            Vec3 a = Point(part, part.Tri[t * 3]), b = Point(part, part.Tri[t * 3 + 1]), c = Point(part, part.Tri[t * 3 + 2]);
            float left = Math.Min(a.X, Math.Min(b.X, c.X)), right = Math.Max(a.X, Math.Max(b.X, c.X));
            float near = Math.Min(a.Y, Math.Min(b.Y, c.Y)), far = Math.Max(a.Y, Math.Max(b.Y, c.Y));
            float bottom = Math.Min(a.Z, Math.Min(b.Z, c.Z)), top = Math.Max(a.Z, Math.Max(b.Z, c.Z));
            // 相邻门扉合成一个入口，分开的公寓门和双塔门各自保留。
            for (int i = 0; i < doors.Count; i++)
            {
                var other = doors[i];
                if (left > other.Right + 0.01f || right < other.Left - 0.01f ||
                    bottom > other.Top + 0.01f || top < other.Bottom - 0.01f) continue;
                // 外框和门扉可以有凹深间距，但前后的两扇门不能因此合并。
                if (doorMesh != null ? (near + far > middle) != (other.Front + other.Back > middle) :
                    near > other.Back + 0.01f || far < other.Front - 0.01f) continue;
                left = Math.Min(left, other.Left); right = Math.Max(right, other.Right);
                near = Math.Min(near, other.Front); far = Math.Max(far, other.Back);
                bottom = Math.Min(bottom, other.Bottom); top = Math.Max(top, other.Top);
                doors.RemoveAt(i);
                i = -1;
            }
            doors.Add((left, right, near, far, bottom, top));
        }
        if (doorMesh != null && doors.Count > 0)
        {
            // 一些门部件同时存放高处窗框；只接最低一层真正落地的入口。
            float bottom = float.PositiveInfinity;
            foreach (var door in doors) bottom = Math.Min(bottom, door.Bottom);
            doors.RemoveAll(door => door.Bottom > bottom + 0.02f);
        }
        if (doors.Count == 0)
        {
            if (!(doorPart >= 0 && !model.Name.StartsWith("ruin_", StringComparison.Ordinal) ||
                  model.Info("kind") == "tree" || model.PartIndex("trunk") >= 0)) return;
            float x = doorPart >= 0 ? model.Parts[doorPart].Pivot.X : (shape.Min.X + shape.Max.X) * 0.5f;
            doors.Add((x, x, shape.Min.Y, shape.Min.Y, 0, 0));
        }
        doors.Sort((a, b) => a.Left.CompareTo(b.Left));
        foreach (var door in doors)
        {
            float x = Math.Max(model.Min.X + 0.0001f, Math.Min(model.Max.X - 0.0001f, (door.Left + door.Right) * 0.5f));
            bool back = door.Front + door.Back > middle;
            float side = back ? 1f : -1f, edge = back ? float.NegativeInfinity : float.PositiveInfinity;
            float centerY = (door.Front + door.Back) * 0.5f;
            int crossings = 0;
            foreach (Vec3[] loop in shape.Loops)
            for (int i = 0; i < loop.Length; i++)
            {
                Vec3 a = loop[i], b = loop[(i + 1) % loop.Length];
                if ((a.X > x) == (b.X > x)) continue;
                float y = a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                if (y > centerY) crossings++;
                edge = back ? Math.Max(edge, y) : Math.Min(edge, y);
            }
            // 草屋的base部件可能只有门前石块和水桶；门不在这些小底座上时直接接真实门槛。
            bool onBase = crossings % 2 != 0;
            if (!onBase) edge = back ? door.Back : door.Front;
            edge -= side * 0.002f;
            float height = onBase ? SurfaceHeight(upper, x, edge) : door.Bottom;
            shape.Entrances.Add(new Entrance { Side = side, Position = new Vec3(x, edge, height) });
        }
    }
}
#endif
