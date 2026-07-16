using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

public enum ArtifactVisualColorRole
{
    Primary,
    Secondary,
    Glow,
}

/// <summary>
/// 复用 SkillLibV3 RawAnim 的贴图动画 cue。租约只更新位姿、配色和时长，渲染与回收沿用现有系统。
/// </summary>
public sealed class ArtifactAnimVisualCue : IArtifactVisualCue
{
    public string effect_path;
    public ArtifactVisualAnchorKind anchor = ArtifactVisualAnchorKind.Artifact;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public Vector3 offset;
    public float scale = 0.1f;
    public float frame_interval = 0.1f;
    public float alpha = 1f;
    public float scale_pulse_amplitude;
    public float scale_pulse_speed = 4f;
    public float alpha_pulse_period;
    public float alpha_pulse_floor;
    public bool loop = true;
    public bool match_actor_scale = true;
    public VisualRotation? visual_rotation = VisualRotation.FixedUpright();

    private Sprite[] frames;

    public ArtifactAnimVisualCue(string effectPath)
    {
        effect_path = effectPath;
    }

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        frames ??= Core.SkillLibV3.SkillEntityAsset.LoadOrderedFrames(effect_path);
        Color tint = ResolveColor(context.theme, color_role);
        tint.a *= alpha;
        float lifetime = duration > 0f ? duration : float.MaxValue;
        Entity entity = ModClass.I.SkillV3.SpawnAnim(
            frames,
            context.position,
            ResolveDirection(context),
            scale,
            tint,
            frame_interval,
            loop,
            lifetime,
            visual_rotation);
        AnimLease lease = new(this, entity);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private static Color ResolveColor(ArtifactVisualTheme theme, ArtifactVisualColorRole role)
    {
        return role switch
        {
            ArtifactVisualColorRole.Primary => theme.primary,
            ArtifactVisualColorRole.Secondary => theme.secondary,
            ArtifactVisualColorRole.Glow => theme.glow,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static Vector3 ResolveDirection(ArtifactAbilityVisualContext context)
    {
        if (context.direction.sqrMagnitude >= 0.0001f) return context.direction.normalized;
        if (!context.artifact.IsNull && context.artifact.HasComponent<Rotation>())
        {
            Vector3 direction = context.artifact.GetComponent<Rotation>().value;
            if (direction.sqrMagnitude >= 0.0001f) return direction;
        }
        return Vector3.right;
    }

    private sealed class AnimLease : IArtifactVisualLease
    {
        private readonly ArtifactAnimVisualCue cue;
        private readonly Entity entity;
        private double startedAt;

        public AnimLease(ArtifactAnimVisualCue cue, Entity entity)
        {
            this.cue = cue;
            this.entity = entity;
        }

        public bool IsAlive => !entity.IsNull &&
                               entity.HasComponent<Position>() &&
                               !entity.Tags.Has<TagRecycle>();

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            if (!IsAlive) return;
            startedAt = now;
            entity.GetComponent<AliveTimer>().value = 0f;
            entity.GetComponent<AliveTimeLimit>().value = duration > 0f ? duration : float.MaxValue;
            ref AnimData animation = ref entity.GetComponent<AnimData>();
            animation.frame_idx = 0;
            animation.frame_timer = 0f;
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!IsAlive ||
                !ArtifactAbilityVisuals.TryResolveAnchorPosition(context, cue.anchor, out Vector3 position)) return;

            entity.GetComponent<Position>().value = position + cue.offset * ArtifactAbilityVisuals.ResolveActorScale(context);
            entity.GetComponent<Rotation>().value = ResolveDirection(context);

            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            float elapsed = (float)(now - startedAt);
            float pulse = 1f + Mathf.Sin(elapsed * cue.scale_pulse_speed) * cue.scale_pulse_amplitude;
            entity.GetComponent<Scale>().value = Vector3.one * (cue.scale * actorScale * pulse);

            Color color = ResolveColor(context.theme, cue.color_role);
            float visibility = 1f;
            if (cue.alpha_pulse_period > 0f)
            {
                float phase = Mathf.Repeat(elapsed / cue.alpha_pulse_period, 1f) * Mathf.PI * 2f;
                float flash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 6f);
                visibility = Mathf.Lerp(cue.alpha_pulse_floor, 1f, flash);
            }
            color.a *= cue.alpha * visibility * Mathf.Clamp01(0.35f + context.intensity * 0.65f);
            entity.GetComponent<AnimTint>().Value = color;
        }

        public void End()
        {
            if (IsAlive) ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
        }
    }
}

/// <summary>
/// 可组合多个既有 cue，使一种能力表现同时拥有贴图、范围线或其他后续扩展载体。
/// </summary>
public sealed class ArtifactCompositeVisualCue : IArtifactVisualCue
{
    private readonly IArtifactVisualCue[] cues;

    public ArtifactCompositeVisualCue(params IArtifactVisualCue[] cues)
    {
        this.cues = cues ?? throw new ArgumentNullException(nameof(cues));
    }

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        IArtifactVisualLease[] leases = new IArtifactVisualLease[cues.Length];
        for (int i = 0; i < cues.Length; i++) leases[i] = cues[i].Begin(context, now, duration);
        return new CompositeLease(leases);
    }

    private sealed class CompositeLease : IArtifactVisualLease
    {
        private readonly IArtifactVisualLease[] leases;

        public CompositeLease(IArtifactVisualLease[] leases)
        {
            this.leases = leases;
        }

        public bool IsAlive
        {
            get
            {
                for (int i = 0; i < leases.Length; i++)
                {
                    if (leases[i].IsAlive) return true;
                }
                return false;
            }
        }

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            for (int i = 0; i < leases.Length; i++) leases[i].Refresh(context, now, duration);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            for (int i = 0; i < leases.Length; i++) leases[i].Update(context, now);
        }

        public void End()
        {
            for (int i = 0; i < leases.Length; i++) leases[i].End();
        }
    }
}

/// <summary>
/// 地面范围、恢复波纹和常驻法阵共用的程序化圆形 cue。
/// </summary>
public sealed class ArtifactAreaVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorKind anchor = ArtifactVisualAnchorKind.Point;
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
    private const int CircleSegments = 64;
    private static MonoObjPool<ArtifactAreaVisualView> pool;
    private static Material material;
    private static Mesh circleMesh;

    internal static ArtifactAreaVisualView Get()
    {
        EnsureInitialized();
        return pool.GetNext();
    }

    private static void EnsureInitialized()
    {
        if (pool != null) return;

        GameObject root = new("artifact_ability_area_visuals");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        material = CreateMaterial();
        circleMesh = CreateCircleMesh();
        GameObject prefabObject = ModClass.NewPrefabPreview(nameof(ArtifactAreaVisualView));
        ArtifactAreaVisualView prefab = prefabObject.AddComponent<ArtifactAreaVisualView>();
        prefab.outer = CreateLine(prefab.transform, "outer", 0);
        prefab.inner = CreateLine(prefab.transform, "inner", 1);
        prefab.fill = CreateFill(prefab.transform, out prefab.fill_transform);
        pool = new MonoObjPool<ArtifactAreaVisualView>(
            prefab,
            root.transform,
            view => view.pool = pool,
            view => view.ResetView(),
            view => view.ResetView());
        prefab.pool = pool;
    }

    private static LineRenderer CreateLine(Transform parent, string name, int sortingOrder)
    {
        GameObject obj = new(name, typeof(LineRenderer));
        obj.transform.SetParent(parent, false);
        LineRenderer renderer = obj.GetComponent<LineRenderer>();
        renderer.useWorldSpace = false;
        renderer.loop = true;
        renderer.positionCount = CircleSegments;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsBack_3;
        renderer.sortingOrder = sortingOrder;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i / (float)CircleSegments * Mathf.PI * 2f;
            renderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
        }
        return renderer;
    }

    private static MeshRenderer CreateFill(Transform parent, out Transform fillTransform)
    {
        GameObject obj = new("fill", typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<MeshFilter>().sharedMesh = circleMesh;
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsBack_3;
        renderer.sortingOrder = -1;
        fillTransform = obj.transform;
        return renderer;
    }

    private static Material CreateMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        return new Material(shader) { hideFlags = HideFlags.DontSave };
    }

    private static Mesh CreateCircleMesh()
    {
        Vector3[] vertices = new Vector3[CircleSegments + 1];
        int[] triangles = new int[CircleSegments * 3];
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i / (float)CircleSegments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i == CircleSegments - 1 ? 1 : i + 2;
        }
        Mesh mesh = new() { name = "ArtifactAreaCircle", vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}

internal sealed class ArtifactAreaVisualView : MonoBehaviour
{
    internal MonoObjPool<ArtifactAreaVisualView> pool;
    public LineRenderer outer;
    public LineRenderer inner;
    public MeshRenderer fill;
    public Transform fill_transform;
    private MaterialPropertyBlock fillBlock;

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
        Color color = cue.ResolveColor(context.theme);

        transform.position = position;
        transform.localScale = Vector3.one;
        SetLine(outer, resolvedRadius, cue.line_width, color, cue.line_alpha * fade * intensity);
        SetLine(
            inner,
            resolvedRadius * cue.inner_radius_ratio,
            cue.line_width * 0.55f,
            context.theme.glow,
            cue.show_inner_ring ? cue.line_alpha * 0.58f * fade * intensity : 0f);
        inner.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * cue.inner_rotation_speed);

        fill_transform.localScale = new Vector3(resolvedRadius, resolvedRadius, 1f);
        fillBlock ??= new MaterialPropertyBlock();
        fill.GetPropertyBlock(fillBlock);
        color.a = cue.fill_alpha * fade * intensity;
        fillBlock.SetColor("_Color", color);
        fill.SetPropertyBlock(fillBlock);
        fill.enabled = color.a > 0.001f;
    }

    internal void ResetView()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        outer.enabled = false;
        inner.enabled = false;
        fill.enabled = false;
    }

    internal void Return()
    {
        ResetView();
        pool.Return(this);
    }

    private static void SetLine(
        LineRenderer renderer,
        float radius,
        float width,
        Color color,
        float alpha)
    {
        renderer.transform.localScale = new Vector3(radius, radius, 1f);
        renderer.widthMultiplier = width / Mathf.Max(radius, 0.05f);
        color.a = alpha;
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.enabled = alpha > 0.001f;
    }
}
