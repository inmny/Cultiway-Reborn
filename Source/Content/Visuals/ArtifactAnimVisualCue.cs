using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;
public sealed class ArtifactAnimVisualCue : IArtifactVisualCue
{
    public string effect_path;
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Artifact;
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
