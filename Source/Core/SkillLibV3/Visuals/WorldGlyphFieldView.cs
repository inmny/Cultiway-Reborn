using System.Collections.Generic;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>由固定线宽边界和独立图元组成的池化持续法阵视图。</summary>
internal sealed class WorldGlyphFieldView
{
    private readonly GameObject rootObject;
    private readonly Transform root;
    private readonly WorldArcRenderer boundary;
    private readonly Transform primaryRoot;
    private readonly Transform secondaryRoot;
    private readonly Transform inscriptionRoot;
    private readonly List<SpriteRenderer> primaryRenderers = new(12);
    private readonly List<SpriteRenderer> secondaryRenderers = new(12);
    private readonly List<SpriteRenderer> inscriptionRenderers = new(8);
    private SkillWorldVisualProfile profile;
    private Vector3 center;
    private float radius;
    private float pulseElapsed = float.MaxValue;
    private int pulseStartIndex;

    private WorldGlyphFieldView(Transform parent)
    {
        rootObject = new GameObject("SkillGlyphField");
        root = rootObject.transform;
        root.SetParent(parent, false);
        primaryRoot = CreateRoot(root, "SemanticRing");
        secondaryRoot = CreateRoot(root, "InnerStructure");
        inscriptionRoot = CreateRoot(root, "InscriptionRing");
        boundary = WorldArcRenderer.Create(root, "BoundaryArc", -8);
        rootObject.SetActive(false);
    }

    /// <summary>创建一个尚未绑定配置的法阵视图。</summary>
    public static WorldGlyphFieldView Create(Transform parent)
    {
        return new WorldGlyphFieldView(parent);
    }

    /// <summary>绑定一次法阵配置并解析所需图元资源。</summary>
    public void Configure(SkillWorldVisualProfile worldProfile)
    {
        profile = worldProfile;
        EnsureRing(primaryRoot, primaryRenderers, profile.Field.PrimaryRing, "primary");
        EnsureRing(secondaryRoot, secondaryRenderers, profile.Field.SecondaryRing, "secondary");
        EnsureRing(inscriptionRoot, inscriptionRenderers, profile.Field.InscriptionRing, "inscription");
        pulseElapsed = float.MaxValue;
        rootObject.SetActive(true);
    }

    /// <summary>更新法阵出现、旋转、脉冲和消散状态。</summary>
    public void Show(Vector3 worldCenter, float worldRadius, float elapsed, float duration, float deltaTime)
    {
        if (profile?.Field == null) return;
        center = worldCenter;
        radius = worldRadius;
        root.position = center;
        root.rotation = Quaternion.identity;
        root.localScale = Vector3.one;
        bool visible = SkillWorldVisualRuntime.IsVisible(center, radius);
        rootObject.SetActive(visible);
        if (!visible)
        {
            boundary.Hide();
            return;
        }

        SkillFieldVisualProfile field = profile.Field;
        float fade = duration > 0f ? Mathf.Clamp01((duration - elapsed) / 0.25f) : 1f;
        float reveal = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, field.BoundaryDrawDuration));
        Color boundaryColor = field.BoundaryColor;
        boundaryColor.a *= fade;
        boundary.ShowSegmented(
            center,
            radius,
            field.BoundaryWidth,
            field.BoundarySegmentCount,
            field.BoundarySegmentDegrees,
            field.BoundaryGapDegrees,
            0f,
            reveal,
            boundaryColor);

        float slide = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((elapsed - field.BoundaryDrawDuration * 0.35f) / 0.24f));
        pulseElapsed += deltaTime;
        UpdateRing(primaryRoot, primaryRenderers, field.PrimaryRing, elapsed, slide, fade, false, true);
        UpdateRing(secondaryRoot, secondaryRenderers, field.SecondaryRing, elapsed, slide, fade, false, false);
        bool showInscription = radius >= field.InscriptionMinRadius;
        inscriptionRoot.gameObject.SetActive(showInscription);
        if (showInscription)
        {
            UpdateRing(inscriptionRoot, inscriptionRenderers, field.InscriptionRing,
                elapsed, slide, fade, true, false);
        }
    }

    /// <summary>以实际发生变化的位置为起点点亮最近语义图元。</summary>
    public void Pulse(Vector3 position)
    {
        SkillGlyphRingVisualProfile ring = profile?.Field?.PrimaryRing;
        if (ring == null || ring.Count <= 0) return;
        float angle = Mathf.Atan2(position.y - center.y, position.x - center.x) * Mathf.Rad2Deg;
        float ringRotation = primaryRoot.localEulerAngles.z;
        float localAngle = Mathf.Repeat(angle - ringRotation, 360f);
        pulseStartIndex = Mathf.RoundToInt(localAngle / 360f * ring.Count) % ring.Count;
        pulseElapsed = 0f;
    }

    /// <summary>隐藏并释放当前配置状态，供池复用。</summary>
    public void Hide()
    {
        profile = null;
        boundary.Hide();
        rootObject.SetActive(false);
    }

    /// <summary>创建一个不缩放的环形图元父节点。</summary>
    private static Transform CreateRoot(Transform parent, string name)
    {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    /// <summary>确保图元环拥有与配置数量相同的 SpriteRenderer，并绑定对应资源。</summary>
    private static void EnsureRing(
        Transform ringRoot,
        List<SpriteRenderer> renderers,
        SkillGlyphRingVisualProfile ring,
        string namePrefix)
    {
        int count = ring?.Count ?? 0;
        while (renderers.Count < count)
        {
            GameObject obj = new($"{namePrefix}_{renderers.Count:00}", typeof(SpriteRenderer));
            obj.transform.SetParent(ringRoot, false);
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = SkillWorldVisualResources.Material;
            renderer.sortingLayerName = RenderSortingLayerNames.EffectsBack_3;
            renderer.sortingOrder = -7;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            renderers.Add(renderer);
        }
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer renderer = renderers[i];
            bool enabled = i < count && ring.GlyphPaths.Length > 0;
            renderer.gameObject.SetActive(enabled);
            renderer.sprite = enabled
                ? SkillWorldVisualRuntime.ResolveSprite(ring.GlyphPaths[i % ring.GlyphPaths.Length])
                : null;
        }
        ringRoot.gameObject.SetActive(count > 0);
    }

    /// <summary>更新一个图元环的空间位置、朝向、尺寸和成功脉冲。</summary>
    private void UpdateRing(
        Transform ringRoot,
        List<SpriteRenderer> renderers,
        SkillGlyphRingVisualProfile ring,
        float elapsed,
        float slide,
        float fade,
        bool keepGlyphsUpright,
        bool applyPulse)
    {
        if (ring == null || ring.Count <= 0) return;
        float rotation = elapsed * ring.RotationSpeed;
        ringRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);
        float startRatio = Mathf.Max(profile.Field.ClearCenterRatio + 0.04f, ring.RadiusRatio - 0.18f);
        float resolvedRadius = radius * Mathf.Lerp(startRatio, ring.RadiusRatio, slide);
        float worldSize = Mathf.Clamp(radius * ring.SizeRadiusFactor, ring.MinWorldSize, ring.MaxWorldSize);
        for (int i = 0; i < ring.Count; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.sprite == null)
            {
                renderer.enabled = false;
                continue;
            }
            float angle = i * 360f / ring.Count;
            float radians = angle * Mathf.Deg2Rad;
            renderer.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * resolvedRadius,
                Mathf.Sin(radians) * resolvedRadius,
                0f);
            renderer.transform.localRotation = keepGlyphsUpright
                ? Quaternion.Euler(0f, 0f, -rotation)
                : Quaternion.Euler(0f, 0f, angle - 90f);
            float spriteSize = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            float highlight = applyPulse ? ResolvePulse(i, ring.Count) : 0f;
            float scale = worldSize / Mathf.Max(0.001f, spriteSize) * (1f + highlight * 0.22f);
            renderer.transform.localScale = Vector3.one * scale;
            Color color = Color.Lerp(ring.Color, profile.GlowColor, highlight);
            color.a *= fade * Mathf.Lerp(0.72f, 1f, slide);
            renderer.color = color;
            renderer.enabled = color.a > 0.001f;
        }
    }

    /// <summary>按法阵类型解析最近图元的静态高亮或沿环传播的恢复脉冲。</summary>
    private float ResolvePulse(int index, int count)
    {
        if (pulseElapsed > 0.75f || count <= 0) return 0f;
        if (profile.LocalEffect == SkillLocalEffectVisualKind.Rejuvenation)
        {
            float travellingIndex = pulseElapsed / 0.55f * count;
            int offset = (index - pulseStartIndex + count) % count;
            return Mathf.Clamp01(1f - Mathf.Abs(offset - travellingIndex) * 0.8f) *
                   Mathf.Clamp01(1f - pulseElapsed / 0.75f);
        }
        int circularDistance = Mathf.Abs(index - pulseStartIndex);
        circularDistance = Mathf.Min(circularDistance, count - circularDistance);
        return circularDistance == 0 ? Mathf.Clamp01(1f - pulseElapsed / 0.45f) : 0f;
    }
}
