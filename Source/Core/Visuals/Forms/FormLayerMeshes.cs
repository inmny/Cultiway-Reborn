using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>两种视角复用相同动态网格；同一发布帧只上传一次。</summary>
internal static partial class FormLayerMeshes
{
    /// <summary>同一过程复用网格，弱引用不延长已结束过程的寿命。</summary>
    private static readonly List<Entry> entries = new();
    /// <summary>主线程串行复用的上传缓冲。</summary>
    private static readonly List<Vector3> positions = new(), normals = new();
    /// <summary>表面坐标及厚度、泡沫。</summary>
    private static readonly List<Vector2> uv = new(), surface = new();
    /// <summary>有限水层的横向速度、干湿阈值与前沿泡沫。</summary>
    private static readonly List<Vector4> currents = new();
    /// <summary>相邻两行格中心复用，避免为每个三角形重复取样。</summary>
    private static Vertex[] lower = Array.Empty<Vertex>(), upper = Array.Empty<Vertex>();
    /// <summary>已经去掉干燥处的三角形。</summary>
    private static readonly List<int> indices = new();
    /// <summary>裁切缓冲复用，不为每个三角形分配小数组和列表。</summary>
    private static readonly Vertex[] source = new Vertex[8], clipped = new Vertex[8];

    /// <summary>取得这一帧的实际曲面，不重新计算流动。</summary>
    /// <param name="frame">已冻结水深。</param>
    internal static Mesh Get(FormLayerFrame frame)
    {
        Entry entry = null;
        foreach (Entry candidate in entries) if (candidate.Owner.TryGetTarget(out object owner) && ReferenceEquals(owner, frame.Owner)) { entry = candidate; break; }
        if (entry == null)
        {
            entry = new Entry { Owner = new WeakReference<object>(frame.Owner), Mesh = new Mesh { name = "Cultiway.FlowSurface", indexFormat = IndexFormat.UInt32 } };
            entry.Mesh.MarkDynamic(); entries.Add(entry);
        }
        if (entry.Frame != null && entry.Frame.TryGetTarget(out FormLayerFrame current) && ReferenceEquals(current, frame)) return entry.Mesh;
        Build(frame); Mesh mesh = entry.Mesh; mesh.Clear(keepVertexLayout: false); mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(3, surface);
        if (currents.Count != 0) mesh.SetUVs(5, currents);
        mesh.SetTriangles(indices, 0); mesh.bounds = frame.Bounds;
        entry.Frame = new WeakReference<FormLayerFrame>(frame); return mesh;
    }

    /// <summary>在格中心之间生成平滑表面，三角形沿真实干湿阈值裁切。</summary>
    /// <param name="frame">完整发布状态。</param>
    private static void Build(FormLayerFrame frame)
    {
        positions.Clear(); normals.Clear(); uv.Clear(); surface.Clear(); currents.Clear(); indices.Clear();
        if (frame.Columns) { BuildColumns(frame); return; }
        bool finite = !frame.SharedGrid;
        float dry = frame.DryDepth;
        int first = finite ? -1 : 0;
        int samples = frame.Cells - first + 1;
        if (lower.Length < samples)
        {
            lower = new Vertex[samples];
            upper = new Vertex[samples];
        }
        SampleRow(first, lower);
        for (int y = first; y < frame.Cells; y++)
        {
            SampleRow(y + 1, upper);
            for (int i = 0; i < samples - 1; i++)
            {
                Triangle(lower[i], lower[i + 1], upper[i + 1]);
                Triangle(lower[i], upper[i + 1], upper[i]);
            }
            (lower, upper) = (upper, lower);
        }

        void SampleRow(int y, Vertex[] row)
        {
            for (int i = 0; i < samples; i++) row[i] = At(first + i, y);
        }
        Vertex At(int x, int y)
        {
            Vector3 water = frame.WaterAt(x, y);
            float depth = water.x;
            Vector2 velocity = Velocity(water);
            float foam = frame.ConductionAt(x, y);
            Vector4 current = default;
            if (finite)
            {
                float front = 0;
                if (depth > dry)
                {
                    float speed = velocity.magnitude;
                    if (speed > .35f)
                    {
                        // 前方水流变浅或减速才会起沫，整片快速平流不会泛白。
                        float horizontal = Mathf.Abs(velocity.x), vertical = Mathf.Abs(velocity.y);
                        Vector3 next = Vector3.Lerp(frame.WaterAt(x, y + (velocity.y < 0 ? -1 : 1)),
                            frame.WaterAt(x + (velocity.x < 0 ? -1 : 1), y), horizontal / (horizontal + vertical));
                        float shallower = Mathf.Clamp01((depth - next.x) / depth);
                        float slower = Mathf.Clamp01(1f - Vector2.Dot(velocity, Velocity(next)) / (speed * speed));
                        front = Mathf.Clamp01((speed - .35f) * .37f) * (.55f * shallower + .45f * slower) *
                                Mathf.Clamp01((depth - 2f * dry) / (8f * dry));
                    }
                    current = new Vector4(velocity.x, velocity.y, dry, front);
                }
                else current.z = dry;
            }
            else foam = Mathf.Max(foam, Mathf.Clamp01(velocity.magnitude * .045f));
            Vector3 normal = new Vector3(-(Top(x + 1, y) - Top(x - 1, y)) / (2f * frame.Cell),
                -(Top(x, y + 1) - Top(x, y - 1)) / (2f * frame.Cell), 1f).normalized;
            return new Vertex
            {
                Position = new Vector3(frame.Origin.x + (x + .5f) * frame.Cell, frame.Origin.y + (y + .5f) * frame.Cell,
                    frame.GroundAt(x, y) + depth),
                Normal = normal, Depth = depth, Foam = foam, Current = current, Cell = new Vector2Int(x, y)
            };
        }
        Vector2 Velocity(Vector3 water) => water.x > (finite ? dry : 0)
            ? new Vector2(water.y, water.z) / Mathf.Max(water.x, .00001f)
            : Vector2.zero;
        float Top(int x, int y) => frame.GroundAt(x, y) + frame.WaterAt(x, y).x;
        void Triangle(Vertex a, Vertex b, Vertex c)
        {
            // 三条边分别裁切，最多形成四边形；边缘位置与水深共同插值。
            if (Mathf.Max(a.Depth, Mathf.Max(b.Depth, c.Depth)) < frame.DryDepth) return;
            a.Weights = Vector3.right;
            b.Weights = Vector3.up;
            c.Weights = Vector3.forward;
            source[0] = a; source[1] = b; source[2] = c;
            Vertex[] input = source, output = clipped;
            int count = Clip(input, 3, output,
                new Vector3(a.Depth - frame.DryDepth, b.Depth - frame.DryDepth, c.Depth - frame.DryDepth));
            var swap = input;
            input = output;
            output = swap;
            if (frame.AppearanceIndex >= 0)
            {
                int own = frame.AppearanceIndex;
                Vector3 ours = new(frame.MaskAt(own, a.Cell.x, a.Cell.y), frame.MaskAt(own, b.Cell.x, b.Cell.y),
                    frame.MaskAt(own, c.Cell.x, c.Cell.y));
                if (ours == Vector3.zero) return;
            for (int look = 0; look < frame.AppearanceMasks.Length && count >= 3; look++)
            {
                    if (look == own) continue;
                    Vector3 theirs = new(frame.MaskAt(look, a.Cell.x, a.Cell.y), frame.MaskAt(look, b.Cell.x, b.Cell.y),
                        frame.MaskAt(look, c.Cell.x, c.Cell.y));
                if (theirs == Vector3.zero) continue;
                    count = Clip(input, count, output, ours - theirs);
                    swap = input;
                    input = output;
                    output = swap;
                }
            }
            if (count < 3) return;
            int start = positions.Count;
            for (int i = 0; i < count; i++)
            {
                Vertex vertex = input[i];
                positions.Add(vertex.Position);
                normals.Add(vertex.Normal);
                uv.Add((Vector2)vertex.Position * .3f);
                surface.Add(new Vector2(vertex.Depth, vertex.Foam));
                if (finite) currents.Add(vertex.Current);
            }
            for (int i = 1; i < count - 1; i++) { indices.Add(start); indices.Add(start + i + 1); indices.Add(start + i); }
        }
    }

    /// <summary>对线性半平面裁切，水深和外观使用同一套原三角形权重。</summary>
    /// <param name="input">当前多边形。</param><param name="count">顶点数。</param><param name="output">复用输出。</param><param name="plane">原三角形三点的有符号距离。</param>
    private static int Clip(Vertex[] input, int count, Vertex[] output, Vector3 plane)
    {
        int written = 0;
        for (int i = 0; i < count; i++)
        {
            Vertex a = input[i], b = input[(i + 1) % count];
            float da = Vector3.Dot(a.Weights, plane), db = Vector3.Dot(b.Weights, plane);
            bool left = da >= 0, right = db >= 0;
            if (left) output[written++] = a;
            if (left != right) output[written++] = Vertex.Lerp(a, b, da / (da - db));
        }

        return written;
    }

    /// <summary>结束或换世界后在主线程释放已无人使用的原生网格。</summary>
    /// <param name="all">换世界时释放全部。</param>
    internal static void Collect(bool all = false)
    {
        for (int i = entries.Count - 1; i >= 0; i--) if (all || !entries[i].Owner.TryGetTarget(out _))
        { UnityEngine.Object.Destroy(entries[i].Mesh); entries.RemoveAt(i); }
    }

    /// <summary>只保存显示缓冲及弱引用。</summary>
    private sealed class Entry
    {
        /// <summary>实际过程标识。</summary>
        internal WeakReference<object> Owner;
        /// <summary>当前网格对应的只读帧。</summary>
        internal WeakReference<FormLayerFrame> Frame;
        /// <summary>主线程拥有的可复用网格。</summary>
        internal Mesh Mesh;
    }

    /// <summary>一次边缘裁切需要的数值。</summary>
    private struct Vertex
    {
        /// <summary>绝对地图位置。</summary>
        internal Vector3 Position;
        /// <summary>由同一水面梯度得到的平滑法向。</summary>
        internal Vector3 Normal;

        /// <summary>原三角形中的位置，用于多种外观的共同边界。</summary>
        internal Vector3 Weights;

        /// <summary>原格坐标，只在开始裁切前读取。</summary>
        internal Vector2Int Cell;

        /// <summary>实际水深及同一流动产生的泡沫比例。</summary>
        internal float Depth, Foam;

        /// <summary>有限水流的显示取样，与水深沿同一边插值。</summary>
        internal Vector4 Current;
        /// <summary>干湿边界按共同水深线性插值。</summary>
        /// <param name="a">边起点。</param><param name="b">边终点。</param><param name="t">边上进度。</param>
        internal static Vertex Lerp(Vertex a, Vertex b, float t) => new() {
            Weights = Vector3.Lerp(a.Weights, b.Weights, t), Position = Vector3.Lerp(a.Position, b.Position, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized, Depth = Mathf.Lerp(a.Depth, b.Depth, t), Foam = Mathf.Lerp(a.Foam, b.Foam, t), Current = Vector4.Lerp(a.Current, b.Current, t) };
    }
}
