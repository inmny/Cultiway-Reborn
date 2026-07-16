using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>直接读取剑阵执行状态，绘制旋转剑圈、持续穿刺、残影、换位归阵和收阵。</summary>
public sealed class ArtifactSwordArrayVisualCue : IArtifactVisualCue
{
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public int max_count = ArtifactSwordArrayExecution.MaxBladeCount;
    public float formation_size_ratio = 0.24f;
    public float attack_size_ratio = 0.95f;
    public float alpha = 0.84f;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        if (context.artifact.IsNull || !context.artifact.TryGetComponent(out ItemShape itemShape))
        {
            return ArtifactEmptyVisualLease.Instance;
        }
        Sprite sprite = ((ArtifactShapeAsset)itemShape.Type).GetWorldSprite(context.artifact);
        if (sprite == null) return ArtifactEmptyVisualLease.Instance;

        SwordArrayLease lease = new(this, sprite);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class SwordArrayLease : IArtifactVisualLease
    {
        private readonly ArtifactSwordArrayVisualCue cue;
        private readonly Sprite sprite;
        private readonly List<ArtifactSwordArrayBladeView> views = new();

        public SwordArrayLease(ArtifactSwordArrayVisualCue cue, Sprite sprite)
        {
            this.cue = cue;
            this.sprite = sprite;
        }

        public bool IsAlive => views.Count > 0 && views[0].gameObject.activeSelf;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            if (!TryResolveState(context, out ArtifactSwordArrayExecutionState state))
            {
                Resize(0);
                return;
            }
            Resize(Mathf.Clamp(state.blades.Length, 1, cue.max_count));
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!TryResolveState(context, out ArtifactSwordArrayExecutionState state) || state.blades.Length == 0)
                return;
            if (views.Count != Mathf.Min(state.blades.Length, cue.max_count))
            {
                Resize(Mathf.Clamp(state.blades.Length, 1, cue.max_count));
            }

            float time = (float)now;
            float activeWorldSize = ResolveActiveWorldSize(context);
            Vector3 collapseCenter = ResolveCollapseCenter(context);
            float collapseStart = state.started_at + state.duration - state.collapse_duration;
            float collapseProgress = state.collapse_duration > 0f
                ? Mathf.Clamp01((time - collapseStart) / state.collapse_duration)
                : 0f;
            float collapseEased = collapseProgress * collapseProgress;
            float visibility = cue.alpha * Mathf.Clamp01(0.35f + context.intensity * 0.65f) *
                               (1f - collapseProgress);

            for (int i = 0; i < views.Count; i++)
            {
                ArtifactSwordArrayBladeState blade = state.blades[i];
                Vector2 position = Vector2.LerpUnclamped(blade.position, collapseCenter, collapseEased);
                Vector2 previous = Vector2.LerpUnclamped(blade.previous_position, collapseCenter, collapseEased);
                Vector2 direction = collapseProgress > 0f
                    ? Normalize(collapseCenter - (Vector3)blade.position, blade.direction)
                    : blade.direction;
                float worldSize = ResolveWorldSize(cue, blade, activeWorldSize, time) *
                                  Mathf.Lerp(1f, 0.18f, collapseEased);
                float formingAlpha = blade.phase == ArtifactSwordArrayBladePhase.Forming
                    ? Mathf.Clamp01((time - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration))
                    : 1f;
                views[i].Show(
                    sprite,
                    new Vector3(position.x, position.y, i * 0.00015f),
                    previous,
                    direction,
                    worldSize,
                    visibility * formingAlpha,
                    cue.color_role,
                    context.theme,
                    blade.phase,
                    blade.hit_at,
                    time);
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
                views.Add(ArtifactSwordArrayVisualPool.Get());
            }
            while (views.Count > targetCount)
            {
                int index = views.Count - 1;
                views[index].Return();
                views.RemoveAt(index);
            }
        }

        private static bool TryResolveState(
            ArtifactAbilityVisualContext context,
            out ArtifactSwordArrayExecutionState state)
        {
            Entity execution = context.runtime.active_execution;
            if (execution.IsAvailable() && execution.TryGetComponent(out state) && state.blades != null)
            {
                return true;
            }
            state = default;
            return false;
        }

        private static Vector3 ResolveCollapseCenter(ArtifactAbilityVisualContext context)
        {
            if (!context.artifact.IsNull && context.artifact.TryGetComponent(out Position position))
            {
                return position.value;
            }
            return context.position;
        }

        private static float ResolveActiveWorldSize(ArtifactAbilityVisualContext context)
        {
            Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
            return ArtifactManifestationTools.ResolveActiveWorldSize(context.artifact, controller);
        }

        private static float ResolveWorldSize(
            ArtifactSwordArrayVisualCue cue,
            ArtifactSwordArrayBladeState blade,
            float activeWorldSize,
            float now)
        {
            float formationSize = cue.formation_size_ratio * activeWorldSize;
            float attackSize = cue.attack_size_ratio * activeWorldSize;
            float progress = Mathf.Clamp01(
                (now - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration));
            return blade.phase switch
            {
                ArtifactSwordArrayBladePhase.Launching => Mathf.Lerp(
                    formationSize,
                    attackSize,
                    Mathf.SmoothStep(0f, 1f, progress * 3f)),
                ArtifactSwordArrayBladePhase.Piercing => attackSize,
                ArtifactSwordArrayBladePhase.Returning => Mathf.Lerp(
                    attackSize,
                    formationSize,
                    Mathf.SmoothStep(0f, 1f, progress)),
                _ => formationSize,
            };
        }

        private static Vector2 Normalize(Vector2 value, Vector2 fallback)
        {
            if (value.sqrMagnitude > 0.0001f) return value.normalized;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.up;
        }
    }
}

internal static class ArtifactSwordArrayVisualPool
{
    private static MonoObjPool<ArtifactSwordArrayBladeView> pool;
    private static Transform worldRoot;

    internal static ArtifactSwordArrayBladeView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null && worldRoot == World.world.transform) return;
        worldRoot = World.world.transform;
        GameObject root = new("artifact_sword_array_visuals");
        root.transform.SetParent(worldRoot, false);

        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactSwordArrayBladeView));
        ArtifactSwordArrayBladeView prefab = prefabObject.AddComponent<ArtifactSwordArrayBladeView>();
        prefab.main_renderer = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "blade",
            RenderSortingLayerNames.EffectsTop_5,
            -2);
        prefab.near_afterimage = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "afterimage_near",
            RenderSortingLayerNames.EffectsTop_5,
            -3);
        prefab.far_afterimage = ArtifactVisualCueTools.AddSprite(
            prefab.transform,
            "afterimage_far",
            RenderSortingLayerNames.EffectsTop_5,
            -4);
        pool = new MonoObjPool<ArtifactSwordArrayBladeView>(
            prefab,
            root.transform,
            view => view.pool = pool,
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }
}

internal sealed class ArtifactSwordArrayBladeView : MonoBehaviour
{
    internal MonoObjPool<ArtifactSwordArrayBladeView> pool;
    public SpriteRenderer main_renderer;
    public SpriteRenderer near_afterimage;
    public SpriteRenderer far_afterimage;

    internal void Show(
        Sprite sprite,
        Vector3 position,
        Vector2 previous,
        Vector2 direction,
        float worldSize,
        float alpha,
        ArtifactVisualColorRole colorRole,
        ArtifactVisualTheme theme,
        ArtifactSwordArrayBladePhase phase,
        float hitAt,
        float now)
    {
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float hitProgress = hitAt > 0f ? Mathf.Clamp01((now - hitAt) / 0.14f) : 1f;
        float hitPulse = hitAt > 0f && hitProgress < 1f
            ? 1f + Mathf.Sin(hitProgress * Mathf.PI) * 0.38f
            : 1f;
        Color mainColor = ArtifactVisualCueTools.ResolveColor(theme, colorRole);
        mainColor.a = alpha;
        SetRenderer(main_renderer, sprite, position, rotation, worldSize * hitPulse, mainColor);

        bool moving = phase is ArtifactSwordArrayBladePhase.Launching or
            ArtifactSwordArrayBladePhase.Piercing or ArtifactSwordArrayBladePhase.Returning;
        if (!moving || alpha <= 0.001f)
        {
            near_afterimage.enabled = false;
            far_afterimage.enabled = false;
            return;
        }

        Vector2 motion = (Vector2)position - previous;
        Vector2 trailDirection = motion.sqrMagnitude > 0.0001f ? motion.normalized : direction;
        float spacing = worldSize * (phase == ArtifactSwordArrayBladePhase.Piercing ? 0.82f : 0.56f);
        Color nearColor = theme.glow;
        nearColor.a = alpha * 0.3f;
        Color farColor = theme.primary;
        farColor.a = alpha * 0.12f;
        SetRenderer(
            near_afterimage,
            sprite,
            position - (Vector3)(trailDirection * spacing),
            rotation,
            worldSize * 0.86f,
            nearColor);
        SetRenderer(
            far_afterimage,
            sprite,
            position - (Vector3)(trailDirection * spacing * 1.85f),
            rotation,
            worldSize * 0.7f,
            farColor);
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        ResetRenderer(main_renderer);
        ResetRenderer(near_afterimage);
        ResetRenderer(far_afterimage);
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }

    private static void SetRenderer(
        SpriteRenderer renderer,
        Sprite sprite,
        Vector3 position,
        float rotation,
        float worldSize,
        Color color)
    {
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        renderer.sprite = sprite;
        renderer.transform.position = position;
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        renderer.transform.localScale = Vector3.one * (worldSize / Mathf.Max(spriteSize, 0.01f));
        renderer.color = color;
        renderer.enabled = color.a > 0.001f;
    }

    private static void ResetRenderer(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        renderer.enabled = false;
        renderer.sprite = null;
        renderer.transform.localPosition = Vector3.zero;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = Vector3.one;
    }
}
