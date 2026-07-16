using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;
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
