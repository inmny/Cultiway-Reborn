using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>轴向突刺枪芒构造过程中复用的托管缓冲。</summary>
internal sealed class ThrustSheetMeshBuffer
{
    internal readonly List<Vector3> Vertices = new(16);
    internal readonly List<Vector2> Uv = new(16);
    internal readonly List<Color> Colors = new(16);
    internal readonly List<int> Triangles = new(42);
}

/// <summary>把来源中心与武器端点连接成中段饱满、两端收尖的连续枪芒。</summary>
internal static class ThrustSheetMesh
{
    private static readonly float[] SectionPositions = { 0f, 0.1f, 0.24f, 0.44f, 0.66f, 0.84f, 0.95f, 1f };
    private static readonly float[] SectionWidths = { 0.08f, 0.62f, 1f, 0.94f, 0.78f, 0.52f, 0.23f, 0.01f };

    /// <summary>按当前来源与端点重建枪芒，UV.x 表示根部到尖端，UV.y 表示横向两侧。</summary>
    internal static void Build(
        Mesh mesh,
        ThrustSheetMeshBuffer buffer,
        Vector3 sourceOrigin,
        Vector3 weaponPoint,
        float startOffset,
        float tipExtension,
        float width,
        Color color,
        float alpha)
    {
        Vector3 direction = weaponPoint - sourceOrigin;
        float sourceDistance = direction.magnitude;
        if (sourceDistance <= 0.0001f || width <= 0f || alpha <= 0f)
        {
            mesh.Clear();
            return;
        }

        direction /= sourceDistance;
        float clampedStartOffset = Mathf.Min(Mathf.Max(0f, startOffset), sourceDistance * 0.42f);
        Vector3 start = sourceOrigin + direction * clampedStartOffset;
        Vector3 end = weaponPoint + direction * Mathf.Max(0f, tipExtension);
        Vector3 normal = new(-direction.y, direction.x, 0f);

        List<Vector3> vertices = buffer.Vertices;
        List<Vector2> uv = buffer.Uv;
        List<Color> colors = buffer.Colors;
        List<int> triangles = buffer.Triangles;
        vertices.Clear();
        uv.Clear();
        colors.Clear();
        triangles.Clear();

        Color vertexColor = color;
        vertexColor.a = alpha;
        for (var i = 0; i < SectionPositions.Length; i++)
        {
            float along = SectionPositions[i];
            float halfWidth = width * SectionWidths[i] * 0.5f;
            Vector3 center = Vector3.Lerp(start, end, along);
            int vertex = vertices.Count;
            vertices.Add(center - normal * halfWidth);
            vertices.Add(center + normal * halfWidth);
            uv.Add(new Vector2(along, 0f));
            uv.Add(new Vector2(along, 1f));
            colors.Add(vertexColor);
            colors.Add(vertexColor);

            if (i == SectionPositions.Length - 1) continue;
            triangles.Add(vertex);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 2);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 3);
            triangles.Add(vertex + 2);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0, false);
        mesh.RecalculateBounds();
    }
}
