using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal static partial class FormLayerMeshes
{
    /// <summary>显示与接触读取同一格厚，跳过干格以及相邻柱遮住的侧面。</summary>
    /// <param name="frame">不可变真实波面。</param>
    private static void BuildColumns(FormLayerFrame frame)
    {
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

    /// <summary>加入一个真实表面，材质坐标和法线沿普通动态网格入口上传。</summary>
    /// <param name="a">第一角。</param><param name="b">第二角。</param><param name="c">第三角。</param>
    /// <param name="d">第四角。</param><param name="normal">外法线。</param><param name="depth">实际厚度。</param>
    private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float depth)
    {
        int first = positions.Count;
        Vertex(a); Vertex(b); Vertex(c); Vertex(d);
        void Vertex(Vector3 p)
        { positions.Add(p); normals.Add(normal); uv.Add((Vector2)p * .3f); surface.Add(new Vector2(depth, 0f)); }
        indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
        indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
    }
}
