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
public sealed class ArtifactProjectionVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public Func<ArtifactAbilityVisualContext, float> ResolveWorldSize;
    public Vector3 offset;
    public float scale = 1f;
    public float alpha = 0.58f;
    public float start_scale = 0.45f;
    public float end_scale = 1f;
    public float rotation_speed;
    public float pulse_amplitude = 0.04f;
    public float pulse_speed = 4f;
    public bool fade_out = true;
    public bool follow_artifact_rotation;
    public bool match_actor_scale;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        Sprite sprite = ResolveSprite(context.artifact);
        if (sprite == null) return ArtifactEmptyVisualLease.Instance;
        ArtifactProjectionVisualView view = ArtifactProjectionVisualPool.Get();
        view.sprite_renderer.sprite = sprite;
        ProjectionLease lease = new(this, view);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private static Sprite ResolveSprite(Entity artifact)
    {
        if (artifact.IsNull || !artifact.TryGetComponent(out ItemShape itemShape)) return null;
        return ArtifactManifestationTools.ResolveWorldSprite(artifact, true);
    }

    private sealed class ProjectionLease : IArtifactVisualLease
    {
        private readonly ArtifactProjectionVisualCue cue;
        private ArtifactProjectionVisualView view;
        private double startedAt;
        private double expiresAt;

        public ProjectionLease(ArtifactProjectionVisualCue cue, ArtifactProjectionVisualView view)
        {
            this.cue = cue;
            this.view = view;
        }

        public bool IsAlive => view != null && view.gameObject.activeSelf && view.sprite_renderer.sprite != null;

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
            float worldSize = cue.ResolveWorldSize?.Invoke(context) ?? ResolveArtifactWorldSize(context, actorScale);
            float rotation = cue.follow_artifact_rotation && context.artifact.HasComponent<Rotation>()
                ? context.artifact.GetComponent<Rotation>().z
                : Mathf.Atan2(context.direction.y, context.direction.x) * Mathf.Rad2Deg;
            rotation += (float)(now - startedAt) * cue.rotation_speed;
            view.Show(position, worldSize * cue.scale, rotation, cue, context, (float)(now - startedAt), progress);
        }

        public void End()
        {
            if (view == null) return;
            view.Return();
            view = null;
        }

        private static float ResolveArtifactWorldSize(ArtifactAbilityVisualContext context, float actorScale)
        {
            if (!context.artifact.IsNull && context.artifact.TryGetComponent(out ArtifactManifestation manifestation))
            {
                return manifestation.world_size;
            }
            return actorScale * 0.6f;
        }
    }
}

internal static class ArtifactProjectionVisualPool
{
    private static MonoObjPool<ArtifactProjectionVisualView> pool;
    private static Transform worldRoot;

    internal static ArtifactProjectionVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_projection_visuals");
        root.transform.SetParent(worldRoot, false);
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactProjectionVisualView));
        ArtifactProjectionVisualView prefab = prefabObject.AddComponent<ArtifactProjectionVisualView>();
        prefab.sprite_renderer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "projection",
            RenderSortingLayerNames.EffectsTop_5,
            -2);
        pool = new MonoObjPool<ArtifactProjectionVisualView>(
            prefab,
            root.transform,
            view => view.pool = pool,
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactProjectionVisualView : MonoBehaviour
{
    internal MonoObjPool<ArtifactProjectionVisualView> pool;
    public SpriteRenderer sprite_renderer;

    internal void Show(
        Vector3 position,
        float worldSize,
        float rotation,
        ArtifactProjectionVisualCue cue,
        ArtifactAbilityVisualContext context,
        float elapsed,
        float progress)
    {
        Sprite sprite = sprite_renderer.sprite;
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float transition = Mathf.Lerp(cue.start_scale, cue.end_scale, progress);
        float pulse = 1f + Mathf.Sin(elapsed * cue.pulse_speed) * cue.pulse_amplitude;
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        transform.localScale = Vector3.one * (worldSize / Mathf.Max(spriteSize, 0.01f) * transition * pulse);
        Color color = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
        float fade = cue.fade_out ? 1f - progress : 1f;
        color.a = cue.alpha * fade * Mathf.Clamp01(0.35f + context.intensity * 0.65f);
        sprite_renderer.color = color;
        sprite_renderer.enabled = color.a > 0.001f;
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        sprite_renderer.enabled = false;
        sprite_renderer.sprite = null;
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }
}

/// <summary>
/// 以法器世界贴图生成一组环绕投影。它只复制表现，不创建玩法实体，适用于分光、伴生珠和护体阵列。
/// </summary>
public sealed class ArtifactOrbitProjectionVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Controller;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public Func<ArtifactAbilityVisualContext, int> ResolveCount;
    public Func<ArtifactAbilityVisualContext, float> ResolveRadius;
    public Func<ArtifactAbilityVisualContext, float> ResolveWorldSize;
    public Vector3 offset;
    public int count = 3;
    public float radius = 0.8f;
    public float vertical_ratio = 0.42f;
    public float angular_speed = 90f;
    public float rotation_offset = -90f;
    public float bob_amplitude = 0.04f;
    public float bob_speed = 3f;
    public float alpha = 0.62f;
    public float pulse_amplitude = 0.04f;
    public float pulse_speed = 4f;
    public bool radial_facing = true;
    public bool match_actor_scale = true;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        if (context.artifact.IsNull || !context.artifact.TryGetComponent(out ItemShape itemShape))
        {
            return ArtifactEmptyVisualLease.Instance;
        }
        Sprite sprite = ArtifactManifestationTools.ResolveWorldSprite(context.artifact, true);
        if (sprite == null) return ArtifactEmptyVisualLease.Instance;

        OrbitLease lease = new(this, sprite);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class OrbitLease : IArtifactVisualLease
    {
        private readonly ArtifactOrbitProjectionVisualCue cue;
        private readonly Sprite sprite;
        private readonly List<ArtifactProjectionVisualView> views = new();
        private readonly ArtifactProjectionVisualCue projectionStyle;
        private double startedAt;
        private double expiresAt;

        public OrbitLease(ArtifactOrbitProjectionVisualCue cue, Sprite sprite)
        {
            this.cue = cue;
            this.sprite = sprite;
            projectionStyle = new ArtifactProjectionVisualCue
            {
                color_role = cue.color_role,
                alpha = cue.alpha,
                start_scale = 1f,
                end_scale = 1f,
                pulse_amplitude = cue.pulse_amplitude,
                pulse_speed = cue.pulse_speed,
                fade_out = false,
            };
        }

        public bool IsAlive => views.Count > 0 && views[0].gameObject.activeSelf;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            startedAt = now;
            expiresAt = duration > 0f ? now + duration : 0d;
            Resize(Mathf.Clamp(cue.ResolveCount?.Invoke(context) ?? cue.count, 1, 16));
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!IsAlive ||
                !ArtifactVisualCueTools.TryResolve(context, cue.anchor, cue.offset, cue.match_actor_scale,
                    out Vector3 center)) return;

            float elapsed = (float)(now - startedAt);
            float duration = expiresAt > startedAt ? (float)(expiresAt - startedAt) : 0f;
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;
            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            float orbitRadius = Mathf.Max(0.05f, cue.ResolveRadius?.Invoke(context) ?? cue.radius) * actorScale;
            float worldSize = cue.ResolveWorldSize?.Invoke(context) ?? actorScale * 0.62f;
            float phase = (context.artifact.Id % 17) * 13f;

            for (int i = 0; i < views.Count; i++)
            {
                float angle = phase + elapsed * cue.angular_speed + i * 360f / views.Count;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = center + new Vector3(
                    Mathf.Cos(radians) * orbitRadius,
                    Mathf.Sin(radians) * orbitRadius * cue.vertical_ratio +
                    Mathf.Sin(elapsed * cue.bob_speed + i * 1.7f) * cue.bob_amplitude * actorScale,
                    i * 0.0005f);
                float rotation = cue.radial_facing ? angle + cue.rotation_offset : cue.rotation_offset;
                views[i].Show(position, worldSize, rotation, projectionStyle, context, elapsed, progress);
            }
        }

        public void End()
        {
            Resize(0);
        }

        private void Resize(int targetCount)
        {
            while (views.Count < targetCount)
            {
                ArtifactProjectionVisualView view = ArtifactProjectionVisualPool.Get();
                view.sprite_renderer.sprite = sprite;
                views.Add(view);
            }
            while (views.Count > targetCount)
            {
                int index = views.Count - 1;
                views[index].Return();
                views.RemoveAt(index);
            }
        }
    }
}
