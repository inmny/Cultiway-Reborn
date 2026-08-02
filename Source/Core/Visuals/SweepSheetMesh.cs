using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>径向扫掠扇面构造过程中复用的托管缓冲。</summary>
internal sealed class SweepSheetMeshBuffer
{
    internal readonly List<Vector3> EdgePoints = new(64);
    internal readonly List<Vector3> Origins = new(64);
    internal readonly List<Vector3> Vertices = new(128);
    internal readonly List<Vector2> Uv = new(128);
    internal readonly List<Color> Colors = new(128);
    internal readonly List<int> Triangles = new(384);
    internal readonly List<float> Distances = new(64);
}

/// <summary>把运动点及其来源中心转换为可供扫掠 Shader 着色的连续扇面。</summary>
internal static class SweepSheetMesh
{
    private const float DegreesPerSubdivision = 6f;

    /// <summary>重建目标扇面网格，UV.x 表示时间先后，UV.y 表示内缘到外缘。</summary>
    internal static void Build(
        Mesh mesh,
        SweepSheetMeshBuffer buffer,
        IReadOnlyList<Vector3> sourcePoints,
        IReadOnlyList<Vector3> sourceOrigins,
        float innerRadiusRatio,
        float outerExtension,
        Color color,
        float alpha)
    {
        PrepareSamples(sourcePoints, sourceOrigins, buffer.EdgePoints, buffer.Origins);
        List<Vector3> edgePoints = buffer.EdgePoints;
        List<Vector3> origins = buffer.Origins;
        if (edgePoints.Count < 2 || edgePoints.Count != origins.Count || alpha <= 0f)
        {
            mesh.Clear();
            return;
        }

        List<Vector3> vertices = buffer.Vertices;
        List<Vector2> uv = buffer.Uv;
        List<Color> colors = buffer.Colors;
        List<int> triangles = buffer.Triangles;
        List<float> distances = buffer.Distances;
        vertices.Clear();
        uv.Clear();
        colors.Clear();
        triangles.Clear();
        distances.Clear();
        distances.Add(0f);
        for (var i = 1; i < edgePoints.Count; i++)
        {
            distances.Add(distances[i - 1] + Vector3.Distance(edgePoints[i - 1], edgePoints[i]));
        }

        float totalDistance = Mathf.Max(0.001f, distances[^1]);
        float innerRatio = Mathf.Clamp(innerRadiusRatio, 0.05f, 0.92f);
        float extension = Mathf.Max(0f, outerExtension);
        for (var i = 0; i < edgePoints.Count; i++)
        {
            Vector3 radial = edgePoints[i] - origins[i];
            float radius = radial.magnitude;
            if (radius <= 0.0001f) radial = ResolveFallbackDirection(edgePoints, origins, i);
            else radial /= radius;
            radius = Mathf.Max(radius, 0.05f);

            Vector3 inner = origins[i] + radial * (radius * innerRatio);
            Vector3 outer = origins[i] + radial * (radius + extension);
            int vertex = vertices.Count;
            vertices.Add(inner);
            vertices.Add(outer);
            float along = distances[i] / totalDistance;
            uv.Add(new Vector2(along, 0f));
            uv.Add(new Vector2(along, 1f));
            Color vertexColor = color;
            vertexColor.a = alpha;
            colors.Add(vertexColor);
            colors.Add(vertexColor);

            if (i == edgePoints.Count - 1) continue;
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

    /// <summary>按相邻径向夹角补点，使低帧率下的扇面外缘仍保持圆滑。</summary>
    private static void PrepareSamples(
        IReadOnlyList<Vector3> sourcePoints,
        IReadOnlyList<Vector3> sourceOrigins,
        ICollection<Vector3> resultPoints,
        ICollection<Vector3> resultOrigins)
    {
        resultPoints.Clear();
        resultOrigins.Clear();
        if (sourcePoints.Count == 0 || sourcePoints.Count != sourceOrigins.Count) return;
        resultPoints.Add(sourcePoints[0]);
        resultOrigins.Add(sourceOrigins[0]);
        for (var i = 0; i < sourcePoints.Count - 1; i++)
        {
            Vector3 originA = sourceOrigins[i];
            Vector3 originB = sourceOrigins[i + 1];
            Vector3 radialA = sourcePoints[i] - originA;
            Vector3 radialB = sourcePoints[i + 1] - originB;
            float radiusA = radialA.magnitude;
            float radiusB = radialB.magnitude;
            Vector3 directionA = radiusA > 0.0001f ? radialA / radiusA : Vector3.right;
            Vector3 directionB = radiusB > 0.0001f ? radialB / radiusB : directionA;
            int subdivisions = Mathf.Clamp(
                Mathf.CeilToInt(Vector3.Angle(directionA, directionB) / DegreesPerSubdivision),
                1,
                12);
            for (var step = 1; step <= subdivisions; step++)
            {
                float t = step / (float)subdivisions;
                Vector3 origin = Vector3.Lerp(originA, originB, t);
                Vector3 direction = Vector3.Slerp(directionA, directionB, t).normalized;
                float radius = Mathf.Lerp(radiusA, radiusB, t);
                resultOrigins.Add(origin);
                resultPoints.Add(origin + direction * radius);
            }
        }
    }

    /// <summary>在零半径异常采样处沿相邻运动方向给出稳定径向。</summary>
    private static Vector3 ResolveFallbackDirection(
        IReadOnlyList<Vector3> points,
        IReadOnlyList<Vector3> origins,
        int index)
    {
        if (index > 0)
        {
            Vector3 previous = points[index - 1] - origins[index - 1];
            if (previous.sqrMagnitude > 0.0001f) return previous.normalized;
        }
        if (index + 1 < points.Count)
        {
            Vector3 next = points[index + 1] - origins[index + 1];
            if (next.sqrMagnitude > 0.0001f) return next.normalized;
        }
        return Vector3.right;
    }
}
