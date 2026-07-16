using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>连接两个语义锚点的光束；通过曲率和波动参数也可表现锁链、灵丝或能量牵引。</summary>
public class ArtifactTrailVisualCue : IArtifactVisualCue
{
    public string style_key = ArtifactVfxStyles.Arcane;
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.ActiveExecution;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public Vector3 offset;
    public float width = 0.055f;
    public float alpha = 0.82f;
    public float history = 0.28f;
    public float min_distance = 0.025f;
    public int max_points = 24;
    public bool match_actor_scale = true;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ArtifactTrailVisualView view = ArtifactTrailVisualPool.Get();
        TrailLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class TrailLease : IArtifactVisualLease
    {
        private readonly ArtifactTrailVisualCue cue;
        private readonly List<Vector3> points = new();
        private readonly List<double> times = new();
        private ArtifactTrailVisualView view;

        public TrailLease(ArtifactTrailVisualCue cue, ArtifactTrailVisualView view)
        {
            this.cue = cue;
            this.view = view;
        }

        public bool IsAlive => view != null && view.gameObject.activeSelf;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            points.Clear();
            times.Clear();
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!IsAlive ||
                !ArtifactVisualCueTools.TryResolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 position))
            {
                return;
            }

            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            float minDistance = cue.min_distance * actorScale;
            if (points.Count == 0 || (position - points[^1]).sqrMagnitude >= minDistance * minDistance)
            {
                points.Add(position);
                times.Add(now);
            }
            else
            {
                points[^1] = position;
                times[^1] = now;
            }

            while (points.Count > 1 &&
                   (points.Count > Mathf.Max(2, cue.max_points) || now - times[0] > cue.history))
            {
                points.RemoveAt(0);
                times.RemoveAt(0);
            }
            view.Show(points, cue, context, actorScale);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }
    }
}

/// <summary>适合宽幅能量、披帛和扇风轨迹的拖尾预设。</summary>
public sealed class ArtifactRibbonVisualCue : ArtifactTrailVisualCue
{
    public ArtifactRibbonVisualCue()
    {
        style_key = ArtifactVfxStyles.Cloth;
        width = 0.14f;
        alpha = 0.58f;
        history = 0.42f;
        max_points = 32;
    }
}

internal static class ArtifactTrailVisualPool
{
    private static MonoObjPool<ArtifactTrailVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactTrailVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_trail_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactTrailVisualView));
        ArtifactTrailVisualView prefab = prefabObject.AddComponent<ArtifactTrailVisualView>();
        prefab.glow_mesh = new Mesh { name = "ArtifactTrailGlow" };
        prefab.core_mesh = new Mesh { name = "ArtifactTrailCore" };
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
        pool = new MonoObjPool<ArtifactTrailVisualView>(
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

internal sealed class ArtifactTrailVisualView : MonoBehaviour
{
    internal MonoObjPool<ArtifactTrailVisualView> pool;
    public Mesh glow_mesh;
    public Mesh core_mesh;
    public MeshFilter glow_filter;
    public MeshFilter core_filter;
    public MeshRenderer glow;
    public MeshRenderer core;
    private MaterialPropertyBlock glowBlock;
    private MaterialPropertyBlock coreBlock;
    private ArtifactVfxPathBuffer pathBuffer;

    internal void Show(
        IReadOnlyList<Vector3> points,
        ArtifactTrailVisualCue cue,
        ArtifactAbilityVisualContext context,
        float actorScale)
    {
        if (points.Count < 2)
        {
            core.enabled = false;
            glow.enabled = false;
            return;
        }
        float intensity = Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        ArtifactVfxPathStyleDef style = ArtifactVfxStyleCatalog.Get(cue.style_key).Path;
        ArtifactPathTextureSet textures = ArtifactVfxTextureLibrary.GetPath(cue.style_key);
        Color coreColor = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
        Color glowColor = context.theme.glow;
        float elapsed = Time.time;
        pathBuffer ??= new ArtifactVfxPathBuffer();
        ArtifactVfxPathMesh.Build(
            core_mesh,
            pathBuffer,
            points,
            cue.width * actorScale,
            style,
            elapsed,
            coreColor,
            cue.alpha * intensity,
            trail: true);
        ArtifactVfxPathMesh.Build(
            glow_mesh,
            pathBuffer,
            points,
            cue.width * actorScale * 2.2f,
            style,
            elapsed,
            glowColor,
            cue.alpha * style.EdgeGlow * 0.58f * intensity,
            trail: true);
        ArtifactVfxPathMesh.ApplyTexture(core, textures.Core, ref coreBlock);
        ArtifactVfxPathMesh.ApplyTexture(glow, textures.Glow, ref glowBlock);
    }

    internal void ResetView()
    {
        core.enabled = false;
        glow.enabled = false;
        core_mesh.Clear();
        glow_mesh.Clear();
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }
}
