using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>程序化扇形或锥形范围提示，可跟随任意语义锚点和朝向。</summary>
public class ArtifactGlyphVisualCue : IArtifactVisualCue
{
    public string style_key = ArtifactVfxStyles.Arcane;
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    public Vector3 offset;
    public float radius = 1f;
    public float line_width = 0.055f;
    public float alpha = 0.72f;
    public float inner_ratio = 0.62f;
    public float start_scale = 0.2f;
    public float end_scale = 1f;
    public float rotation_speed = 24f;
    public float counter_rotation_ratio = -0.65f;
    public float pulse_amplitude = 0.03f;
    public float pulse_speed = 3f;
    public int sides = 8;
    public bool fade_out;
    public bool match_actor_scale;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ArtifactGlyphVisualView view = ArtifactGlyphVisualPool.Get();
        GlyphLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class GlyphLease : IArtifactVisualLease
    {
        private readonly ArtifactGlyphVisualCue cue;
        private ArtifactGlyphVisualView view;
        private double startedAt;
        private double expiresAt;

        public GlyphLease(ArtifactGlyphVisualCue cue, ArtifactGlyphVisualView view)
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
                !ArtifactVisualCueTools.TryResolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 position))
            {
                return;
            }
            float duration = expiresAt > startedAt ? (float)(expiresAt - startedAt) : 0f;
            float progress = duration > 0f ? Mathf.Clamp01((float)(now - startedAt) / duration) : 0f;
            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            float resolvedRadius = Mathf.Max(0.05f, cue.ResolveRadius?.Invoke(context) ?? cue.radius) * actorScale;
            view.Show(position, resolvedRadius, cue, context, (float)(now - startedAt), progress);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }
    }
}

/// <summary>静态符纹投影的语义别名，默认不旋转且由触发时长淡出。</summary>
public sealed class ArtifactDecalVisualCue : ArtifactGlyphVisualCue
{
    public ArtifactDecalVisualCue()
    {
        rotation_speed = 0f;
        counter_rotation_ratio = 0f;
        fade_out = true;
        start_scale = 0.72f;
    }
}

internal static class ArtifactGlyphVisualPool
{
    private static MonoObjPool<ArtifactGlyphVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactGlyphVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_glyph_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactGlyphVisualView));
        ArtifactGlyphVisualView prefab = prefabObject.AddComponent<ArtifactGlyphVisualView>();
        prefab.outer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "outer",
            RenderSortingLayerNames.EffectsBack_3,
            0);
        prefab.inner = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "inner",
            RenderSortingLayerNames.EffectsBack_3,
            1);
        prefab.motif = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "motif",
            RenderSortingLayerNames.EffectsBack_3,
            2);
        prefab.nodes = new SpriteRenderer[12];
        for (int i = 0; i < prefab.nodes.Length; i++)
        {
            prefab.nodes[i] = ArtifactVisualCueTools.AddSprite(
                prefab.transform,
                $"node_{i}",
                RenderSortingLayerNames.EffectsBack_3,
                3);
        }
        pool = new MonoObjPool<ArtifactGlyphVisualView>(
            prefab,
            root.transform,
            view => view.pool = pool,
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactGlyphVisualView : MonoBehaviour
{
    internal MonoObjPool<ArtifactGlyphVisualView> pool;
    public SpriteRenderer outer;
    public SpriteRenderer inner;
    public SpriteRenderer motif;
    public SpriteRenderer[] nodes;

    internal void Show(
        Vector3 position,
        float radius,
        ArtifactGlyphVisualCue cue,
        ArtifactAbilityVisualContext context,
        float elapsed,
        float progress)
    {
        float transition = Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        float pulse = 1f + Mathf.Sin(elapsed * cue.pulse_speed) * cue.pulse_amplitude;
        float resolvedRadius = radius * transition * pulse;
        transform.position = position;
        transform.localScale = new Vector3(resolvedRadius, resolvedRadius, 1f);

        float fade = cue.fade_out ? 1f - progress : 1f;
        float intensity = Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface;
        ArtifactSurfaceTextureSet textures = ArtifactVfxTextureLibrary.GetSurface(cue.style_key, cue.sides);
        float phase = (context.artifact.Id % 31) * 0.11f;
        int frame = Mathf.FloorToInt((elapsed + phase) * style.FrameRate) % textures.OuterFrames.Length;

        outer.sprite = textures.OuterFrames[frame];
        outer.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (cue.rotation_speed + style.OuterRotation));
        Color color = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
        color.a = style.EdgeAlpha * cue.alpha * fade * intensity;
        SetStyle(outer, color);

        inner.sprite = textures.OuterFrames[(frame + textures.OuterFrames.Length / 2) % textures.OuterFrames.Length];
        inner.transform.localScale = Vector3.one * cue.inner_ratio;
        inner.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            elapsed * (cue.rotation_speed * cue.counter_rotation_ratio + style.InnerRotation));
        Color innerColor = context.theme.glow;
        innerColor.a = style.GlyphAlpha * cue.alpha * 0.7f * fade * intensity;
        SetStyle(inner, innerColor);

        motif.sprite = textures.GlyphFrames[frame];
        motif.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * cue.rotation_speed * 0.24f);
        Color motifColor = context.theme.secondary;
        motifColor.a = style.GlyphAlpha * cue.alpha * fade * intensity;
        SetStyle(motif, motifColor);

        int nodeCount = Mathf.Clamp(style.NodeCount, 0, nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            SpriteRenderer node = nodes[i];
            if (i >= nodeCount || color.a <= 0.001f)
            {
                node.enabled = false;
                continue;
            }
            float angle = phase * 120f + elapsed * style.OuterRotation * 0.7f + i * 360f / nodeCount;
            float radians = angle * Mathf.Deg2Rad;
            node.sprite = textures.Node;
            node.transform.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 0.9f;
            node.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            node.transform.localScale = Vector3.one * (0.13f / Mathf.Max(resolvedRadius, 0.12f));
            Color nodeColor = context.theme.glow;
            nodeColor.a = color.a * (0.42f + Mathf.Sin(elapsed * 6f + i) * 0.18f);
            SetStyle(node, nodeColor);
        }
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        outer.enabled = false;
        inner.enabled = false;
        motif.enabled = false;
        for (int i = 0; i < nodes.Length; i++) nodes[i].enabled = false;
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }

    private static void SetStyle(SpriteRenderer renderer, Color color)
    {
        renderer.color = color;
        renderer.enabled = renderer.sprite != null && color.a > 0.001f;
    }
}

/// <summary>把法器当前世界外观复制为瞬时投影，用于分身、召唤虚影、回响和传送残像。</summary>
