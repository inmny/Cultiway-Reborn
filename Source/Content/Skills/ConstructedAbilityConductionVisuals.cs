using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private const int ConductionBands = 3, ConductionCellsPerWave = 2048, ConductionVisibleCells = 8192, ConductionVisibleWaves = 32;
    private const float ConductionBandDelay = .035f, ConductionCrestSeconds = .29f;
    private static readonly List<ConductionWake> conductionWakes = new();
    private static int conductionVisibleCells;

    /// <summary>一次确认到达时的水面与上游序号，不再读取活动或留存水体。</summary>
    private readonly struct ConductionFront
    {
        internal readonly Rect Area;
        internal readonly int Parent;
        internal readonly float Height, Travel;

        internal ConductionFront(Rect area, float height, int parent, float travel)
        {
            Area = area;
            Height = height;
            Parent = parent;
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

        float longest = fronts[fronts.Count - 1].Travel;
        var load = new int[fronts.Count];
        var selected = new bool[fronts.Count];
        for (int i = fronts.Count - 1; i >= 0; i--)
        {
            load[i]++;
            if (fronts[i].Parent >= 0) load[fronts[i].Parent] += load[i];
            longest = Mathf.Max(longest, fronts[i].Travel);
        }
        for (int i = 0; i < count; i++) selected[VisibleIndex(i)] = true;

        float peak = Mathf.Log(1 + fronts.Count);
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
                int index = VisibleIndex(i);
                var front = fronts[index];
                int phase = longest == 0 ? 0 : Mathf.Min(ConductionBands - 1, (int)(front.Travel / longest * ConductionBands));
                if (phase != band) continue;
                int salt = unchecked(index * 137 + pulse * 17);
                float width = Mathf.Lerp(.063f, .34f, Mathf.Log(1 + load[index]) / peak);
                if (front.Parent >= 0 && selected[front.Parent])
                {
                    ConductionFront parent = fronts[front.Parent];
                    Vector2 gate = WaterJunction(parent.Area, front.Area);
                    AddBranch(parent, gate, width, width * .66f, salt, vertices, uv, triangles);
                    AddBranch(front, gate, load[index] == 1 ? .025f : width, width * .66f, salt, vertices, uv, triangles);
                }
                else
                {
                    float angle = (salt & 127) * (6.283185f / 128);
                    Vector2 axis = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 tip = front.Area.center + axis * Mathf.Min(front.Area.width, front.Area.height) * .34f;
                    AddBranch(front, tip, width, .018f, salt, vertices, uv, triangles);
                }
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
        int VisibleIndex(int i) => count == 1 ? 0 : (int)((long)i * (fronts.Count - 1) / (count - 1));
    }

    /// <summary>在实际相邻足迹的公共边内挑选连接点，两个半段都留在自己的水面。</summary>
    private static Vector2 WaterJunction(Rect a, Rect b)
    {
        Vector2 middle = (a.center + b.center) * .5f;
        return new Vector2(Within(middle.x, Mathf.Max(a.xMin, b.xMin), Mathf.Min(a.xMax, b.xMax)),
            Within(middle.y, Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.yMax, b.yMax)));

        static float Within(float value, float low, float high) => low > high ? (low + high) * .5f : Mathf.Clamp(value, low, high);
    }

    /// <summary>沿真实水边伸出有脊背的半段；经过分岔时由粗到细，边沿不伸出水格。</summary>
    private static void AddBranch(ConductionFront front, Vector2 end, float startWidth, float endWidth, int variation,
        List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        const int rings = 3, shoulders = 5;
        Vector2 center = front.Area.center, run = end - center;
        float scale = Mathf.Min(front.Area.width, front.Area.height);
        Vector2 across = run.sqrMagnitude > .000001f ? new Vector2(-run.y, run.x).normalized : Vector2.up;
        float bend = (variation & 127) / 127f - .5f;
        int start = vertices.Count;
        for (int i = 0; i < rings; i++)
        {
            float t = i * .5f, width = Mathf.Lerp(startWidth, endWidth, t) * scale;
            Vector2 mid = Vector2.Lerp(center, end, t) + across * (i == 1 ? .17f * bend * scale : 0f);
            float raised = Mathf.Min(.23f, width * .85f + .035f);
            for (int j = 0; j < shoulders; j++)
            {
                Vector2 point = mid + across * ((j - 2) * .5f * width);
                float ridge = j == 2 ? 1f : j is 1 or 3 ? .50f : 0f;
                vertices.Add(new Vector3(Mathf.Clamp(point.x, front.Area.xMin, front.Area.xMax),
                    Mathf.Clamp(point.y, front.Area.yMin, front.Area.yMax), front.Height + .042f * Mathf.Min(1f, scale) + ridge * raised));
                uv.Add(new Vector2(t, j * .25f));
                if (i == rings - 1 || j == shoulders - 1) continue;
                int edge = start + i * shoulders + j;
                triangles.Add(edge);
                triangles.Add(edge + shoulders);
                triangles.Add(edge + 1);
                triangles.Add(edge + 1);
                triangles.Add(edge + shoulders);
                triangles.Add(edge + shoulders + 1);
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
