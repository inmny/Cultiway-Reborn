using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>连接两个语义锚点的光束；通过曲率和波动参数也可表现锁链、灵丝或能量牵引。</summary>
public class ArtifactBeamVisualCue : IArtifactVisualCue
{
    public string style_key = ArtifactVfxStyles.Arcane;
    public ArtifactVisualAnchorRef from = ArtifactVisualAnchorKind.Artifact;
    public ArtifactVisualAnchorRef to = ArtifactVisualAnchorKind.Target;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Primary;
    public Vector3 from_offset;
    public Vector3 to_offset;
    public float width = 0.045f;
    public float glow_width_multiplier = 2.4f;
    public float alpha = 0.9f;
    public float glow_alpha = 0.32f;
    public float fallback_length = 1f;
    public float curvature;
    public float wave_amplitude;
    public float wave_frequency = 2f;
    public float wave_speed = 5f;
    public float width_pulse_amplitude = 0.08f;
    public float width_pulse_speed = 6f;
    public bool match_actor_scale = true;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ArtifactBeamVisualView view = ArtifactBeamVisualPool.Get();
        BeamLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class BeamLease : IArtifactVisualLease
    {
        private readonly ArtifactBeamVisualCue cue;
        private ArtifactBeamVisualView view;
        private double startedAt;

        public BeamLease(ArtifactBeamVisualCue cue, ArtifactBeamVisualView view)
        {
            this.cue = cue;
            this.view = view;
        }

        public bool IsAlive => view != null && view.gameObject.activeSelf;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            startedAt = now;
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!IsAlive ||
                !ArtifactVisualCueTools.TryResolve(context, cue.from, cue.from_offset, cue.match_actor_scale, out Vector3 start) ||
                !ArtifactVisualCueTools.TryResolve(context, cue.to, cue.to_offset, cue.match_actor_scale, out Vector3 end))
            {
                return;
            }

            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            if ((end - start).sqrMagnitude < 0.0001f)
            {
                end = start + ArtifactVisualCueTools.ResolveDirection(context) * cue.fallback_length * actorScale;
            }
            view.Show(start, end, cue, context, (float)(now - startedAt), actorScale);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }
    }
}

/// <summary>默认带下垂和轻微波动的光束配置，用于灵力牵引、锁链和绑定关系。</summary>
public sealed class ArtifactTetherVisualCue : ArtifactBeamVisualCue
{
    public ArtifactTetherVisualCue()
    {
        curvature = -0.16f;
        wave_amplitude = 0.025f;
        wave_frequency = 1.5f;
        width = 0.032f;
    }
}

internal static class ArtifactBeamVisualPool
{
    private static MonoObjPool<ArtifactBeamVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactBeamVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_beam_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactBeamVisualView));
        ArtifactBeamVisualView prefab = prefabObject.AddComponent<ArtifactBeamVisualView>();
        prefab.glow_mesh = new Mesh { name = "ArtifactBeamGlow" };
        prefab.core_mesh = new Mesh { name = "ArtifactBeamCore" };
        prefab.glow = ArtifactVisualCueTools.AddMesh(
            prefab.transform,
            "glow",
            RenderSortingLayerNames.EffectsTop_5,
            -1,
            out prefab.glow_filter);
        prefab.core = ArtifactVisualCueTools.AddMesh(
            prefab.transform,
            "core",
            RenderSortingLayerNames.EffectsTop_5,
            0,
            out prefab.core_filter);
        prefab.glow_filter.sharedMesh = prefab.glow_mesh;
        prefab.core_filter.sharedMesh = prefab.core_mesh;
        prefab.start_cap = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "start_cap",
            RenderSortingLayerNames.EffectsTop_5,
            1);
        prefab.end_cap = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "end_cap",
            RenderSortingLayerNames.EffectsTop_5,
            1);
        pool = new MonoObjPool<ArtifactBeamVisualView>(
            prefab,
            root.transform,
            view =>
            {
                view.pool = pool;
                view.glow_mesh = UnityEngine.Object.Instantiate(prefab.glow_mesh);
                view.core_mesh = UnityEngine.Object.Instantiate(prefab.core_mesh);
                view.glow_filter.sharedMesh = view.glow_mesh;
                view.core_filter.sharedMesh = view.core_mesh;
            },
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactBeamVisualView : MonoBehaviour
{
    private const int CurvedSegments = 20;
    internal MonoObjPool<ArtifactBeamVisualView> pool;
    public Mesh glow_mesh;
    public Mesh core_mesh;
    public MeshFilter glow_filter;
    public MeshFilter core_filter;
    public MeshRenderer glow;
    public MeshRenderer core;
    public SpriteRenderer start_cap;
    public SpriteRenderer end_cap;
    private MaterialPropertyBlock glowBlock;
    private MaterialPropertyBlock coreBlock;
    private ArtifactVfxPathBuffer pathBuffer;

    internal void Show(
        Vector3 start,
        Vector3 end,
        ArtifactBeamVisualCue cue,
        ArtifactAbilityVisualContext context,
        float elapsed,
        float actorScale)
    {
        Vector3 delta = end - start;
        Vector3 normal = new(-delta.y, delta.x, 0f);
        if (normal.sqrMagnitude > 0.0001f) normal.Normalize();
        int count = Mathf.Abs(cue.curvature) > 0.0001f || cue.wave_amplitude > 0.0001f
            ? CurvedSegments
            : 2;
        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float bend = Mathf.Sin(t * Mathf.PI) * cue.curvature;
            float wave = Mathf.Sin((t * cue.wave_frequency - elapsed * cue.wave_speed) * Mathf.PI * 2f) *
                         Mathf.Sin(t * Mathf.PI) * cue.wave_amplitude;
            points[i] = Vector3.Lerp(start, end, t) + normal * (bend + wave) * actorScale;
        }

        float intensity = Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        float pulse = 1f + Mathf.Sin(elapsed * cue.width_pulse_speed) * cue.width_pulse_amplitude;
        float resolvedWidth = cue.width * actorScale * pulse;
        Color coreColor = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
        Color glowColor = context.theme.glow;
        ArtifactVfxPathStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Path;
        ArtifactPathTextureSet textures = ArtifactVfxTextureLibrary.GetPath(cue.style_key);
        pathBuffer ??= new ArtifactVfxPathBuffer();
        ArtifactVfxPathMesh.Build(
            core_mesh,
            pathBuffer,
            points,
            resolvedWidth,
            style,
            elapsed,
            coreColor,
            cue.alpha * intensity,
            trail: false);
        ArtifactVfxPathMesh.Build(
            glow_mesh,
            pathBuffer,
            points,
            resolvedWidth * cue.glow_width_multiplier,
            style,
            elapsed,
            glowColor,
            cue.glow_alpha * style.EdgeGlow * intensity,
            trail: false);
        ArtifactVfxPathMesh.ApplyTexture(core, textures.Core, ref coreBlock);
        ArtifactVfxPathMesh.ApplyTexture(glow, textures.Glow, ref glowBlock);

        Vector3 startDirection = points[1] - points[0];
        Vector3 endDirection = points[^1] - points[^2];
        coreColor.a = cue.alpha * intensity;
        glowColor.a = cue.alpha * 0.82f * intensity;
        ArtifactVfxPathMesh.ShowCap(
            start_cap,
            textures.Cap,
            points[0],
            startDirection,
            resolvedWidth * 2.6f,
            coreColor,
            reverse: true);
        ArtifactVfxPathMesh.ShowCap(
            end_cap,
            textures.Cap,
            points[^1],
            endDirection,
            resolvedWidth * 3.2f,
            glowColor,
            reverse: false);
    }

    internal void ResetView()
    {
        core.enabled = false;
        glow.enabled = false;
        start_cap.enabled = false;
        end_cap.enabled = false;
        core_mesh.Clear();
        glow_mesh.Clear();
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }

}

/// <summary>记录移动锚点历史的拖尾；宽度较大时可直接作为丝带、披帛或能量尾迹使用。</summary>
