using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private const int ConductionBands = 3, ConductionCellsPerWave = 2048, ConductionVisibleCells = 8192, ConductionVisibleWaves = 32;
    private const float ConductionBandDelay = .035f, ConductionCrestSeconds = .23f;
    private static readonly List<ConductionWake> conductionWakes = new();
    private static int conductionVisibleCells;

    /// <summary>一次确认到达时的水面与上游方向，不再读取活动或留存水体。</summary>
    private readonly struct ConductionFront
    {
        internal readonly Rect Area;
        internal readonly Vector2 Direction;
        internal readonly float Height, Travel;

        internal ConductionFront(Rect area, float height, Vector2 direction, float travel)
        {
            Area = area;
            Height = height;
            Direction = direction;
            Travel = travel;
        }
    }

    private sealed class ConductionWake
    {
        internal readonly double At;
        internal readonly int Cells;
        internal readonly Mesh[] Bands = new Mesh[ConductionBands];

        internal ConductionWake(double at, int cells)
        {
            At = at;
            Cells = cells;
        }
    }

    /// <summary>按实际水路距离分阶段发布；极大水域抽取已确认水格，仍不绘制干地。</summary>
    private static void AddConductionVisuals(List<ConductionFront> fronts, double clock, int pulse)
    {
        int count = Mathf.Min(fronts.Count, ConductionCellsPerWave);
        if (count == 0) return;
        // 保护正在显示的上一轮完整画面；超出预算只跳过本波的显示。
        if (conductionWakes.Count >= ConductionVisibleWaves || conductionVisibleCells + count > ConductionVisibleCells) return;

        float longest = 0;
        foreach (var front in fronts) longest = Mathf.Max(longest, front.Travel);
        var wake = new ConductionWake(clock, count);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int band = 0; band < ConductionBands; band++)
        {
            vertices.Clear();
            uv.Clear();
            triangles.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = count == 1 ? 0 : (int)((long)i * (fronts.Count - 1) / (count - 1));
                var front = fronts[index];
                int phase = longest == 0 ? 0 : Mathf.Min(ConductionBands - 1, (int)(front.Travel / longest * ConductionBands));
                if (phase == band) AddCrest(front, unchecked(index * 137 + pulse * 17), vertices, uv, triangles);
            }

            if (vertices.Count == 0) continue;
            var mesh = new Mesh { name = "Cultiway Water Discharge" };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            wake.Bands[band] = mesh;
        }

        conductionWakes.Add(wake);
        conductionVisibleCells += count;
    }

    /// <summary>曲形亮脊从两侧低面抬起，所有顶点都留在这块真实水面内。</summary>
    private static void AddCrest(ConductionFront front, int variation, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        const int segments = 7;
        float seed = (variation & 127) / 127f;
        float scale = Mathf.Min(front.Area.width, front.Area.height);
        Vector2 forward = front.Direction.sqrMagnitude > .000001f ? front.Direction.normalized :
            new Vector2(Mathf.Cos(seed * 6.283185f), Mathf.Sin(seed * 6.283185f));
        Vector2 across = new(-forward.y, forward.x);
        float bend = (variation & 128) == 0 ? 1f : -1f;
        int start = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float arc = Mathf.Sin(t * Mathf.PI);
            float flutter = Mathf.Sin(t * 7f + seed * 6f);
            float sideways = (t - .5f) * Mathf.Lerp(.7f, .91f, seed);
            float waist = (.20f * arc + .045f * flutter) * bend;
            float width = .012f + .126f * arc * Mathf.Lerp(.77f, 1.19f, seed);
            for (int ridge = -1; ridge <= 1; ridge++)
            {
                Vector2 offset = across * sideways + forward * (waist + width * ridge);
                Vector2 point = front.Area.center + offset * scale;
                float height = front.Height + Mathf.Min(.045f, scale * .045f)
                    + (ridge == 0 ? arc * Mathf.Min(.13f, scale * .16f) : 0f);
                vertices.Add(new Vector3(Mathf.Clamp(point.x, front.Area.xMin, front.Area.xMax),
                    Mathf.Clamp(point.y, front.Area.yMin, front.Area.yMax), height));
                uv.Add(new Vector2(t, (ridge + 1) * .5f));
            }

            if (i == segments) continue;
            for (int ridge = 0; ridge < 2; ridge++)
            {
                int edge = start + i * 3 + ridge;
                triangles.Add(edge);
                triangles.Add(edge + 1);
                triangles.Add(edge + 3);
                triangles.Add(edge + 1);
                triangles.Add(edge + 4);
                triangles.Add(edge + 3);
            }
        }
    }

    /// <summary>从同一世界时钟分批亮起、退散，供两个显示方式读同一组形体。</summary>
    private void AppendConductionVisuals(double now)
    {
        if (conductionWakes.Count == 0) return;
        FormResource resource = FormResources.Load("lightning_burst");
        for (int i = conductionWakes.Count - 1; i >= 0; i--)
        {
            var wake = conductionWakes[i];
            float age = (float)(now - wake.At);
            if (age >= ConductionCrestSeconds + (ConductionBands - 1) * ConductionBandDelay)
            {
                RemoveConductionWake(i);
                continue;
            }

            for (int band = 0; band < ConductionBands; band++)
            {
                var mesh = wake.Bands[band];
                float phase = age - band * ConductionBandDelay;
                if (mesh == null || phase < 0f || phase >= ConductionCrestSeconds) continue;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, ConductionCrestSeconds, phase));
                float opacity = fade * .91f;
                float gleam = Mathf.Clamp01(1f - phase / .12f);
                var tint = new Color(.35f + gleam * .53f, .71f + gleam * .27f, 1f, opacity);
                visuals.Add(new FormVisualInstance(resource, mesh, tint, terrain));
            }
        }
    }

    private static void RemoveConductionWake(int index)
    {
        var wake = conductionWakes[index];
        foreach (var mesh in wake.Bands)
            if (mesh != null) Object.Destroy(mesh);
        conductionVisibleCells -= wake.Cells;
        conductionWakes.RemoveAt(index);
    }

    private static void ClearConductionVisuals()
    {
        while (conductionWakes.Count > 0) RemoveConductionWake(conductionWakes.Count - 1);
    }
}
