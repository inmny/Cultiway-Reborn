using System.Collections.Generic;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>池化局部粒子和逐帧动画共用的 SpriteRenderer 视图。</summary>
internal sealed class WorldSpriteView
{
    private readonly GameObject gameObject;
    public readonly Transform Transform;
    public readonly SpriteRenderer Renderer;

    private WorldSpriteView(Transform parent)
    {
        gameObject = new GameObject("SkillLocalSprite", typeof(SpriteRenderer));
        Transform = gameObject.transform;
        Transform.SetParent(parent, false);
        Renderer = gameObject.GetComponent<SpriteRenderer>();
        Renderer.sharedMaterial = SkillWorldVisualResources.Material;
        Renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        Renderer.sortingOrder = -5;
        Renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        gameObject.SetActive(false);
    }

    /// <summary>创建一个局部精灵视图。</summary>
    public static WorldSpriteView Create(Transform parent)
    {
        return new WorldSpriteView(parent);
    }

    /// <summary>显示指定精灵并写入全部空间和颜色状态。</summary>
    public void Show(Sprite sprite, Vector3 position, float rotation, float scale, Color color)
    {
        Renderer.sprite = sprite;
        Renderer.color = color;
        Transform.position = position;
        Transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        Transform.localScale = Vector3.one * scale;
        gameObject.SetActive(sprite != null && color.a > 0.001f);
    }

    /// <summary>隐藏视图并移除旧精灵引用。</summary>
    public void Hide()
    {
        Renderer.sprite = null;
        gameObject.SetActive(false);
    }
}

/// <summary>净土法阵从中心扫向实际变化地块的池化窄带网格。</summary>
internal sealed class WorldSweepRenderer
{
    private readonly GameObject gameObject;
    private readonly Mesh mesh;
    private readonly MeshRenderer renderer;
    private readonly List<Vector3> vertices = new(6);
    private readonly List<Vector2> uv = new(6);
    private readonly List<Color> colors = new(6);
    private readonly int[] triangles = { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 };
    private MaterialPropertyBlock propertyBlock;

    private WorldSweepRenderer(Transform parent)
    {
        gameObject = new GameObject("SkillCleanLandSweep", typeof(MeshFilter), typeof(MeshRenderer));
        gameObject.transform.SetParent(parent, false);
        mesh = new Mesh
        {
            name = "SkillCleanLandSweepMesh",
            hideFlags = HideFlags.DontSave
        };
        gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = SkillWorldVisualResources.Material;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsBack_3;
        renderer.sortingOrder = -6;
        gameObject.SetActive(false);
    }

    /// <summary>创建一个窄带扫掠视图。</summary>
    public static WorldSweepRenderer Create(Transform parent)
    {
        return new WorldSweepRenderer(parent);
    }

    /// <summary>绘制从中心逐步抵达目标的尖头窄带。</summary>
    public void Show(Vector3 start, Vector3 end, float progress, float width, Color color)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f || progress <= 0f || color.a <= 0.001f)
        {
            Hide();
            return;
        }
        Vector3 direction = delta / length;
        Vector3 normal = new(-direction.y, direction.x, 0f);
        float visibleLength = length * Mathf.Clamp01(progress);
        Vector3 middle = start + direction * visibleLength * 0.72f;
        Vector3 tip = start + direction * visibleLength;
        float halfWidth = width * 0.5f;

        vertices.Clear();
        vertices.Add(start - normal * halfWidth * 0.3f);
        vertices.Add(start + normal * halfWidth * 0.3f);
        vertices.Add(middle - normal * halfWidth);
        vertices.Add(middle + normal * halfWidth);
        vertices.Add(tip);
        vertices.Add(tip);
        uv.Clear();
        uv.Add(new Vector2(0f, 0f));
        uv.Add(new Vector2(0f, 1f));
        uv.Add(new Vector2(0.72f, 0f));
        uv.Add(new Vector2(0.72f, 1f));
        uv.Add(new Vector2(1f, 0.5f));
        uv.Add(new Vector2(1f, 0.5f));
        colors.Clear();
        Color transparent = color;
        transparent.a = 0f;
        colors.Add(transparent);
        colors.Add(transparent);
        colors.Add(color);
        colors.Add(color);
        colors.Add(transparent);
        colors.Add(transparent);

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0, false);
        mesh.RecalculateBounds();
        propertyBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture("_MainTex", Texture2D.whiteTexture);
        propertyBlock.SetColor("_Color", Color.white);
        renderer.SetPropertyBlock(propertyBlock);
        gameObject.SetActive(true);
    }

    /// <summary>隐藏并清空窄带几何。</summary>
    public void Hide()
    {
        mesh.Clear();
        gameObject.SetActive(false);
    }
}
