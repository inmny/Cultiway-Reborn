using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Render;

/// <summary>
/// 从法器能力运行状态重建持续视觉，并在查询结束后消费短时信号。所有租约均为瞬时表现状态。
/// </summary>
public sealed class ArtifactAbilityVisualSystem : QuerySystem<ArtifactAbilitySet, ArtifactAbilityRuntime>
{
    private readonly Dictionary<ArtifactVisualKey, DesiredVisual> desiredLoops = new();
    private readonly Dictionary<ArtifactVisualKey, ActiveVisual> activeLoops = new();
    private readonly Dictionary<ArtifactVisualKey, ActiveVisual> transientVisuals = new();
    private readonly List<ArtifactVisualSignalRequest> signals = new();
    private readonly List<ArtifactVisualKey> staleKeys = new();
    private long signalSerial;

    public ArtifactAbilityVisualSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        double now = World.world.getCurWorldTime();
        if (!GeneralSettings.EnableArtifactSystems || !MapBox.isRenderGameplay() || MapBox.isRenderMiniMap())
        {
            ArtifactAbilityVisuals.DrainSignals(signals);
            EndAll();
            return;
        }

        desiredLoops.Clear();
        Query.ForEachEntity((ref ArtifactAbilitySet abilitySet, ref ArtifactAbilityRuntime runtime, Entity artifact) =>
        {
            if (!runtime.attached || runtime.controller.IsNull) return;
            for (int i = 0; i < abilitySet.abilities.Length; i++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[i];
                ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
                ArtifactAbilityVisualProfile profile = asset.visual;
                if (profile == null) continue;

                IReadOnlyList<ArtifactAbilityVisualLoop> loops = profile.Loops;
                for (int j = 0; j < loops.Count; j++)
                {
                    ArtifactAbilityVisualLoop loop = loops[j];
                    ArtifactAbilityVisualContext context = ArtifactAbilityVisuals.CreateLoopContext(
                        artifact,
                        asset,
                        ability,
                        runtime.abilities[i],
                        runtime,
                        loop.channel);
                    if (!ArtifactAbilityVisuals.IsVisible(context) || !loop.IsActive(context)) continue;

                    float intensity = Math.Max(0f, loop.ResolveIntensity?.Invoke(context) ?? 1f);
                    AddDesiredLoop(loop, context.WithIntensity(intensity));
                }
            }
        });

        ReconcileLoops(now);
        ArtifactAbilityVisuals.DrainSignals(signals);
        for (int i = 0; i < signals.Count; i++) ProcessSignal(signals[i], now);
        UpdateTransients(now);
    }

    private void AddDesiredLoop(ArtifactAbilityVisualLoop loop, ArtifactAbilityVisualContext context)
    {
        ArtifactVisualKey key = ArtifactVisualKey.ForLoop(context, loop);
        DesiredVisual candidate = new(loop.cue, context);
        if (!desiredLoops.TryGetValue(key, out DesiredVisual existing))
        {
            desiredLoops.Add(key, candidate);
            return;
        }

        if (loop.stack_policy == ArtifactVisualStackPolicy.MergeIntensity)
        {
            float mergedIntensity = existing.context.intensity + context.intensity;
            ArtifactAbilityVisualContext representative = context.intensity > existing.context.intensity
                ? context
                : existing.context;
            desiredLoops[key] = new DesiredVisual(loop.cue, representative.WithIntensity(mergedIntensity));
        }
        else if (context.intensity > existing.context.intensity)
        {
            desiredLoops[key] = candidate;
        }
    }

    private void ReconcileLoops(double now)
    {
        staleKeys.Clear();
        foreach (KeyValuePair<ArtifactVisualKey, ActiveVisual> pair in activeLoops)
        {
            if (!desiredLoops.ContainsKey(pair.Key)) staleKeys.Add(pair.Key);
        }
        for (int i = 0; i < staleKeys.Count; i++) RemoveVisual(activeLoops, staleKeys[i]);

        foreach (KeyValuePair<ArtifactVisualKey, DesiredVisual> pair in desiredLoops)
        {
            DesiredVisual desired = pair.Value;
            if (activeLoops.TryGetValue(pair.Key, out ActiveVisual active) &&
                ReferenceEquals(active.cue, desired.cue) && active.lease.IsAlive)
            {
                active.context = desired.context;
                active.lease.Update(active.context, now);
                continue;
            }

            if (active != null) RemoveVisual(activeLoops, pair.Key);
            IArtifactVisualLease lease = desired.cue.Begin(desired.context, now, 0f);
            if (lease.IsAlive)
            {
                activeLoops[pair.Key] = new ActiveVisual(desired.cue, lease, desired.context, 0d);
            }
        }
    }

    private void ProcessSignal(ArtifactVisualSignalRequest request, double now)
    {
        if (!ArtifactAbilityVisuals.IsVisible(request.context)) return;
        ArtifactAbilityVisualSignal signal = request.signal;
        ArtifactVisualKey key = ArtifactVisualKey.ForSignal(request.context, signal, ++signalSerial);
        if (transientVisuals.TryGetValue(key, out ActiveVisual active) &&
            ReferenceEquals(active.cue, signal.cue) && active.lease.IsAlive)
        {
            ArtifactAbilityVisualContext context = request.context;
            if (signal.stack_policy == ArtifactVisualStackPolicy.Strongest &&
                active.context.intensity > context.intensity) return;
            if (signal.stack_policy == ArtifactVisualStackPolicy.MergeIntensity)
            {
                float mergedIntensity = active.context.intensity + context.intensity;
                ArtifactAbilityVisualContext representative = context.intensity > active.context.intensity
                    ? context
                    : active.context;
                context = representative.WithIntensity(mergedIntensity);
            }

            active.context = context;
            active.expires_at = now + request.duration;
            active.lease.Refresh(context, now, request.duration);
            return;
        }

        if (active != null) RemoveVisual(transientVisuals, key);
        IArtifactVisualLease lease = signal.cue.Begin(request.context, now, request.duration);
        if (lease.IsAlive)
        {
            transientVisuals[key] = new ActiveVisual(
                signal.cue,
                lease,
                request.context,
                now + request.duration);
        }
    }

    private void UpdateTransients(double now)
    {
        staleKeys.Clear();
        foreach (KeyValuePair<ArtifactVisualKey, ActiveVisual> pair in transientVisuals)
        {
            ActiveVisual active = pair.Value;
            if (now >= active.expires_at || !active.lease.IsAlive ||
                !ArtifactAbilityVisuals.IsVisible(active.context))
            {
                staleKeys.Add(pair.Key);
                continue;
            }
            active.lease.Update(active.context, now);
        }
        for (int i = 0; i < staleKeys.Count; i++) RemoveVisual(transientVisuals, staleKeys[i]);
    }

    private void EndAll()
    {
        foreach (ActiveVisual active in activeLoops.Values) active.lease.End();
        foreach (ActiveVisual active in transientVisuals.Values) active.lease.End();
        activeLoops.Clear();
        transientVisuals.Clear();
        desiredLoops.Clear();
    }

    private static void RemoveVisual(
        Dictionary<ArtifactVisualKey, ActiveVisual> visuals,
        ArtifactVisualKey key)
    {
        if (!visuals.TryGetValue(key, out ActiveVisual active)) return;
        active.lease.End();
        visuals.Remove(key);
    }

    private readonly struct DesiredVisual
    {
        public readonly IArtifactVisualCue cue;
        public readonly ArtifactAbilityVisualContext context;

        public DesiredVisual(IArtifactVisualCue cue, ArtifactAbilityVisualContext context)
        {
            this.cue = cue;
            this.context = context;
        }
    }

    private sealed class ActiveVisual
    {
        public readonly IArtifactVisualCue cue;
        public readonly IArtifactVisualLease lease;
        public ArtifactAbilityVisualContext context;
        public double expires_at;

        public ActiveVisual(
            IArtifactVisualCue cue,
            IArtifactVisualLease lease,
            ArtifactAbilityVisualContext context,
            double expiresAt)
        {
            this.cue = cue;
            this.lease = lease;
            this.context = context;
            expires_at = expiresAt;
        }
    }

    private readonly struct ArtifactVisualKey : IEquatable<ArtifactVisualKey>
    {
        private readonly Entity scope;
        private readonly Entity artifact;
        private readonly string group;
        private readonly string instance;
        private readonly long serial;

        private ArtifactVisualKey(Entity scope, Entity artifact, string group, string instance, long serial)
        {
            this.scope = scope;
            this.artifact = artifact;
            this.group = group;
            this.instance = instance;
            this.serial = serial;
        }

        public static ArtifactVisualKey ForLoop(
            ArtifactAbilityVisualContext context,
            ArtifactAbilityVisualLoop loop)
        {
            return loop.stack_policy == ArtifactVisualStackPolicy.Independent
                ? new ArtifactVisualKey(
                    context.artifact,
                    context.artifact,
                    loop.channel,
                    context.ability.instance_id,
                    0)
                : new ArtifactVisualKey(context.controller, default, loop.stack_group, string.Empty, 0);
        }

        public static ArtifactVisualKey ForSignal(
            ArtifactAbilityVisualContext context,
            ArtifactAbilityVisualSignal signal,
            long nextSerial)
        {
            return signal.stack_policy == ArtifactVisualStackPolicy.Independent
                ? new ArtifactVisualKey(
                    context.artifact,
                    context.artifact,
                    signal.channel,
                    context.ability.instance_id,
                    nextSerial)
                : new ArtifactVisualKey(context.controller, default, signal.stack_group, string.Empty, 0);
        }

        public bool Equals(ArtifactVisualKey other)
        {
            return scope == other.scope && artifact == other.artifact && group == other.group &&
                   instance == other.instance && serial == other.serial;
        }

        public override bool Equals(object obj)
        {
            return obj is ArtifactVisualKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = scope.GetHashCode();
                hash = hash * 397 ^ artifact.GetHashCode();
                hash = hash * 397 ^ (group?.GetHashCode() ?? 0);
                hash = hash * 397 ^ (instance?.GetHashCode() ?? 0);
                return hash * 397 ^ serial.GetHashCode();
            }
        }
    }
}
