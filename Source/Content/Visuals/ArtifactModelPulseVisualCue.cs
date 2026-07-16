using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>复用 SkillLibV3 全局发射器的粒子 cue，可跟随移动锚点持续发射或作为短时爆发。</summary>
public sealed class ArtifactModelPulseVisualCue : IArtifactVisualCue
{
    public ArtifactVisualColorRole color_role = ArtifactVisualColorRole.Glow;
    public float start_scale = 1f;
    public float end_scale = 1f;
    public float pulse_amplitude = 0.12f;
    public float pulse_speed = 7f;
    public float tint_blend = 0.45f;
    public bool fade_out;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        return ArtifactModelPulseVisualState.Begin(this, context, now, duration);
    }
}

/// <summary>模型脉冲的纯运行时注册表；世界渲染系统逐帧读取，不向法器实体附加表现组件。</summary>
internal static class ArtifactModelPulseVisualState
{
    private static readonly Dictionary<long, PulseEntry> Entries = new();
    private static long nextToken;

    internal static IArtifactVisualLease Begin(
        ArtifactModelPulseVisualCue cue,
        ArtifactAbilityVisualContext context,
        double now,
        float duration)
    {
        long token = ++nextToken;
        Entries[token] = new PulseEntry(cue, context, now, duration);
        return new PulseLease(token);
    }

    internal static void Apply(Entity artifact, double now, ref float scale, ref Color color)
    {
        float combinedScale = 1f;
        float strongestBlend = 0f;
        Color strongestTint = color;
        foreach (PulseEntry entry in Entries.Values)
        {
            if (entry.context.artifact != artifact) continue;
            float elapsed = (float)(now - entry.started_at);
            float progress = entry.expires_at > entry.started_at
                ? Mathf.Clamp01((float)((now - entry.started_at) / (entry.expires_at - entry.started_at)))
                : 0f;
            float transition = Mathf.Lerp(entry.cue.start_scale, entry.cue.end_scale, progress);
            combinedScale *= transition *
                             (1f + Mathf.Sin(elapsed * entry.cue.pulse_speed) * entry.cue.pulse_amplitude);
            float fade = entry.cue.fade_out ? 1f - progress : 1f;
            float blend = entry.cue.tint_blend * fade * Mathf.Clamp01(entry.context.intensity);
            if (blend <= strongestBlend) continue;
            strongestBlend = blend;
            strongestTint = ArtifactVisualCueTools.ResolveColor(entry.context.theme, entry.cue.color_role);
        }
        scale *= Mathf.Max(0.05f, combinedScale);
        color = Color.Lerp(color, strongestTint, Mathf.Clamp01(strongestBlend));
    }

    private sealed class PulseLease : IArtifactVisualLease
    {
        private readonly long token;

        public PulseLease(long token)
        {
            this.token = token;
        }

        public bool IsAlive => Entries.ContainsKey(token);

        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration)
        {
            if (!Entries.TryGetValue(token, out PulseEntry entry)) return;
            entry.context = context;
            entry.started_at = now;
            entry.expires_at = duration > 0f ? now + duration : 0d;
        }

        public void Update(ArtifactAbilityVisualContext context, double now)
        {
            if (Entries.TryGetValue(token, out PulseEntry entry)) entry.context = context;
        }

        public void End()
        {
            Entries.Remove(token);
        }
    }

    private sealed class PulseEntry
    {
        public readonly ArtifactModelPulseVisualCue cue;
        public ArtifactAbilityVisualContext context;
        public double started_at;
        public double expires_at;

        public PulseEntry(
            ArtifactModelPulseVisualCue cue,
            ArtifactAbilityVisualContext context,
            double now,
            float duration)
        {
            this.cue = cue;
            this.context = context;
            started_at = now;
            expires_at = duration > 0f ? now + duration : 0d;
        }
    }
}
