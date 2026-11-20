using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>相接地貌的一段真实取景。表面用地图颜色，地层和景物仍能从侧面辨认。</summary>
internal sealed class LandscapeSnapshot : IDisposable
{
    /// <summary>本次施放自己的景物和实际切面的网格。</summary>
    internal readonly Mesh Scene, Edge;

    /// <summary>对合位置的真实地图格、植被和建筑被采进同一带，取景完成后世界不再被读取。</summary>
    /// <param name="rule">已经检查的取景尺寸和实际切面。</param>
    /// <param name="source">原地貌与这条断面对合时的局部姿态。</param>
    /// <param name="terrain">真实共同地表。</param>
    internal LandscapeSnapshot(CompiledLandscapeSlice rule, Matrix4x4 source, AbilityHeightField terrain)
    {
        var builder = new Relief();
        float floor = -rule.Thickness;
        int width = rule.Width, length = rule.Length;
        float left = -width * .5f, front = -length * .5f;
        var corners = new Vector3[(width + 1) * (length + 1)];
        for (int y = 0; y <= length; y++)
        for (int x = 0; x <= width; x++)
            corners[y * (width + 1) + x] = Corner(left + x, front + y);

        var visible = new HashSet<Building>();
        for (int y = 0; y < length; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3 sample = source.MultiplyPoint3x4(new Vector3(left + x + .5f, front + y + .5f, 0f));
            WorldTile tile = Tile(sample.x, sample.y);
            Color32 colour = tile == null ? new Color32(22, 36, 55, 255) : (Color32)tile.getColor();
            int first = y * (width + 1) + x;
            Vector3 a = corners[first], b = corners[first + 1],
                c = corners[first + width + 2], d = corners[first + width + 1];
            builder.Quad(a, b, c, d, colour, 1f);
            if (y == 0) Cut(a, b, colour);
            if (x == width - 1) Cut(b, c, colour);
            if (y == length - 1) Cut(c, d, colour);
            if (x == 0) Cut(d, a, colour);
            if (tile?.building != null) visible.Add(tile.building);
        }

        Vector3 lower = new(left, front, floor), upper = new(-left, -front, floor);
        builder.Quad(lower, new Vector3(left, -front, floor), upper, new Vector3(-left, front, floor),
            new Color32(53, 67, 89, 255), .76f);

        Matrix4x4 toLocal = source.inverse;
        foreach (Building building in visible)
        {
            WorldTile home = building.current_tile;
            if (home == null || building.asset == null) continue;
            Vector3 point = toLocal.MultiplyPoint3x4(new Vector3(home.x + .5f, home.y + .5f, source.m23));
            if (point.x < left || point.x >= -left || point.y < front || point.y >= -front) continue;
            Vector3 ground = Corner(point.x, point.y);
            Color32 colour = building.getColorForMinimap(home);
            if (colour.a == 0) colour = (Color32)home.getColor();
            if (building.asset.flora)
                builder.Tree(ground, colour);
            else
            {
                BuildingFundament size = building.asset.fundament;
                float xSize = size == null ? .44f : Mathf.Clamp((size.left + size.right + 1) * .38f, .36f, 1.1f);
                float ySize = size == null ? .40f : Mathf.Clamp((size.top + size.bottom + 1) * .38f, .36f, 1.1f);
                float x = Mathf.Min(xSize, -left - Mathf.Abs(point.x));
                float y = Mathf.Min(ySize, -front - Mathf.Abs(point.y));
                if (x > .05f && y > .05f) builder.House(ground, x, y, colour);
            }
        }

        Scene = builder.Build("Cultiway Folded Landscape");
        builder = new Relief();
        builder.Box(rule.Edge.min, rule.Edge.max, new Color32(143, 209, 233, 255));
        Edge = builder.Build("Cultiway Folded Edge");

        Vector3 Corner(float x, float y)
        {
            Vector3 point = source.MultiplyPoint3x4(new Vector3(x, y, 0f));
            float ground = terrain?.Sample(point.x, point.y) ?? point.z;
            float height = (ground - point.z) / Mathf.Max(.001f, source.lossyScale.z);
            return new Vector3(x, y, Mathf.Clamp(height, floor + .035f, 1.45f) + .028f);
        }

        void Cut(Vector3 a, Vector3 b, Color32 surface)
        {
            var a0 = new Vector3(a.x, a.y, floor);
            var b0 = new Vector3(b.x, b.y, floor);
            var a1 = Vector3.Lerp(a0, a, .43f);
            var b1 = Vector3.Lerp(b0, b, .43f);
            Color32 middle = Color32.Lerp(surface, new Color32(104, 82, 64, 255), .63f);
            builder.Quad(a0, b0, b1, a1, new Color32(74, 72, 84, 255), .79f);
            builder.Quad(a1, b1, b, a, middle, .88f);
        }
    }

    /// <summary>读取确实在地图范围内的原地块。</summary>
    private static WorldTile Tile(float x, float y)
    {
        int col = Mathf.FloorToInt(x), row = Mathf.FloorToInt(y);
        return (uint)col < (uint)MapBox.width && (uint)row < (uint)MapBox.height ? World.world.GetTileSimple(col, row) : null;
    }

    /// <summary>只销毁本次施放生成的显示网格。</summary>
    public void Dispose()
    {
        Object.Destroy(Scene);
        Object.Destroy(Edge);
    }

    /// <summary>以与折景门相同的顶点色格式制作有厚度的景物。</summary>
    private sealed class Relief
    {
        private readonly List<Vector3> points = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector4> colours = new();
        private readonly List<int> faces = new();

        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 colour, float shade)
        {
            int first = points.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vertex(a, normal, colour, shade); Vertex(b, normal, colour, shade);
            Vertex(c, normal, colour, shade); Vertex(d, normal, colour, shade);
            Face(first, first + 1, first + 2);
            Face(first, first + 2, first + 3);
        }

        internal void Box(Vector3 min, Vector3 max, Color32 colour)
        {
            Vector3 a = min, b = new(max.x, min.y, min.z), c = new(max.x, max.y, min.z), d = new(min.x, max.y, min.z);
            Vector3 up = Vector3.forward * (max.z - min.z);
            Quad(a, d, c, b, colour, .64f);
            Quad(a + up, b + up, c + up, d + up, colour, 1f);
            Quad(a, b, b + up, a + up, colour, .83f);
            Quad(b, c, c + up, b + up, colour, .74f);
            Quad(c, d, d + up, c + up, colour, .88f);
            Quad(d, a, a + up, d + up, colour, .75f);
        }

        internal void Tree(Vector3 foot, Color32 colour)
        {
            Box(foot - new Vector3(.065f, .065f, 0f), foot + new Vector3(.065f, .065f, .57f),
                new Color32(101, 73, 52, 255));
            Color32 leaf = Color32.Lerp(colour, new Color32(64, 117, 74, 255), .55f);
            Roof(foot + Vector3.forward * .37f, .32f, .31f, .57f, leaf);
            Roof(foot + Vector3.forward * .63f, .26f, .25f, .52f, leaf);
        }

        internal void House(Vector3 foot, float x, float y, Color32 colour)
        {
            Box(foot - new Vector3(x * .84f, y * .84f, 0f), foot + new Vector3(x * .84f, y * .84f, .48f),
                Color32.Lerp(colour, new Color32(216, 187, 147, 255), .55f));
            Roof(foot + Vector3.forward * .48f, x, y, .34f, colour);
        }

        private void Roof(Vector3 centre, float x, float y, float height, Color32 colour)
        {
            Vector3 a = centre + new Vector3(-x, -y), b = centre + new Vector3(x, -y),
                c = centre + new Vector3(x, y), d = centre + new Vector3(-x, y);
            Vector3 front = centre + new Vector3(0, -y * .38f, height), back = centre + new Vector3(0, y * .38f, height);
            Quad(b, c, back, front, colour, .82f);
            Quad(d, a, front, back, colour, 1f);
            Triangle(a, b, front, colour, .91f);
            Triangle(c, d, back, colour, .72f);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color32 colour, float shade)
        {
            int first = points.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vertex(a, normal, colour, shade); Vertex(b, normal, colour, shade); Vertex(c, normal, colour, shade);
            Face(first, first + 1, first + 2);
        }

        private void Vertex(Vector3 point, Vector3 normal, Color32 colour, float shade)
        {
            points.Add(point);
            normals.Add(normal);
            colours.Add(new Vector4(colour.r / 255f, colour.g / 255f, colour.b / 255f, shade));
        }

        // 与共同流层网格采用相同的顺时针正面，仍向外提供受光法线。
        private void Face(int a, int b, int c) { faces.Add(a); faces.Add(c); faces.Add(b); }

        internal Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(5, colours);
            mesh.SetTriangles(faces, 0);
            return mesh;
        }
    }
}
