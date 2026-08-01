using System.Collections.Generic;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>一段具有独立半径、线宽、起点和方向的环形带。</summary>
internal readonly struct WorldArcBand
{
    public readonly float Radius;
    public readonly float Width;
    public readonly float StartDegrees;
    public readonly float SpanDegrees;

    public WorldArcBand(float radius, float width, float startDegrees, float spanDegrees)
    {
        Radius = radius;
        Width = width;
        StartDegrees = startDegrees;
        SpanDegrees = spanDegrees;
    }
}

/// <summary>使用带状网格绘制固定世界线宽的圆弧，避免缩放环形贴图导致线宽失真。</summary>
internal sealed class WorldArcRenderer : MonoBehaviour
{
    private const float DegreesPerSubdivision = 5f;
    private readonly List<Vector3> vertices = new(256);
    private readonly List<Vector2> uv = new(256);
    private readonly List<Color> colors = new(256);
    private readonly List<int> triangles = new(768);
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    /// <summary>创建一个拥有独立网格缓冲的圆弧视图。</summary>
    public static WorldArcRenderer Create(Transform parent, string name, int sortingOrder)
    {
        GameObject obj = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        WorldArcRenderer view = obj.AddComponent<WorldArcRenderer>();
        view.mesh = new Mesh
        {
            name = $"{name}_Mesh",
            hideFlags = HideFlags.DontSave
        };
        obj.GetComponent<MeshFilter>().sharedMesh = view.mesh;
        view.meshRenderer = obj.GetComponent<MeshRenderer>();
        view.meshRenderer.sharedMaterial = SkillWorldVisualResources.Material;
        view.meshRenderer.sortingLayerName = RenderSortingLayerNames.EffectsBack_3;
        view.meshRenderer.sortingOrder = sortingOrder;
        obj.SetActive(false);
        return view;
    }

    /// <summary>按顺时针显现进度绘制具有固定段宽和间隙的分段圆环。</summary>
    public void ShowSegmented(
        Vector3 position,
        float radius,
        float width,
        int segmentCount,
        float segmentDegrees,
        float gapDegrees,
        float rotationDegrees,
        float reveal,
        Color color)
    {
        Begin(position);
        float remainingDegrees = Mathf.Clamp01(reveal) * Mathf.Max(0, segmentCount) * segmentDegrees;
        for (int i = 0; i < segmentCount && remainingDegrees > 0.001f; i++)
        {
            float visibleDegrees = Mathf.Min(segmentDegrees, remainingDegrees);
            float start = 90f + rotationDegrees - i * (segmentDegrees + gapDegrees);
            AddArc(new WorldArcBand(radius, width, start, -visibleDegrees), color);
            remainingDegrees -= visibleDegrees;
        }
        Commit(color.a > 0.001f);
    }

    /// <summary>把多段不同半径的圆弧合并到一个网格中绘制。</summary>
    public void ShowBands(Vector3 position, IReadOnlyList<WorldArcBand> bands, Color color)
    {
        Begin(position);
        for (int i = 0; i < bands.Count; i++) AddArc(bands[i], color);
        Commit(color.a > 0.001f);
    }

    /// <summary>隐藏视图并清空上一帧几何。</summary>
    public void Hide()
    {
        if (mesh != null) mesh.Clear();
        gameObject.SetActive(false);
    }

    /// <summary>开始构建一帧网格并复用现有托管缓冲。</summary>
    private void Begin(Vector3 position)
    {
        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        vertices.Clear();
        uv.Clear();
        colors.Clear();
        triangles.Clear();
    }

    /// <summary>把一段环形带离散成具有恒定宽度的三角形条带。</summary>
    private void AddArc(in WorldArcBand band, Color color)
    {
        if (band.Radius <= 0f || band.Width <= 0f || Mathf.Abs(band.SpanDegrees) <= 0.01f) return;
        int subdivisions = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(band.SpanDegrees) / DegreesPerSubdivision));
        float inner = Mathf.Max(0.001f, band.Radius - band.Width * 0.5f);
        float outer = band.Radius + band.Width * 0.5f;
        for (int i = 0; i <= subdivisions; i++)
        {
            float t = i / (float)subdivisions;
            float angle = (band.StartDegrees + band.SpanDegrees * t) * Mathf.Deg2Rad;
            Vector3 radial = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            int vertex = vertices.Count;
            vertices.Add(radial * inner);
            vertices.Add(radial * outer);
            uv.Add(new Vector2(t, 0f));
            uv.Add(new Vector2(t, 1f));
            colors.Add(color);
            colors.Add(color);
            if (i == subdivisions) continue;
            triangles.Add(vertex);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 2);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 3);
            triangles.Add(vertex + 2);
        }
    }

    /// <summary>提交当前网格并用属性块设置白纹理，保留逐顶点透明度。</summary>
    private void Commit(bool visible)
    {
        mesh.Clear();
        if (vertices.Count > 0)
        {
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
        }
        propertyBlock ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture("_MainTex", Texture2D.whiteTexture);
        propertyBlock.SetColor("_Color", Color.white);
        meshRenderer.SetPropertyBlock(propertyBlock);
        gameObject.SetActive(visible && vertices.Count > 0);
    }
}

/// <summary>SkillLib 世界视觉共用的透明材质。</summary>
internal static class SkillWorldVisualResources
{
    private static Material material;

    public static Material Material
    {
        get
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            material = new Material(shader)
            {
                name = "Cultiway_SkillWorldVisualMaterial",
                hideFlags = HideFlags.DontSave
            };
            return material;
        }
    }
}
