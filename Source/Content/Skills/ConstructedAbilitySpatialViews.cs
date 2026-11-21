using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private void AppendSpatialViews(RunningCast running, double time)
    {
        var pairs = running.Cast.Plan.SpatialViewPairs;
        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            if (!running.Cast.TrySpatialView(pair, out Matrix4x4 near, out Matrix4x4 far)) continue;
            running.SpatialViews ??= new SpatialDestinationRelief[pairs.Length];
            var relief = running.SpatialViews[i] ??= new SpatialDestinationRelief();
            relief.Refresh(far, time);
            visuals.Add(new FormVisualInstance(running.Cast.Plan.Nodes[pair.Near].Resource, relief.Mesh, near, Color.white,
                terrain, "space_destination"));
        }
    }

    private static void ReleaseSpatialViews(RunningCast running)
    {
        if (running.ArtifactSectorMesh != null)
        {
            Object.Destroy(running.ArtifactSectorMesh);
            running.ArtifactSectorMesh = null;
        }
        if (running.SpatialViews == null) return;
        foreach (var relief in running.SpatialViews)
            if (relief != null) Object.Destroy(relief.Mesh);
        running.SpatialViews = null;
    }

    /// <summary>只采对端附近的真实地块；两种显示使用同一个凹在门框里的立体取景。</summary>
    private sealed class SpatialDestinationRelief
    {
        private const int Width = 7, Height = 5;
        private readonly List<Vector3> vertices = new(Width * Height * 24);
        private readonly List<Vector4> tones = new(Width * Height * 24);
        private readonly List<int> faces = new(Width * Height * 36);
        private readonly Cell[] cells = new Cell[Width * Height];
        private Vector2 centre, right, forward;
        private int baseHeight;
        private double nextRefresh;
        internal readonly Mesh Mesh = new() { name = "Cultiway Spatial Destination" };

        internal SpatialDestinationRelief() => Mesh.MarkDynamic();

        internal void Refresh(Matrix4x4 destination, double time)
        {
            Vector2 position = (Vector3)destination.GetColumn(3);
            Vector2 side = ((Vector2)destination.MultiplyVector(Vector3.right)).normalized;
            Vector2 depth = ((Vector2)destination.MultiplyVector(Vector3.up)).normalized;
            if (side == Vector2.zero) side = Vector2.right;
            if (depth == Vector2.zero) depth = Vector2.up;
            bool first = nextRefresh == 0d;
            if (!first && time < nextRefresh && (position - centre).sqrMagnitude < .25f &&
                (side - right).sqrMagnitude < .04f && (depth - forward).sqrMagnitude < .04f) return;
            nextRefresh = time + .25d;
            centre = position;
            right = side;
            forward = depth;
            int elevation = Tile(position)?.Height ?? 0;
            bool changed = first || elevation != baseHeight;
            baseHeight = elevation;
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
            {
                int index = row * Width + col;
                WorldTile tile = Tile(position + side * (col - (Width - 1) * .5f) + depth * (row - (Height - 1) * .5f));
                var cell = new Cell(tile);
                changed |= !cell.Equals(cells[index]);
                cells[index] = cell;
            }
            if (changed) Build();
        }

        private static WorldTile Tile(Vector2 point)
        {
            int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y);
            return (uint)x < (uint)MapBox.width && (uint)y < (uint)MapBox.height ? World.world.GetTileSimple(x, y) : null;
        }

        private void Build()
        {
            vertices.Clear();
            tones.Clear();
            faces.Clear();
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
            {
                Color32 color = cells[row * Width + col].Color;
                Vector3 a = Point(col, row), b = Point(col + 1, row), c = Point(col + 1, row + 1), d = Point(col, row + 1);
                var t = new Vector3(0f, Relief(col, row), 0f);
                Quad(a - t, b - t, c - t, d - t, color, 1f);
                Quad(b + t, a + t, d + t, c + t, color, 1f);
            }

            // 相接的格只补真正的高差；两侧等高时不生成相互重叠的侧面。
            for (int row = 0; row < Height; row++)
            for (int col = 0; col < Width; col++)
            {
                Vector3 a = Point(col, row), b = Point(col + 1, row), c = Point(col + 1, row + 1), d = Point(col, row + 1);
                Color32 color = cells[row * Width + col].Color;
                float relief = Relief(col, row);
                AddWall(a, b, relief, col, row - 1, color);
                AddWall(b, c, relief, col + 1, row, color);
                AddWall(c, d, relief, col, row + 1, color);
                AddWall(d, a, relief, col - 1, row, color);
            }

            Mesh.Clear();
            Mesh.SetVertices(vertices);
            // 这个表面复用流层资料的位置传颜色；其他表面仍按原约定读流速。
            Mesh.SetUVs(5, tones);
            Mesh.SetTriangles(faces, 0);
            Mesh.RecalculateNormals();
        }

        private float Relief(int col, int row)
        {
            Cell cell = cells[row * Width + col];
            return .034f + Mathf.Clamp((cell.Height - baseHeight) * .005f, 0f, .088f) + (cell.Building ? .045f : 0f);
        }

        private void AddWall(Vector3 a, Vector3 b, float relief, int col, int row, Color32 color)
        {
            var t = new Vector3(0f, relief, 0f);
            if ((uint)col >= Width || (uint)row >= Height)
            {
                Quad(b - t, a - t, a + t, b + t, color, .72f);
                return;
            }

            float next = Relief(col, row);
            if (next >= relief) return;
            var n = new Vector3(0f, next, 0f);
            Quad(b - t, a - t, a - n, b - n, color, .78f);
            Quad(a + t, b + t, b + n, a + n, color, .78f);
        }

        private static Vector3 Point(int x, int y)
        {
            float v = (float)y / Height;
            return new Vector3((2f * x / Width - 1f) * 1.023f, (v - .5f) * 1.06f, .74f + v * 1.94f);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color, float shade)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            var tone = new Vector4(color.r / 255f, color.g / 255f, color.b / 255f, shade);
            for (int i = 0; i < 4; i++) tones.Add(tone);
            faces.Add(index); faces.Add(index + 1); faces.Add(index + 2);
            faces.Add(index); faces.Add(index + 2); faces.Add(index + 3);
        }

        private readonly struct Cell
        {
            internal readonly int Height;
            internal readonly bool Building;
            internal readonly Color32 Color;

            internal Cell(WorldTile tile)
            {
                Height = tile?.Height ?? 0;
                Building = tile != null && tile.hasBuilding();
                Color32 building = Building ? tile.building.getColorForMinimap(tile) : default;
                Color = building.a != 0 ? building : tile != null ? (Color32)tile.getColor() : new Color32(16, 22, 27, 255);
            }

            internal bool Equals(Cell other) => Height == other.Height && Building == other.Building &&
                Color.r == other.Color.r && Color.g == other.Color.g && Color.b == other.Color.b;
        }
    }
}
