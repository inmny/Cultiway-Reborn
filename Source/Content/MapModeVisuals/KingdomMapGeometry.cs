using System;
using System.Collections.Generic;
using LibTessDotNet;
using UnityEngine;

namespace Cultiway.Content.MapModeVisuals;

internal sealed class KingdomMapSnapshot
{
    internal readonly int Generation;
    internal readonly int WorldId;
    internal readonly int ZonesWidth;
    internal readonly int ZonesHeight;
    internal readonly int WorldWidth;
    internal readonly int WorldHeight;
    internal readonly long[] Owners;
    internal readonly Dictionary<long, Color32> Colors;

    internal KingdomMapSnapshot(
        int generation,
        int worldId,
        int zonesWidth,
        int zonesHeight,
        int worldWidth,
        int worldHeight,
        long[] owners,
        Dictionary<long, Color32> colors)
    {
        Generation = generation;
        WorldId = worldId;
        ZonesWidth = zonesWidth;
        ZonesHeight = zonesHeight;
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        Owners = owners;
        Colors = colors;
    }

    internal long GetOwner(int x, int y)
    {
        if ((uint)x >= (uint)ZonesWidth || (uint)y >= (uint)ZonesHeight) return 0;
        return Owners[y * ZonesWidth + x];
    }
}

internal sealed class KingdomMapGeometry
{
    internal readonly int Generation;
    internal readonly int WorldId;
    internal readonly Vector3[] FillVertices;
    internal readonly int[] FillTriangles;
    internal readonly Color32[] FillColors;
    internal readonly Vector2[] FillHighlights;
    internal readonly long[] FillOwners;
    internal readonly Vector3[] BorderVertices;
    internal readonly int[] BorderTriangles;
    internal readonly Color32[] BorderColors;
    internal readonly Vector2[] BorderLineData;
    internal readonly Vector2[] BorderMiterExpand;
    internal readonly Vector2[] BorderHighlights;
    internal readonly long[] BorderOwners;

    internal KingdomMapGeometry(
        KingdomMapSnapshot snapshot,
        List<Vector3> fillVertices,
        List<int> fillTriangles,
        List<Color32> fillColors,
        List<Vector2> fillHighlights,
        List<long> fillOwners,
        List<Vector3> borderVertices,
        List<int> borderTriangles,
        List<Color32> borderColors,
        List<Vector2> borderLineData,
        List<Vector2> borderMiterExpand,
        List<Vector2> borderHighlights,
        List<long> borderOwners)
    {
        Generation = snapshot.Generation;
        WorldId = snapshot.WorldId;
        FillVertices = fillVertices.ToArray();
        FillTriangles = fillTriangles.ToArray();
        FillColors = fillColors.ToArray();
        FillHighlights = fillHighlights.ToArray();
        FillOwners = fillOwners.ToArray();
        BorderVertices = borderVertices.ToArray();
        BorderTriangles = borderTriangles.ToArray();
        BorderColors = borderColors.ToArray();
        BorderLineData = borderLineData.ToArray();
        BorderMiterExpand = borderMiterExpand.ToArray();
        BorderHighlights = borderHighlights.ToArray();
        BorderOwners = borderOwners.ToArray();
    }
}

internal static class KingdomMapGeometryBuilder
{
    internal static KingdomMapGeometry Build(KingdomMapSnapshot snapshot)
    {
        KingdomBoundaryMap boundaryMap = KingdomMapBoundaryBuilder.Build(snapshot);
        var fillVertices = new List<Vector3>(snapshot.Owners.Length * 4);
        var fillTriangles = new List<int>(snapshot.Owners.Length * 6);
        var fillColors = new List<Color32>(snapshot.Owners.Length * 4);
        var fillHighlights = new List<Vector2>(snapshot.Owners.Length * 4);
        var fillOwners = new List<long>(snapshot.Owners.Length * 4);
        BuildFill(
            snapshot,
            boundaryMap.Contours,
            fillVertices,
            fillTriangles,
            fillColors,
            fillHighlights,
            fillOwners);

        var borderVertices = new List<Vector3>(boundaryMap.Paths.Length * 8);
        var borderTriangles = new List<int>(boundaryMap.Paths.Length * 12);
        var borderColors = new List<Color32>(boundaryMap.Paths.Length * 8);
        var borderLineData = new List<Vector2>(boundaryMap.Paths.Length * 8);
        var borderMiterExpand = new List<Vector2>(boundaryMap.Paths.Length * 8);
        var borderHighlights = new List<Vector2>(boundaryMap.Paths.Length * 8);
        var borderOwners = new List<long>(boundaryMap.Paths.Length * 8);
        BuildBorders(
            snapshot,
            boundaryMap.Paths,
            borderVertices,
            borderTriangles,
            borderColors,
            borderLineData,
            borderMiterExpand,
            borderHighlights,
            borderOwners);

        return new KingdomMapGeometry(
            snapshot,
            fillVertices,
            fillTriangles,
            fillColors,
            fillHighlights,
            fillOwners,
            borderVertices,
            borderTriangles,
            borderColors,
            borderLineData,
            borderMiterExpand,
            borderHighlights,
            borderOwners);
    }

    private static void BuildFill(
        KingdomMapSnapshot snapshot,
        KingdomMapContour[] contours,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        List<Vector2> highlights,
        List<long> owners)
    {
        var contoursByOwner = new Dictionary<long, List<Vector2[]>>();
        for (int i = 0; i < contours.Length; i++)
        {
            KingdomMapContour contour = contours[i];
            if (!contoursByOwner.TryGetValue(contour.Owner, out List<Vector2[]> ownerContours))
            {
                ownerContours = new List<Vector2[]>();
                contoursByOwner.Add(contour.Owner, ownerContours);
            }
            ownerContours.Add(contour.Points);
        }

        foreach (KeyValuePair<long, List<Vector2[]>> pair in contoursByOwner)
        {
            var tessellator = new Tess { NoEmptyPolygons = true };
            for (int contourIndex = 0; contourIndex < pair.Value.Count; contourIndex++)
            {
                Vector2[] points = pair.Value[contourIndex];
                var contour = new ContourVertex[points.Length];
                for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    contour[pointIndex] = new ContourVertex(new Vec3(points[pointIndex].x, points[pointIndex].y, 0f));
                }
                tessellator.AddContour(contour, ContourOrientation.Original);
            }
            tessellator.Tessellate(
                WindingRule.NonZero,
                ElementType.Polygons,
                3,
                null,
                new Vec3(0f, 0f, 1f));

            int vertexOffset = vertices.Count;
            Color32 color = ResolveColor(snapshot, pair.Key);
            for (int i = 0; i < tessellator.VertexCount; i++)
            {
                Vec3 point = tessellator.Vertices[i].Position;
                vertices.Add(new Vector3(point.X, point.Y, 0f));
                colors.Add(color);
                highlights.Add(Vector2.zero);
                owners.Add(pair.Key);
            }
            for (int i = 0; i < tessellator.ElementCount * 3; i++)
            {
                int index = tessellator.Elements[i];
                if (index != Tess.Undef) triangles.Add(vertexOffset + index);
            }
        }
    }

    private static void BuildBorders(
        KingdomMapSnapshot snapshot,
        KingdomBoundaryPath[] paths,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        List<Vector2> lineData,
        List<Vector2> miterExpand,
        List<Vector2> highlights,
        List<long> owners)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            KingdomBoundaryPath path = paths[i];
            AddBorderPath(
                snapshot,
                path.Points,
                path.LeftOwner,
                path.RightOwner,
                vertices,
                triangles,
                colors,
                lineData,
                miterExpand,
                highlights,
                owners);
        }
    }

    private static void AddBorderPath(
        KingdomMapSnapshot snapshot,
        Vector2[] path,
        long leftOwner,
        long rightOwner,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color32> colors,
        List<Vector2> lineData,
        List<Vector2> miterExpand,
        List<Vector2> highlights,
        List<long> owners)
    {
        float distance = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            if (i > 0) distance += Distance(path[i - 1], path[i]);
            Vector2 expand = CalculateMiter(path, i);
            int vertex = vertices.Count;
            vertices.Add(new Vector3(path[i].x, path[i].y, 0f));
            vertices.Add(new Vector3(path[i].x, path[i].y, 0f));
            lineData.Add(new Vector2(distance, -1f));
            lineData.Add(new Vector2(distance, 1f));
            miterExpand.Add(expand);
            miterExpand.Add(expand);
            highlights.Add(Vector2.zero);
            highlights.Add(Vector2.zero);

            long resolvedRight = rightOwner == 0 ? leftOwner : rightOwner;
            long resolvedLeft = leftOwner == 0 ? rightOwner : leftOwner;
            owners.Add(resolvedRight);
            owners.Add(resolvedLeft);
            colors.Add(ResolveBorderColor(snapshot, rightOwner, leftOwner));
            colors.Add(ResolveBorderColor(snapshot, leftOwner, rightOwner));

            if (i == path.Length - 1) continue;
            triangles.Add(vertex);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 2);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 3);
            triangles.Add(vertex + 2);
        }
    }

    private static Vector2 CalculateMiter(Vector2[] path, int index)
    {
        Vector2 previous;
        Vector2 next;
        bool closed = path.Length > 3 && SamePoint(path[0], path[^1]);
        if (closed)
        {
            int count = path.Length - 1;
            int current = index == count ? 0 : index;
            previous = path[current] - path[(current - 1 + count) % count];
            next = path[(current + 1) % count] - path[current];
        }
        else
        {
            previous = index == 0 ? path[1] - path[0] : path[index] - path[index - 1];
            next = index == path.Length - 1 ? path[^1] - path[^2] : path[index + 1] - path[index];
        }
        previous = Normalize(previous);
        next = Normalize(next);
        Vector2 previousNormal = new(-previous.y, previous.x);
        Vector2 nextNormal = new(-next.y, next.x);
        Vector2 miter = Normalize(previousNormal + nextNormal);
        float denominator = miter.x * nextNormal.x + miter.y * nextNormal.y;
        if (Math.Abs(denominator) < 0.35f) return nextNormal;
        float scale = Math.Min(2.2f, Math.Abs(1f / denominator));
        return miter * scale;
    }

    private static bool SamePoint(Vector2 left, Vector2 right)
    {
        return Math.Abs(left.x - right.x) < 0.0001f && Math.Abs(left.y - right.y) < 0.0001f;
    }

    private static Vector2 Normalize(Vector2 value)
    {
        float length = (float)Math.Sqrt(value.x * value.x + value.y * value.y);
        return length < 0.0001f ? Vector2.right : value / length;
    }

    private static float Distance(Vector2 left, Vector2 right)
    {
        float x = right.x - left.x;
        float y = right.y - left.y;
        return (float)Math.Sqrt(x * x + y * y);
    }

    private static Color32 ResolveColor(KingdomMapSnapshot snapshot, long owner)
    {
        return snapshot.Colors.TryGetValue(owner, out Color32 color)
            ? color
            : new Color32(164, 164, 164, 255);
    }

    private static Color32 ResolveBorderColor(KingdomMapSnapshot snapshot, long owner, long otherOwner)
    {
        Color32 color = ResolveColor(snapshot, owner == 0 ? otherOwner : owner);
        if (owner != 0) return color;
        color.r = (byte)(color.r * 0.62f);
        color.g = (byte)(color.g * 0.62f);
        color.b = (byte)(color.b * 0.62f);
        return color;
    }

}
