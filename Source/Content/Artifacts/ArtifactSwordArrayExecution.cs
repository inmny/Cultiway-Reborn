using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 分光剑阵的统一执行会话。一个会话推进全部剑影，命中请求延迟到轨迹查询结束后统一结算。
/// </summary>
internal static class ArtifactSwordArrayExecution
{
    internal const int MaxBladeCount = 96;

    private const int SelectionStride = 17;
    private const int MaxLaunchesPerUpdate = 4;
    private const float SortiesPerBlade = 2.25f;
    private const float MinimumLaunchInterval = 0.055f;
    private const float MaximumLaunchInterval = 0.14f;
    private const float MaximumLaunchDuration = 0.24f;
    private const float PierceDuration = 0.085f;
    private const float BaseReturnDuration = 0.34f;
    private const float MaximumReturnDuration = 0.46f;
    private const float LaunchSpeed = 30f;
    private const float RingAngularSpeed = 18f;
    private const float RingFollowResponse = 11f;
    private const float NoTargetRetryInterval = 0.18f;
    private static readonly List<PendingHit> PendingHits = new();

    internal static void Initialize(
        Entity execution,
        Actor owner,
        Vector2 origin,
        int bladeCount,
        float attackRange,
        float duration)
    {
        int count = Mathf.Clamp(bladeCount, 8, MaxBladeCount);
        float resolvedDuration = Mathf.Max(3f, duration);
        float formationDuration = Mathf.Min(0.78f, resolvedDuration * 0.12f);
        float collapseDuration = Mathf.Min(0.5f, resolvedDuration * 0.08f);
        float flightReserve = MaximumLaunchDuration + PierceDuration + MaximumReturnDuration;
        float launchWindow = Mathf.Max(
            MinimumLaunchInterval,
            resolvedDuration - formationDuration - collapseDuration - flightReserve);
        float launchInterval = Mathf.Clamp(
            launchWindow / (count * SortiesPerBlade),
            MinimumLaunchInterval,
            MaximumLaunchInterval);
        float now = (float)World.world.getCurWorldTime();

        ArtifactSwordArrayBladeState[] blades = new ArtifactSwordArrayBladeState[count];
        for (int i = 0; i < count; i++)
        {
            blades[i] = new ArtifactSwordArrayBladeState
            {
                slot_index = i,
                return_slot_index = i,
                phase = ArtifactSwordArrayBladePhase.Forming,
                position = origin,
                previous_position = origin,
                direction = Vector2.down,
                phase_origin = origin,
                phase_started_at = now,
                phase_duration = formationDuration,
            };
        }

        execution.GetComponent<ArtifactSwordArrayExecutionState>() = new ArtifactSwordArrayExecutionState
        {
            blades = blades,
            max_simultaneous = Mathf.Clamp(Mathf.CeilToInt(count / 6f), 6, 16),
            started_at = now,
            duration = resolvedDuration,
            formation_duration = formationDuration,
            collapse_duration = collapseDuration,
            launch_interval = launchInterval,
            next_launch_at = now + formationDuration,
            attack_range = attackRange,
            ring_angle = 90f,
            angular_speed = RingAngularSpeed,
        };
        execution.GetComponent<Position>().value = owner.cur_transform_position;
    }

    internal static void Update(
        ref SkillContext context,
        ref Position position,
        ref Rotation rotation,
        Entity execution,
        float deltaTime)
    {
        if (ModClass.I.Game.IsPaused() || execution.GetComponent<SkillExecution>().end_requested) return;

        Actor owner = context.SourceObj?.a;
        if (owner == null || owner.isRekt())
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        ref ArtifactSwordArrayExecutionState state = ref execution.GetComponent<ArtifactSwordArrayExecutionState>();
        float now = (float)World.world.getCurWorldTime();
        float elapsed = now - state.started_at;
        position.value = owner.cur_transform_position;
        rotation.z = 0f;
        if (elapsed >= state.duration)
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        float actorScale = Mathf.Max(owner.stats[S.scale], 0.1f) * 10f;
        if (elapsed >= state.formation_duration)
        {
            state.ring_angle = Mathf.Repeat(
                state.ring_angle + state.angular_speed * Mathf.Max(0f, deltaTime),
                360f);
        }

        for (int i = 0; i < state.blades.Length; i++)
        {
            ref ArtifactSwordArrayBladeState blade = ref state.blades[i];
            blade.previous_position = blade.position;
            switch (blade.phase)
            {
                case ArtifactSwordArrayBladePhase.Forming:
                    UpdateForming(
                        ref blade,
                        ResolveRingPosition(
                            owner,
                            blade.slot_index,
                            state.blades.Length,
                            state.blades.Length,
                            actorScale,
                            state.ring_angle),
                        now,
                        i);
                    break;
                case ArtifactSwordArrayBladePhase.Launching:
                    UpdateLaunching(execution, owner, ref state, i, actorScale, now);
                    break;
                case ArtifactSwordArrayBladePhase.Piercing:
                    UpdatePiercing(owner, ref state, i, actorScale, now);
                    break;
                case ArtifactSwordArrayBladePhase.Returning:
                    UpdateReturning(owner, ref state, i, actorScale, now);
                    break;
            }
        }

        UpdateArrayedBlades(owner, ref state, actorScale, deltaTime);

        float launchStopAt = state.started_at + state.duration - state.collapse_duration -
                             MaximumLaunchDuration - PierceDuration - MaximumReturnDuration;
        if (elapsed < state.formation_duration || now >= launchStopAt || now < state.next_launch_at) return;

        LaunchDueBlades(owner, ref state, actorScale, now);
    }

    internal static void ResolvePendingHits()
    {
        for (int i = 0; i < PendingHits.Count; i++)
        {
            PendingHit pending = PendingHits[i];
            Entity execution = pending.execution;
            Actor target = pending.target;
            if (!execution.IsAvailable() ||
                !execution.HasComponent<SkillContext>() ||
                !execution.HasComponent<SkillEntity>() ||
                target == null || target.isRekt()) continue;

            ref SkillContext context = ref execution.GetComponent<SkillContext>();
            if (context.SourceObj == null || context.SourceObj.isRekt()) continue;
            SkillHitResolver.HitTarget(
                ArtifactSkillExecutions.SwordArray,
                ref context,
                default,
                execution,
                target,
                playImpact: true);
        }
        PendingHits.Clear();
    }

    internal static float ResolveRingRadius(Actor owner, int bladeCount)
    {
        float actorScale = Mathf.Max(owner.stats[S.scale], 0.1f) * 10f;
        return ResolveRingRadius(actorScale, Mathf.Clamp(bladeCount, 8, MaxBladeCount));
    }

    private static void UpdateForming(
        ref ArtifactSwordArrayBladeState blade,
        Vector2 ringPosition,
        float now,
        int bladeIndex)
    {
        float stagger = bladeIndex % 8 * 0.012f;
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at - stagger) / Mathf.Max(0.01f, blade.phase_duration - stagger));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        blade.position = Vector2.LerpUnclamped(blade.phase_origin, ringPosition, eased);
        blade.direction = Vector2.down;
        if (progress < 1f) return;
        blade.phase = ArtifactSwordArrayBladePhase.Arrayed;
    }

    private static void UpdateArrayedBlades(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        float actorScale,
        float deltaTime)
    {
        int arrayedCount = CountArrayedBlades(ref state);
        if (arrayedCount == 0) return;

        float follow = 1f - Mathf.Exp(-RingFollowResponse * Mathf.Max(0f, deltaTime));
        for (int i = 0; i < state.blades.Length; i++)
        {
            ref ArtifactSwordArrayBladeState blade = ref state.blades[i];
            if (blade.phase != ArtifactSwordArrayBladePhase.Arrayed) continue;

            int rank = ResolveArrayedRank(ref state, blade.slot_index);
            Vector2 destination = ResolveRingPosition(
                owner,
                rank,
                arrayedCount,
                state.blades.Length,
                actorScale,
                state.ring_angle);
            blade.position = Vector2.LerpUnclamped(blade.position, destination, follow);
            blade.direction = Vector2.down;
        }
    }

    private static void LaunchDueBlades(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        float actorScale,
        float now)
    {
        using ListPool<Actor> targets = new();
        CollectTargets(owner, state.attack_range, targets);
        if (targets.Count == 0)
        {
            state.next_launch_at = now + NoTargetRetryInterval;
            return;
        }

        int inFlight = state.blades.Length - CountArrayedBlades(ref state);
        int launched = 0;
        while (now >= state.next_launch_at &&
               inFlight < state.max_simultaneous &&
               launched < MaxLaunchesPerUpdate)
        {
            if (!TrySelectArrayedBlade(ref state, out int bladeIndex))
            {
                state.next_launch_at = now + state.launch_interval;
                return;
            }

            int sequence = state.sortie_sequence;
            ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
            blade.return_slot_index = ResolveReturnSlot(
                blade.slot_index,
                bladeIndex,
                sequence,
                state.blades.Length);
            BeginLaunch(ref blade, targets[sequence % targets.Count], actorScale, now);
            state.sortie_sequence++;
            state.next_launch_at += state.launch_interval;
            inFlight++;
            launched++;
        }

        if (inFlight >= state.max_simultaneous && state.next_launch_at < now)
        {
            state.next_launch_at = now + state.launch_interval;
        }
    }

    private static bool TrySelectArrayedBlade(
        ref ArtifactSwordArrayExecutionState state,
        out int bladeIndex)
    {
        int count = state.blades.Length;
        int start = PositiveModulo(state.sortie_sequence * SelectionStride, count);
        for (int offset = 0; offset < count; offset++)
        {
            int candidate = (start + offset) % count;
            if (state.blades[candidate].phase != ArtifactSwordArrayBladePhase.Arrayed) continue;
            bladeIndex = candidate;
            return true;
        }

        bladeIndex = -1;
        return false;
    }

    private static void BeginLaunch(
        ref ArtifactSwordArrayBladeState blade,
        Actor target,
        float actorScale,
        float now)
    {
        Vector2 delta = target.current_position - blade.position;
        Vector2 direction = Normalize(delta, blade.direction);
        Vector2 perpendicular = new(-direction.y, direction.x);
        float arcSign = (blade.slot_index & 1) == 0 ? 1f : -1f;
        float distance = delta.magnitude;
        blade.phase = ArtifactSwordArrayBladePhase.Launching;
        blade.target = target;
        blade.phase_origin = blade.position;
        blade.phase_control = blade.position + delta * 0.18f + perpendicular * actorScale * 0.12f * arcSign;
        blade.phase_destination = target.current_position;
        blade.phase_started_at = now;
        blade.phase_duration = Mathf.Clamp(distance / LaunchSpeed, 0.11f, MaximumLaunchDuration);
        blade.direction = direction;
        blade.hit_at = 0f;
    }

    private static void UpdateLaunching(
        Entity execution,
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        float actorScale,
        float now)
    {
        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        if (!IsValidTarget(owner, blade.target, state.attack_range))
        {
            if (!TryAcquireTarget(
                    owner,
                    state.attack_range,
                    state.sortie_sequence + bladeIndex,
                    out Actor replacement))
            {
                BeginReturning(owner, ref state, ref blade, actorScale, now);
                return;
            }
            BeginLaunch(ref blade, replacement, actorScale, now);
        }

        Vector2 targetPosition = blade.target.current_position;
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration));
        float accelerated = progress * progress;
        blade.position = QuadraticBezier(
            blade.phase_origin,
            blade.phase_control,
            targetPosition,
            accelerated);
        blade.direction = Normalize(blade.position - blade.previous_position, blade.direction);
        if (progress < 1f) return;

        Actor target = blade.target;
        blade.position = target.current_position;
        blade.direction = Normalize(blade.position - blade.phase_origin, blade.direction);
        blade.phase = ArtifactSwordArrayBladePhase.Piercing;
        blade.phase_origin = blade.position;
        blade.phase_destination = blade.position + blade.direction *
            Mathf.Max(0.55f, target.stats[S.size] * 0.45f + actorScale * 0.5f);
        blade.phase_started_at = now;
        blade.phase_duration = PierceDuration;
        blade.hit_at = now;
        blade.target = null;
        PendingHits.Add(new PendingHit(execution, target));
    }

    private static void UpdatePiercing(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        float actorScale,
        float now)
    {
        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration));
        blade.position = Vector2.LerpUnclamped(blade.phase_origin, blade.phase_destination, progress);
        blade.direction = Normalize(blade.position - blade.previous_position, blade.direction);
        if (progress < 1f) return;
        BeginReturning(owner, ref state, ref blade, actorScale, now);
    }

    private static void BeginReturning(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        ref ArtifactSwordArrayBladeState blade,
        float actorScale,
        float now)
    {
        Vector2 ringPosition = ResolveRingPosition(
            owner,
            blade.return_slot_index,
            state.blades.Length,
            state.blades.Length,
            actorScale,
            state.ring_angle);
        Vector2 toRing = ringPosition - blade.position;
        Vector2 direction = Normalize(toRing, -blade.direction);
        Vector2 perpendicular = new(-direction.y, direction.x);
        float arcSign = (blade.return_slot_index & 1) == 0 ? -1f : 1f;
        float distance = toRing.magnitude;
        blade.phase = ArtifactSwordArrayBladePhase.Returning;
        blade.phase_origin = blade.position;
        blade.phase_control = blade.position + toRing * 0.42f +
                              perpendicular * actorScale * 0.62f * arcSign;
        blade.phase_destination = ringPosition;
        blade.phase_started_at = now;
        blade.phase_duration = Mathf.Clamp(
            BaseReturnDuration + distance * 0.012f,
            0.3f,
            MaximumReturnDuration);
        blade.direction = direction;
    }

    private static void UpdateReturning(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        float actorScale,
        float now)
    {
        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        Vector2 ringPosition = ResolveRingPosition(
            owner,
            blade.return_slot_index,
            state.blades.Length,
            state.blades.Length,
            actorScale,
            state.ring_angle);
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration));
        float decelerated = 1f - (1f - progress) * (1f - progress);
        blade.position = QuadraticBezier(
            blade.phase_origin,
            blade.phase_control,
            ringPosition,
            decelerated);
        blade.direction = Normalize(blade.position - blade.previous_position, blade.direction);
        if (progress < 1f) return;

        int returnSlot = blade.return_slot_index;
        ReinsertBlade(ref state, bladeIndex, returnSlot);
        ref ArtifactSwordArrayBladeState returnedBlade = ref state.blades[bladeIndex];
        returnedBlade.phase = ArtifactSwordArrayBladePhase.Arrayed;
        returnedBlade.position = ringPosition;
        returnedBlade.direction = Vector2.down;
        returnedBlade.target = null;
    }

    /// <summary>将归阵剑影插入目标阵位，并让沿途阵位整体轮换，形成多剑交换而非成对互换。</summary>
    private static void ReinsertBlade(
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        int destinationSlot)
    {
        int oldSlot = state.blades[bladeIndex].slot_index;
        if (destinationSlot > oldSlot)
        {
            for (int i = 0; i < state.blades.Length; i++)
            {
                if (i == bladeIndex) continue;
                ref ArtifactSwordArrayBladeState shifted = ref state.blades[i];
                if (shifted.slot_index > oldSlot && shifted.slot_index <= destinationSlot)
                {
                    shifted.slot_index--;
                }
            }
        }
        else if (destinationSlot < oldSlot)
        {
            for (int i = 0; i < state.blades.Length; i++)
            {
                if (i == bladeIndex) continue;
                ref ArtifactSwordArrayBladeState shifted = ref state.blades[i];
                if (shifted.slot_index >= destinationSlot && shifted.slot_index < oldSlot)
                {
                    shifted.slot_index++;
                }
            }
        }

        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        blade.slot_index = destinationSlot;
        blade.return_slot_index = destinationSlot;
    }

    private static bool TryAcquireTarget(
        Actor owner,
        float attackRange,
        int sequence,
        out Actor target)
    {
        using ListPool<Actor> targets = new();
        CollectTargets(owner, attackRange, targets);
        if (targets.Count == 0)
        {
            target = null;
            return false;
        }
        target = targets[PositiveModulo(sequence, targets.Count)];
        return true;
    }

    private static void CollectTargets(Actor owner, float attackRange, ListPool<Actor> targets)
    {
        ArtifactTargeting.ForEachHostile(
            owner,
            owner.current_position,
            attackRange,
            targets.Add);
        targets.Sort((left, right) =>
        {
            float leftDistance = Toolbox.SquaredDistVec2Float(owner.current_position, left.current_position);
            float rightDistance = Toolbox.SquaredDistVec2Float(owner.current_position, right.current_position);
            int comparison = leftDistance.CompareTo(rightDistance);
            return comparison != 0 ? comparison : left.data.id.CompareTo(right.data.id);
        });
    }

    private static bool IsValidTarget(Actor owner, Actor target, float attackRange)
    {
        if (target == null || target.isRekt() || !owner.canAttackTarget(target)) return false;
        float range = attackRange + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(owner.current_position, target.current_position) <= range * range;
    }

    private static int CountArrayedBlades(ref ArtifactSwordArrayExecutionState state)
    {
        int count = 0;
        for (int i = 0; i < state.blades.Length; i++)
        {
            if (state.blades[i].phase == ArtifactSwordArrayBladePhase.Arrayed) count++;
        }
        return count;
    }

    private static int ResolveArrayedRank(
        ref ArtifactSwordArrayExecutionState state,
        int slotIndex)
    {
        int rank = 0;
        for (int i = 0; i < state.blades.Length; i++)
        {
            ArtifactSwordArrayBladeState blade = state.blades[i];
            if (blade.phase == ArtifactSwordArrayBladePhase.Arrayed && blade.slot_index < slotIndex)
            {
                rank++;
            }
        }
        return rank;
    }

    private static int ResolveReturnSlot(
        int currentSlot,
        int bladeIndex,
        int sequence,
        int bladeCount)
    {
        int maximumOffset = Mathf.Max(2, bladeCount / 3);
        int offset = 1 + PositiveModulo(sequence * SelectionStride + bladeIndex * 11, maximumOffset);
        int direction = ((sequence + bladeIndex) & 1) == 0 ? 1 : -1;
        return PositiveModulo(currentSlot + direction * offset, bladeCount);
    }

    private static Vector2 ResolveRingPosition(
        Actor owner,
        int rank,
        int visibleCount,
        int totalCount,
        float actorScale,
        float ringAngle)
    {
        float angle = (ringAngle + rank * 360f / Mathf.Max(1, visibleCount)) * Mathf.Deg2Rad;
        float radius = ResolveRingRadius(actorScale, totalCount);
        Vector2 center = owner.cur_transform_position + Vector3.up * actorScale * 0.52f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static float ResolveRingRadius(float actorScale, int bladeCount)
    {
        return actorScale * Mathf.Sqrt(bladeCount);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
    }

    private static Vector2 Normalize(Vector2 value, Vector2 fallback)
    {
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.up;
    }

    private readonly struct PendingHit
    {
        internal readonly Entity execution;
        internal readonly Actor target;

        internal PendingHit(Entity execution, Actor target)
        {
            this.execution = execution;
            this.target = target;
        }
    }
}
