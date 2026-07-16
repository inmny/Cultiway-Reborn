using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>复用 SkillLibV3 全局发射器的粒子 cue，可跟随移动锚点持续发射或作为短时爆发。</summary>
public class ArtifactAudioVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public Func<ArtifactAbilityVisualContext, string> ResolveSound;
    public string sound;
    public float shake_duration = 0.08f;
    public float shake_speed = 0.02f;
    public float shake_intensity;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        AudioLease lease = new(this);
        lease.Refresh(context, now, duration);
        return lease;
    }

    private void Play(ArtifactAbilityVisualContext context)
    {
        ArtifactAbilityVisuals.TryResolveAnchorPosition(context, anchor, out Vector3 position);
        string resolvedSound = ResolveSound?.Invoke(context) ?? sound;
        if (!string.IsNullOrEmpty(resolvedSound))
        {
            MusicBox.playSound(resolvedSound, position.x, position.y, pGameViewOnly: true);
        }
        if (shake_intensity > 0f)
        {
            World.world.startShake(shake_duration, shake_speed, shake_intensity * Mathf.Clamp(context.intensity, 0.25f, 2f));
        }
    }

    private sealed class AudioLease : IArtifactVisualLease
    {
        private readonly ArtifactAudioVisualCue cue;
        private bool alive = true;

        public AudioLease(ArtifactAudioVisualCue cue)
        {
            this.cue = cue;
        }

        public bool IsAlive => alive;

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            if (alive) cue.Play(context);
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
        }

        public void End()
        {
            alive = false;
        }
    }
}

/// <summary>强调命中、落地和破碎等瞬间反馈的声音与震屏预设。</summary>
public sealed class ArtifactImpactVisualCue : ArtifactAudioVisualCue
{
    public ArtifactImpactVisualCue()
    {
        shake_intensity = 0.12f;
    }
}

/// <summary>直接改变法器世界本体的缩放与染色，用于蓄力、激活、受击和显化脉冲。</summary>
