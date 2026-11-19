using System;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>法宝动作信号只在真实锚点播放一次声音。</summary>
public class ArtifactAudioVisualCue : IArtifactVisualCue
{
    public ArtifactVisualAnchorRef anchor = ArtifactVisualAnchorKind.Point;
    public Func<ArtifactAbilityVisualContext, string> ResolveSound;
    public string sound;

    public IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration)
    {
        if (ArtifactAbilityVisuals.TryResolveAnchorPosition(context, anchor, out Vector3 position))
            SpatialCombatFeedback.Sound(ResolveSound?.Invoke(context) ?? sound, position);
        return AudioLease.Instance;
    }
    // 声音不占用持续表现，组合回退与刷新无需重新播放。
    private sealed class AudioLease : IArtifactVisualLease
    {
        internal static readonly AudioLease Instance = new();
        public bool IsAlive => false;
        public void Refresh(ArtifactAbilityVisualContext context, double now, float duration) { }
        public void Update(ArtifactAbilityVisualContext context, double now) { }
        public void End() { }
    }
}
