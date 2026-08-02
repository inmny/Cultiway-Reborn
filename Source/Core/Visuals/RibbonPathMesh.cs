using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>通用路径带网格构造过程中复用的托管缓冲。</summary>
internal sealed class RibbonPathMeshBuffer
{
    internal readonly List<Vector3> Points = new(48);
    internal readonly List<Vector3> Vertices = new(96);
    internal readonly List<Vector2> Uv = new(96);
    internal readonly List<Color> Colors = new(96);
    internal readonly List<int> Triangles = new(288);
    internal readonly List<float> Distances = new(48);
}

/// <summary>路径带的几何宽度与纹理流动参数。</summary>
internal readonly struct RibbonPathStyle
{
    internal readonly float TileLength;
    internal readonly float FlowSpeed;
    internal readonly float StartWidth;
    internal readonly float MiddleWidth;
    internal readonly float EndWidth;
    internal readonly bool Smooth;

    internal RibbonPathStyle(
        float tileLength,
        float flowSpeed,
        float startWidth,
        float middleWidth,
        float endWidth,
        bool smooth)
    {
        TileLength = tileLength;
        FlowSpeed = flowSpeed;
        StartWidth = startWidth;
        MiddleWidth = middleWidth;
        EndWidth = endWidth;
        Smooth = smooth;
    }
}

/// <summary>把任意世界路径转换为带纹理、宽度曲线和顶点透明度的带状网格。</summary>
internal static class RibbonPathMesh
{
    /// <summary>按照给定样式重建目标网格；路径顺序必须从尾部指向头部。</summary>
    internal static void Build(
        Mesh mesh,
        RibbonPathMeshBuffer buffer,
        IReadOnlyList<Vector3> source,
        float width,
        in RibbonPathStyle style,
        float elapsed,
        Color color,
        float alpha,
        bool trail)
    {
        List<Vector3> points = buffer.Points;
        PreparePoints(source, style.Smooth, points);
        if (points.Count < 2 || width <= 0f || alpha <= 0f)
        {
            mesh.Clear();
            return;
        }

        int count = points.Count;
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
        for (var i = 1; i < count; i++)
            distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1], points[i]));
        float totalDistance = Mathf.Max(distances[^1], 0.001f);
        float tileLength = Mathf.Max(style.TileLength, 0.03f);

        for (var i = 0; i < count; i++)
        {
            Vector3 tangent;
            if (i == 0) tangent = points[1] - points[0];
            else if (i == count - 1) tangent = points[^1] - points[^2];
            else tangent = points[i + 1] - points[i - 1];
            if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.right;
            tangent.Normalize();
            Vector3 normal = new(-tangent.y, tangent.x, 0f);
            float t = distances[i] / totalDistance;
            float widthFactor = t < 0.5f
                ? Mathf.Lerp(style.StartWidth, style.MiddleWidth, t * 2f)
                : Mathf.Lerp(style.MiddleWidth, style.EndWidth, (t - 0.5f) * 2f);
            if (trail) widthFactor *= Mathf.SmoothStep(0.08f, 1f, t);
            float halfWidth = width * Mathf.Max(0.02f, widthFactor) * 0.5f;
            int vertex = vertices.Count;
            vertices.Add(points[i] - normal * halfWidth);
            vertices.Add(points[i] + normal * halfWidth);
            float u = distances[i] / tileLength - elapsed * style.FlowSpeed;
            uv.Add(new Vector2(u, 0f));
            uv.Add(new Vector2(u, 1f));
            float ageAlpha = trail
                ? Mathf.SmoothStep(0f, 1f, t)
                : Mathf.Lerp(0.62f, 1f, Mathf.Sin(t * Mathf.PI));
            Color vertexColor = color;
            vertexColor.a = alpha * ageAlpha;
            colors.Add(vertexColor);
            colors.Add(vertexColor);

            if (i == count - 1) continue;
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

    /// <summary>按既有路径分段补点，减轻低采样率下的折线感。</summary>
    private static void PreparePoints(IReadOnlyList<Vector3> source, bool smooth, ICollection<Vector3> result)
    {
        result.Clear();
        if (!smooth || source.Count < 3)
        {
            for (var i = 0; i < source.Count; i++) result.Add(source[i]);
            return;
        }

        result.Add(source[0]);
        for (var i = 0; i < source.Count - 1; i++)
        {
            Vector3 current = source[i];
            Vector3 next = source[i + 1];
            result.Add(Vector3.Lerp(current, next, 0.25f));
            result.Add(Vector3.Lerp(current, next, 0.75f));
        }
        result.Add(source[^1]);
    }
}
