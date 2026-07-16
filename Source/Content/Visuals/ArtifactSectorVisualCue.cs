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
public sealed class ArtifactSectorVisualCue : IArtifactVisualCue
{
    public string style_key = ArtifactVfxStyles.Arcane;
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    public Func<ArtifactAbilityVisualContext, float> ResolveAngle;
    public Vector3 offset;
    public float radius = 1f;
    public float angle = 60f;
    public float line_width = 0.065f;
    public float line_alpha = 0.7f;
    public float fill_alpha = 0.08f;
    public float start_scale = 1f;
    public float end_scale = 1f;
    public float pulse_amplitude = 0.025f;
    public float pulse_speed = 4f;
    public bool fade_out;
    public bool match_actor_scale;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ArtifactSectorVisualView view = ArtifactSectorVisualPool.Get();
        SectorLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class SectorLease : IArtifactVisualLease
    {
        private readonly ArtifactSectorVisualCue cue;
        private ArtifactSectorVisualView view;
        private double startedAt;
        private double expiresAt;

        public SectorLease(ArtifactSectorVisualCue cue, ArtifactSectorVisualView view)
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
            float resolvedAngle = Mathf.Clamp(cue.ResolveAngle?.Invoke(context) ?? cue.angle, 1f, 359f);
            view.Show(
                position,
                ArtifactVisualCueTools.ResolveDirection(context),
                resolvedRadius,
                resolvedAngle,
                cue,
                context,
                (float)(now - startedAt),
                progress);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }
    }
}

internal static class ArtifactSectorVisualPool
{
    private static MonoObjPool<ArtifactSectorVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactSectorVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_sector_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactSectorVisualView));
        ArtifactSectorVisualView prefab = prefabObject.AddComponent<ArtifactSectorVisualView>();
        prefab.mesh = new Mesh { name = "ArtifactSector" };
        prefab.base_layer = ArtifactVisualCueTools.AddMesh(
            prefab.transform,
            "base",
            RenderSortingLayerNames.EffectsBack_3,
            -2,
            out prefab.base_filter);
        prefab.flow_layer = ArtifactVisualCueTools.AddMesh(
            prefab.transform,
            "flow",
            RenderSortingLayerNames.EffectsBack_3,
            -1,
            out prefab.flow_filter);
        prefab.base_filter.sharedMesh = prefab.mesh;
        prefab.flow_filter.sharedMesh = prefab.mesh;
        pool = new MonoObjPool<ArtifactSectorVisualView>(
            prefab,
            root.transform,
            view =>
            {
                view.pool = pool;
                view.mesh = UnityEngine.Object.Instantiate(prefab.mesh);
                view.base_filter.sharedMesh = view.mesh;
                view.flow_filter.sharedMesh = view.mesh;
            },
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactSectorVisualView : MonoBehaviour
{
    private const int ArcSegments = 72;
    internal MonoObjPool<ArtifactSectorVisualView> pool;
    public Mesh mesh;
    public MeshFilter base_filter;
    public MeshFilter flow_filter;
    public MeshRenderer base_layer;
    public MeshRenderer flow_layer;
    private MaterialPropertyBlock baseBlock;
    private MaterialPropertyBlock flowBlock;
    private readonly List<Vector3> vertices = new((ArcSegments + 1) * 2);
    private readonly List<Vector2> uv = new((ArcSegments + 1) * 2);
    private readonly List<int> triangles = new(ArcSegments * 6);

    internal void Show(
        Vector3 position,
        Vector3 direction,
        float radius,
        float angle,
        ArtifactSectorVisualCue cue,
        ArtifactAbilityVisualContext context,
        float elapsed,
        float progress)
    {
        int segments = Mathf.Max(2, Mathf.CeilToInt(ArcSegments * angle / 360f));
        vertices.Clear();
        uv.Clear();
        triangles.Clear();
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float arc = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 radialDirection = new(Mathf.Cos(arc), Mathf.Sin(arc), 0f);
            int vertex = vertices.Count;
            vertices.Add(radialDirection * 0.015f);
            vertices.Add(radialDirection);
            uv.Add(new Vector2(t, 0f));
            uv.Add(new Vector2(t, 1f));
            if (i == segments) continue;
            triangles.Add(vertex);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 2);
            triangles.Add(vertex + 1);
            triangles.Add(vertex + 3);
            triangles.Add(vertex + 2);
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0, false);
        mesh.RecalculateBounds();

        float transition = Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        float pulse = 1f + Mathf.Sin(elapsed * cue.pulse_speed) * cue.pulse_amplitude;
        float resolvedRadius = radius * transition * pulse;
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        transform.localScale = new Vector3(resolvedRadius, resolvedRadius, 1f);

        float fade = cue.fade_out ? 1f - progress : 1f;
        float intensity = Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Surface;
        ArtifactSectorTextureSet textures = ArtifactVfxTextureLibrary.GetSector(cue.style_key);
        float phase = (context.artifact.Id % 23) * 0.17f;
        int frame = Mathf.FloorToInt((elapsed + phase) * style.FrameRate) % textures.FlowFrames.Length;

        baseBlock ??= new MaterialPropertyBlock();
        base_layer.GetPropertyBlock(baseBlock);
        baseBlock.SetTexture("_MainTex", textures.Base);
        Color baseColor = context.theme.secondary;
        baseColor.a = style.BaseAlpha * Mathf.Clamp01(0.35f + cue.fill_alpha * 7f) * fade * intensity;
        baseBlock.SetColor("_Color", baseColor);
        base_layer.SetPropertyBlock(baseBlock);
        base_layer.enabled = baseColor.a > 0.001f;

        flowBlock ??= new MaterialPropertyBlock();
        flow_layer.GetPropertyBlock(flowBlock);
        flowBlock.SetTexture("_MainTex", textures.FlowFrames[frame]);
        Color flowColor = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
        flowColor.a = style.EdgeAlpha * cue.line_alpha * fade * intensity;
        flowBlock.SetColor("_Color", flowColor);
        flow_layer.SetPropertyBlock(flowBlock);
        flow_layer.enabled = flowColor.a > 0.001f;
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        base_layer.enabled = false;
        flow_layer.enabled = false;
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }
}

/// <summary>由同心多边形和交错符线构成的地面法阵，可作为阵纹、封印、结界或召唤落点。</summary>
