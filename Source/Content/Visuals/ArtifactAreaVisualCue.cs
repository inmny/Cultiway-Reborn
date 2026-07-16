using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;
public sealed class ArtifactAreaVisualCue : IArtifactVisualCue
{
    public string style_key = ArtifactVfxStyles.Arcane;
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    public Vector3 offset;
    public float line_alpha = 0.55f;
    public float fill_alpha = 0.06f;
    public float line_width = 0.08f;
    public float inner_radius_ratio = 0.72f;
    public float inner_rotation_speed = 18f;
    public float pulse_amplitude = 0.025f;
    public float pulse_speed = 3f;
    public float start_scale = 1f;
    public float end_scale = 1f;
    public bool fade_out;
    public bool show_inner_ring = true;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ArtifactAreaVisualView view = ArtifactAreaVisualPool.Get();
        AreaLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    internal Color ResolveColor(ArtifactVisualTheme theme)
    {
        return color_role switch
        {
            ArtifactVisualColorRole.Primary => theme.primary,
            ArtifactVisualColorRole.Secondary => theme.secondary,
            ArtifactVisualColorRole.Glow => theme.glow,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private sealed class AreaLease : IArtifactVisualLease
    {
        private readonly ArtifactAreaVisualCue cue;
        private ArtifactAreaVisualView view;
        private double startedAt;
        private double expiresAt;

        public AreaLease(ArtifactAreaVisualCue cue, ArtifactAreaVisualView view)
        {
            this.cue = cue;
            this.view = view;
        }

        public bool IsAlive => view != null && view.gameObject.activeSelf;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            startedAt = now;
            expiresAt = duration > 0f ? now + duration : 0d;
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!IsAlive ||
                !ArtifactAbilityVisuals.TryResolveAnchorPosition(context, cue.anchor, out Vector3 position)) return;

            float duration = expiresAt > startedAt ? (float)(expiresAt - startedAt) : 0f;
            float progress = duration > 0f
                ? Mathf.Clamp01((float)(now - startedAt) / duration)
                : 0f;
            float radius = Mathf.Max(0.05f, cue.ResolveRadius?.Invoke(context) ?? 1f);
            view.Show(position + cue.offset, radius, cue, context, (float)(now - startedAt), progress);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }
    }
}

internal static class ArtifactAreaVisualPool
{
    private static MonoObjPool<ArtifactAreaVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactAreaVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_ability_area_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactAreaVisualView));
        ArtifactAreaVisualView prefab = prefabObject.AddComponent<ArtifactAreaVisualView>();
        prefab.base_layer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "base",
            RenderSortingLayerNames.EffectsBack_3,
            -2);
        prefab.outer_layer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "outer",
            RenderSortingLayerNames.EffectsBack_3,
            -1);
        prefab.glyph_layer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "glyph",
            RenderSortingLayerNames.EffectsBack_3,
            0);
        prefab.nodes = new SpriteRenderer[12];
        for (int i = 0; i < prefab.nodes.Length; i++)
        {
            prefab.nodes[i] = ArtifactVisualCueTools.AddSprite(
                prefab.transform,
                $"node_{i}",
                RenderSortingLayerNames.EffectsBack_3,
                1);
        }
        pool = new MonoObjPool<ArtifactAreaVisualView>(
            prefab,
            root.transform,
            view => view.pool = pool,
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactAreaVisualView : MonoBehaviour
{
    internal MonoObjPool<ArtifactAreaVisualView> pool;
    public SpriteRenderer base_layer;
    public SpriteRenderer outer_layer;
    public SpriteRenderer glyph_layer;
    public SpriteRenderer[] nodes;

    internal void Show(
        Vector3 position,
        float radius,
        ArtifactAreaVisualCue cue,
        ArtifactAbilityVisualContext context,
        float elapsed,
        float progress)
    {
        float transitionScale = Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        float pulse = 1f + Mathf.Sin(elapsed * cue.pulse_speed) * cue.pulse_amplitude;
        float resolvedRadius = radius * transitionScale * pulse;
        float fade = cue.fade_out ? 1f - progress : 1f;
        float intensity = Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface;
        ArtifactSurfaceTextureSet textures = ArtifactVfxTextureLibrary.GetSurface(cue.style_key, 8);
        float phase = (context.artifact.Id % 29) * 0.13f;
        int frame = Mathf.FloorToInt((elapsed + phase) * style.FrameRate) % textures.OuterFrames.Length;
        transform.position = position;
        transform.localScale = Vector3.one * resolvedRadius;

        base_layer.sprite = textures.Base;
        base_layer.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * style.OuterRotation * 0.12f);
        Color baseColor = context.theme.secondary;
        baseColor.a = cue.fill_alpha <= 0f
            ? 0f
            : style.BaseAlpha * Mathf.Clamp01(0.42f + cue.fill_alpha * 7f) * fade * intensity;
        SetSprite(base_layer, baseColor);

        outer_layer.sprite = textures.OuterFrames[frame];
        outer_layer.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * style.OuterRotation);
        Color outerColor = cue.ResolveColor(context.theme);
        outerColor.a = style.EdgeAlpha * cue.line_alpha * fade * intensity;
        SetSprite(outer_layer, outerColor);

        glyph_layer.sprite = textures.GlyphFrames[frame];
        glyph_layer.transform.localScale = Vector3.one * Mathf.Clamp(cue.inner_radius_ratio / 0.72f, 0.35f, 1.15f);
        glyph_layer.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            elapsed * (style.InnerRotation + cue.inner_rotation_speed));
        Color glyphColor = context.theme.glow;
        glyphColor.a = cue.show_inner_ring
            ? style.GlyphAlpha * cue.line_alpha * 0.82f * fade * intensity
            : 0f;
        SetSprite(glyph_layer, glyphColor);

        int nodeCount = Mathf.Clamp(style.NodeCount, 0, nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            SpriteRenderer node = nodes[i];
            if (i >= nodeCount || outerColor.a <= 0.001f)
            {
                node.enabled = false;
                continue;
            }
            float angle = phase * 90f + elapsed * style.OuterRotation * 0.55f + i * 360f / nodeCount;
            float radians = angle * Mathf.Deg2Rad;
            node.sprite = textures.Node;
            node.transform.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 0.88f;
            node.transform.localRotation = Quaternion.Euler(0f, 0f, angle + 45f);
            node.transform.localScale = Vector3.one * (0.14f / Mathf.Max(resolvedRadius, 0.12f));
            Color nodeColor = context.theme.glow;
            nodeColor.a = outerColor.a * (0.45f + Mathf.Sin(elapsed * 5f + i * 1.7f) * 0.2f);
            SetSprite(node, nodeColor);
        }
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        base_layer.enabled = false;
        outer_layer.enabled = false;
        glyph_layer.enabled = false;
        for (int i = 0; i < nodes.Length; i++) nodes[i].enabled = false;
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }

    private static void SetSprite(SpriteRenderer renderer, Color color)
    {
        renderer.color = color;
        renderer.enabled = renderer.sprite != null && color.a > 0.001f;
    }
}
