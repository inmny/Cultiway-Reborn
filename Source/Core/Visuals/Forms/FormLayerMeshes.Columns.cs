using System;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal static partial class FormLayerMeshes
{
    /// <summary>显示与接触读取同一格厚，跳过干格以及相邻柱遮住的侧面。</summary>
    /// <param name="frame">不可变真实波面。</param>
    private static void BuildColumns(FormLayerFrame frame)
    {
        if (frame.SharedGrid)
        {
            BuildWaveSurface(frame);
            return;
        }

        for (int y = 0; y < frame.Cells; y++)
        for (int x = 0; x < frame.Cells; x++)
        {
            float depth = frame.Depth(x, y);
            if (depth <= frame.DryDepth) continue;
            float left = frame.Origin.x + x, bottom = frame.Origin.y + y;
            float floor = frame.GroundAt(x, y), top = floor + depth;
            Quad(new Vector3(left, bottom, top), new Vector3(left + 1f, bottom, top),
                new Vector3(left + 1f, bottom + 1f, top), new Vector3(left, bottom + 1f, top), Vector3.forward, depth);
            Side(x - 1, y, new Vector3(left, bottom + 1f, 0), new Vector3(left, bottom, 0), Vector3.left);
            Side(x + 1, y, new Vector3(left + 1f, bottom, 0), new Vector3(left + 1f, bottom + 1f, 0), Vector3.right);
            Side(x, y - 1, new Vector3(left, bottom, 0), new Vector3(left + 1f, bottom, 0), Vector3.down);
            Side(x, y + 1, new Vector3(left + 1f, bottom + 1f, 0), new Vector3(left, bottom + 1f, 0), Vector3.up);

            void Side(int nx, int ny, Vector3 a, Vector3 b, Vector3 normal)
            {
                float neighbour = frame.Depth(Mathf.Clamp(nx, 0, frame.Cells - 1), Mathf.Clamp(ny, 0, frame.Cells - 1));
                bool inside = nx >= 0 && ny >= 0 && nx < frame.Cells && ny < frame.Cells;
                float low = inside && neighbour > frame.DryDepth ? Mathf.Max(floor, frame.GroundAt(nx, ny) + neighbour) : floor;
                if (low >= top) return;
                Quad(a + Vector3.forward * low, b + Vector3.forward * low,
                    b + Vector3.forward * top, a + Vector3.forward * top, normal, depth);
            }
        }
    }

    private static Vertex[] waveCorners = Array.Empty<Vertex>();

    /// <summary>只在已到达格内相接波面，保留格中心的实际高度及干格边界。</summary>
    private static void BuildWaveSurface(FormLayerFrame frame)
    {
        int stride = frame.Cells + 1;
        if (waveCorners.Length < stride * stride) waveCorners = new Vertex[stride * stride];
        for (int y = 0; y < stride; y++)
        for (int x = 0; x < stride; x++)
        {
            float southwest = frame.WaterAt(x - 1, y - 1).x;
            float southeast = frame.WaterAt(x, y - 1).x;
            float northwest = frame.WaterAt(x - 1, y).x;
            float northeast = frame.WaterAt(x, y).x;
            float bottom = frame.GroundAt(x - 1, y - 1), rightBottom = frame.GroundAt(x, y - 1);
            float leftTop = frame.GroundAt(x - 1, y), top = frame.GroundAt(x, y);
            float depth = (southwest + southeast + northwest + northeast) * .25f;
            float bed = (bottom + rightBottom + leftTop + top) * .25f;
            float west = bottom + southwest + leftTop + northwest;
            float east = rightBottom + southeast + top + northeast;
            float south = bottom + southwest + rightBottom + southeast;
            float north = leftTop + northwest + top + northeast;
            waveCorners[x + y * stride] = new Vertex
            {
                Position = new Vector3(frame.Origin.x + x, frame.Origin.y + y, bed + depth),
                Normal = new Vector3((west - east) * .5f, (south - north) * .5f, 1f).normalized,
                Depth = depth
            };
        }

        for (int y = 0; y < frame.Cells; y++)
        for (int x = 0; x < frame.Cells; x++)
        {
            float depth = frame.Depth(x, y);
            if (depth <= frame.DryDepth) continue;
            int at = x + y * stride;
            var southwest = waveCorners[at];
            var southeast = waveCorners[at + 1];
            var northeast = waveCorners[at + stride + 1];
            var northwest = waveCorners[at + stride];
            Vector3 normal = (southwest.Normal + southeast.Normal + northeast.Normal + northwest.Normal).normalized;
            int center = positions.Count;
            ColumnVertex(new Vector3(frame.Origin.x + x + .5f, frame.Origin.y + y + .5f,
                frame.GroundAt(x, y) + depth), normal, depth);
            ColumnVertex(southwest.Position, southwest.Normal, southwest.Depth);
            ColumnVertex(southeast.Position, southeast.Normal, southeast.Depth);
            ColumnVertex(northeast.Position, northeast.Normal, northeast.Depth);
            ColumnVertex(northwest.Position, northwest.Normal, northwest.Depth);
            for (int side = 0; side < 4; side++)
            {
                indices.Add(center);
                indices.Add(center + 1 + (side + 1) % 4);
                indices.Add(center + 1 + side);
            }

            if (frame.WaterAt(x - 1, y).x <= frame.DryDepth) WaveSide(northwest, southwest, Vector3.left);
            if (frame.WaterAt(x + 1, y).x <= frame.DryDepth) WaveSide(southeast, northeast, Vector3.right);
            if (frame.WaterAt(x, y - 1).x <= frame.DryDepth) WaveSide(southwest, southeast, Vector3.down);
            if (frame.WaterAt(x, y + 1).x <= frame.DryDepth) WaveSide(northeast, northwest, Vector3.up);
        }
    }

    /// <summary>只在真实波前外缘画侧面，相邻的活动格共享表面。</summary>
    private static void WaveSide(Vertex a, Vertex b, Vector3 normal) =>
        Quad(a.Position - a.Depth * Vector3.forward, b.Position - b.Depth * Vector3.forward,
            b.Position, a.Position, normal, (a.Depth + b.Depth) * .5f);

    /// <summary>加入一个真实表面，材质坐标和法线沿普通动态网格入口上传。</summary>
    /// <param name="a">第一角。</param><param name="b">第二角。</param><param name="c">第三角。</param>
    /// <param name="d">第四角。</param><param name="normal">外法线。</param><param name="depth">实际厚度。</param>
    private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float depth)
    {
        int first = positions.Count;
        ColumnVertex(a, normal, depth); ColumnVertex(b, normal, depth);
        ColumnVertex(c, normal, depth); ColumnVertex(d, normal, depth);
        indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
        indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
    }

    private static void ColumnVertex(Vector3 point, Vector3 normal, float depth)
    {
        positions.Add(point);
        normals.Add(normal);
        uv.Add((Vector2)point * .3f);
        surface.Add(new Vector2(depth, 0f));
    }
}
