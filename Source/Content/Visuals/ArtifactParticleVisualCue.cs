using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>复用 SkillLibV3 全局发射器的粒子 cue，可跟随移动锚点持续发射或作为短时爆发。</summary>
public sealed class ArtifactParticleVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public SkillFlyOverParticleStyle style = SkillFlyOverParticleStyle.Default;
    public Vector3 offset;
    public float spread = 0.25f;
    public float directional_speed;
    public float emission_interval = -1f;
    public int particle_count = -1;
    public bool match_actor_scale = true;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        ParticleLease lease = new(this);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private sealed class ParticleLease : IArtifactVisualLease
    {
        private readonly ArtifactParticleVisualCue cue;
        private bool alive = true;
        private double nextEmission;

        public ParticleLease(ArtifactParticleVisualCue cue)
        {
            this.cue = cue;
        }

        public bool IsAlive => alive;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            nextEmission = now;
            Update(context, now);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (!alive || now < nextEmission ||
                !ArtifactVisualCueTools.TryResolve(context, cue.anchor, cue.offset, cue.match_actor_scale, out Vector3 position))
            {
                return;
            }

            float actorScale = cue.match_actor_scale ? ArtifactAbilityVisuals.ResolveActorScale(context) : 1f;
            SkillFlyOverParticleStyle style = cue.style;
            style.MinSize *= actorScale;
            style.MaxSize *= actorScale;
            style.MinRiseSpeed *= actorScale;
            style.MaxRiseSpeed *= actorScale;
            style.HorizontalDrift *= actorScale;
            int count = cue.particle_count >= 0
                ? cue.particle_count
                : Mathf.Max(1, Mathf.RoundToInt(style.ParticlesPerEmission * Mathf.Clamp(context.intensity, 0.5f, 3f)));
            Color color = ArtifactVisualCueTools.ResolveColor(context.theme, cue.color_role);
            SkillFlyOverParticleEmitter.EmitBurst(
                position,
                color,
                style,
                cue.spread * actorScale,
                ArtifactVisualCueTools.ResolveDirection(context),
                cue.directional_speed * actorScale,
                count);
            float interval = cue.emission_interval > 0f ? cue.emission_interval : Mathf.Max(0.02f, style.EmissionInterval);
            nextEmission = now + interval;
        }

        public void End()
        {
            alive = false;
        }
    }
}

/// <summary>声音和镜头冲击反馈 cue；不创建场景对象，生命周期仍遵循统一租约协议。</summary>
