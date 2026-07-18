using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
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
/// 分光剑阵的统一执行会话。一个会话调度全部剑影，每道剑影由独立 SkillEntity 渲染并参与标准扫掠碰撞。
/// </summary>
internal static class ArtifactSwordArrayExecution
{
    internal const int MaxBladeCount = 96;

    private const int MaxLaunchesPerUpdate = 4;
    private const float InFlightRatio = 0.5f;
    private const float TraversalSpeed = 36f;
    private const float MinimumTraversalDuration = 0.28f;
    private const float MaximumTraversalDuration = 0.68f;
    private const float DirectionJitter = 8f;
    private const float RingAngularSpeed = 18f;
    private const float RingFollowResponse = 11f;
    private const float NoTargetRetryInterval = 0.18f;
    private const float BladeSizeRatio = 0.25f;
    private const int MovingAfterimageCount = 2;

    internal static void Initialize(
        Entity execution,
        Actor owner,
        Entity artifact,
        Vector2 origin,
        int bladeCount,
        float attackRange,
        float duration)
    {
        int count = Mathf.Clamp(bladeCount, 8, MaxBladeCount);
        float resolvedDuration = Mathf.Max(3f, duration);
        float formationDuration = Mathf.Min(0.78f, resolvedDuration * 0.12f);
        float collapseDuration = Mathf.Min(0.5f, resolvedDuration * 0.08f);
        float now = (float)World.world.getCurWorldTime();

        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        ArtifactManifestationTools.EnsureWorldComponents(artifact, shape.presentation.body_radius);
        ArtifactManifestationTools.ApplyActiveWorldSize(artifact, owner);
        Sprite sprite = ArtifactManifestationTools.ResolveWorldSprite(artifact, true);
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float renderScale = artifact.GetComponent<ArtifactManifestation>().world_size / spriteSize * BladeSizeRatio;
        ArtifactBody artifactBody = artifact.GetComponent<ArtifactBody>();
        SkillContext bladeContext = execution.GetComponent<SkillContext>();

        ArtifactSwordArrayBladeState[] blades = new ArtifactSwordArrayBladeState[count];
        for (int i = 0; i < count; i++)
        {
            Entity bladeEntity = ArtifactSkillExecutions.SwordArrayBlade.NewEntity();
            InitializeBladeEntity(
                execution,
                bladeEntity,
                bladeContext,
                sprite,
                renderScale,
                artifactBody,
                origin);
            blades[i] = new ArtifactSwordArrayBladeState
            {
                entity = bladeEntity,
                slot_index = i,
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
            artifact = artifact,
            target_in_flight = Mathf.CeilToInt(count * InFlightRatio),
            started_at = now,
            duration = resolvedDuration,
            formation_duration = formationDuration,
            collapse_duration = collapseDuration,
            next_launch_attempt_at = now + formationDuration,
            attack_range = attackRange,
            ring_angle = 90f,
            angular_speed = RingAngularSpeed,
        };
        execution.GetComponent<Position>().value = owner.cur_transform_position;
    }

    private static void InitializeBladeEntity(
        Entity execution,
        Entity blade,
        SkillContext context,
        Sprite sprite,
        float renderScale,
        ArtifactBody artifactBody,
        Vector2 origin)
    {
        blade.GetComponent<SkillContext>() = context;
        blade.GetComponent<Position>().value = origin;
        blade.GetComponent<PrevPosition>().Value = origin;
        blade.GetComponent<Rotation>().in_plane = Vector2.down;
        blade.GetComponent<Scale>().value = Vector3.one * renderScale;

        ref AnimData animData = ref blade.GetComponent<AnimData>();
        animData.frames = [sprite];
        animData.frame_idx = 0;
        animData.frame_timer = 0f;

        blade.GetComponent<ColliderSphere>().Radius = artifactBody.radius * BladeSizeRatio;
        blade.GetComponent<ColliderLinearExtent>() = new ColliderLinearExtent
        {
            Forward = Mathf.Max(0f, artifactBody.forward_extent - artifactBody.radius) * BladeSizeRatio,
            Backward = Mathf.Max(0f, artifactBody.backward_extent - artifactBody.radius) * BladeSizeRatio,
        };
        blade.GetComponent<ColliderConfig>().Enabled = true;
        SkillExecutionLifecycle.BindSpawnedBody(execution, blade, "blade");
    }

    internal static void Update(
        ref SkillContext context,
        ref Position position,
        ref Rotation rotation,
        Entity execution,
        float deltaTime)
    {
        if (ModClass.I.Game.IsPaused()) return;

        ref ArtifactSwordArrayExecutionState state = ref execution.GetComponent<ArtifactSwordArrayExecutionState>();
        if (execution.GetComponent<SkillExecution>().end_requested)
        {
            DisableBladeColliders(ref state);
            return;
        }

        Actor owner = context.SourceObj?.a;
        if (owner == null || owner.isRekt())
        {
            DisableBladeColliders(ref state);
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        float now = (float)World.world.getCurWorldTime();
        float elapsed = now - state.started_at;
        position.value = owner.cur_transform_position;
        rotation.z = 0f;
        if (elapsed >= state.duration)
        {
            DisableBladeColliders(ref state);
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
                case ArtifactSwordArrayBladePhase.Traversing:
                    UpdateTraversing(owner, ref state, i, actorScale, now);
                    break;
            }
        }

        float collapseProgress = state.collapse_duration > 0f
            ? Mathf.Clamp01(
                (elapsed - (state.duration - state.collapse_duration)) /
                state.collapse_duration)
            : 0f;
        UpdateArrayedBlades(
            owner,
            ref state,
            actorScale,
            deltaTime,
            ResolveCollapseCenter(ref state),
            collapseProgress);

        float launchStopAt = state.started_at + state.duration - state.collapse_duration -
                             MaximumTraversalDuration;
        if (elapsed >= state.formation_duration && now < launchStopAt && now >= state.next_launch_attempt_at)
        {
            LaunchDueBlades(owner, ref state, actorScale, now);
        }

        SyncBladeEntities(owner, ref state, now, collapseProgress);
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
        float deltaTime,
        Vector2 collapseCenter,
        float collapseProgress)
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
            Vector2 followed = Vector2.LerpUnclamped(blade.position, destination, follow);
            blade.position = Vector2.LerpUnclamped(
                followed,
                collapseCenter,
                collapseProgress * collapseProgress);
            blade.direction = Vector2.down;
        }
    }

    private static void LaunchDueBlades(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        float actorScale,
        float now)
    {
        int inFlight = state.blades.Length - CountArrayedBlades(ref state);
        if (inFlight >= state.target_in_flight)
        {
            if (state.next_launch_attempt_at <= now)
            {
                state.next_launch_attempt_at = now + ResolveLaunchInterval(ref state, state.sortie_sequence);
            }
            return;
        }

        using ListPool<Actor> targets = new();
        CollectTargets(owner, state.attack_range, targets);
        if (targets.Count == 0)
        {
            state.next_launch_attempt_at = now + NoTargetRetryInterval;
            return;
        }

        Vector2 ringCenter = ResolveRingCenter(owner, actorScale);
        int launched = 0;
        while (now >= state.next_launch_attempt_at &&
               inFlight < state.target_in_flight &&
               launched < MaxLaunchesPerUpdate)
        {
            int sequence = state.sortie_sequence;
            int targetIndex = Mathf.Min(
                targets.Count - 1,
                Mathf.FloorToInt(Sample01(state.artifact.Id, sequence, 11) * targets.Count));
            if (!TrySelectRandomArrayedBlade(ref state, sequence, out int bladeIndex)) return;

            ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
            Vector2 targetDirection = Normalize(
                targets[targetIndex].current_position - blade.position,
                ringCenter - blade.position);
            float directionJitter = Mathf.Lerp(
                -DirectionJitter,
                DirectionJitter,
                Sample01(state.artifact.Id, sequence, 23));
            Vector2 travelDirection = Rotate(targetDirection, directionJitter);
            BeginTraversal(
                ref blade,
                travelDirection,
                ringCenter,
                actorScale,
                state.blades.Length,
                now,
                state.artifact.Id,
                sequence);
            state.sortie_sequence++;
            state.next_launch_attempt_at += ResolveLaunchInterval(ref state, sequence);
            inFlight++;
            launched++;
        }

        if (inFlight >= state.target_in_flight && state.next_launch_attempt_at <= now)
        {
            state.next_launch_attempt_at = now + ResolveLaunchInterval(ref state, state.sortie_sequence);
        }
    }

    private static bool TrySelectRandomArrayedBlade(
        ref ArtifactSwordArrayExecutionState state,
        int sequence,
        out int bladeIndex)
    {
        int arrayedCount = CountArrayedBlades(ref state);
        if (arrayedCount == 0)
        {
            bladeIndex = -1;
            return false;
        }

        int ordinal = Mathf.Min(
            arrayedCount - 1,
            Mathf.FloorToInt(Sample01(state.artifact.Id, sequence, 101) * arrayedCount));
        for (int i = 0; i < state.blades.Length; i++)
        {
            if (state.blades[i].phase != ArtifactSwordArrayBladePhase.Arrayed) continue;
            if (ordinal-- > 0) continue;
            bladeIndex = i;
            return true;
        }

        bladeIndex = -1;
        return false;
    }

    private static void BeginTraversal(
        ref ArtifactSwordArrayBladeState blade,
        Vector2 travelDirection,
        Vector2 ringCenter,
        float actorScale,
        int bladeCount,
        float now,
        int variationSeed,
        int sequence)
    {
        float radius = ResolveRingRadius(actorScale, bladeCount);
        Vector2 destination = ResolveCircleExit(
            blade.position,
            ringCenter,
            travelDirection,
            radius);
        float speedVariation = Mathf.Lerp(
            0.84f,
            1.18f,
            Sample01(variationSeed, sequence, 37));
        float distance = Vector2.Distance(blade.position, destination);

        blade.phase = ArtifactSwordArrayBladePhase.Traversing;
        blade.phase_origin = blade.position;
        blade.phase_center = ringCenter;
        blade.phase_destination = destination;
        blade.travel_direction = travelDirection;
        blade.phase_started_at = now;
        blade.phase_duration = Mathf.Clamp(
            distance / (TraversalSpeed * actorScale * speedVariation),
            MinimumTraversalDuration,
            MaximumTraversalDuration);
        blade.direction = travelDirection;
        blade.entity.GetComponent<SkillHitMemory>().TargetIds.Clear();
    }

    private static void UpdateTraversing(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        float actorScale,
        float now)
    {
        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        Vector2 ringCenter = ResolveRingCenter(owner, actorScale);
        Vector2 centerTranslation = ringCenter - blade.phase_center;
        Vector2 origin = blade.phase_origin + centerTranslation;
        Vector2 destination = blade.phase_destination + centerTranslation;
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at) / Mathf.Max(0.01f, blade.phase_duration));
        blade.position = Vector2.LerpUnclamped(origin, destination, progress);
        blade.direction = blade.travel_direction;
        if (progress < 1f) return;

        Vector2 arrivalDirection = Normalize(
            blade.phase_destination - blade.phase_center,
            blade.travel_direction);
        int destinationSlot = ResolveSlotForDirection(
            arrivalDirection,
            state.blades.Length,
            state.ring_angle);
        ReinsertBlade(ref state, bladeIndex, destinationSlot);
        ref ArtifactSwordArrayBladeState returnedBlade = ref state.blades[bladeIndex];
        returnedBlade.phase = ArtifactSwordArrayBladePhase.Arrayed;
        returnedBlade.position = ResolveRingPosition(
            owner,
            destinationSlot,
            state.blades.Length,
            state.blades.Length,
            actorScale,
            state.ring_angle);
        returnedBlade.direction = Vector2.down;
    }

    /// <summary>将横贯圆阵的剑影插入目标侧阵位，并让沿途阵位整体轮换。</summary>
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
    }

    private static Vector2 ResolveCollapseCenter(ref ArtifactSwordArrayExecutionState state)
    {
        return state.artifact.GetComponent<Position>().v2;
    }

    private static void SyncBladeEntities(
        Actor owner,
        ref ArtifactSwordArrayExecutionState state,
        float now,
        float collapseProgress)
    {
        float visibility = owner.is_visible ? 1f - collapseProgress : 0f;
        for (int i = 0; i < state.blades.Length; i++)
        {
            ref ArtifactSwordArrayBladeState blade = ref state.blades[i];
            Entity entity = blade.entity;
            entity.GetComponent<PrevPosition>().Value = blade.previous_position;
            entity.GetComponent<Position>().value = blade.position;
            entity.GetComponent<Rotation>().in_plane = blade.direction;

            float formingAlpha = blade.phase == ArtifactSwordArrayBladePhase.Forming
                ? Mathf.Clamp01(
                    (now - blade.phase_started_at) /
                    Mathf.Max(0.01f, blade.phase_duration))
                : 1f;
            entity.GetComponent<AnimTint>().Value = new Color(1f, 1f, 1f, visibility * formingAlpha);

            bool moving = blade.phase == ArtifactSwordArrayBladePhase.Traversing;
            entity.GetComponent<AnimAfterimage>().Count = moving ? MovingAfterimageCount : 0;
        }
    }

    private static void DisableBladeColliders(ref ArtifactSwordArrayExecutionState state)
    {
        for (int i = 0; i < state.blades.Length; i++)
        {
            state.blades[i].entity.GetComponent<ColliderConfig>().Enabled = false;
        }
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
        Vector2 center = ResolveRingCenter(owner, actorScale);
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static Vector2 ResolveRingCenter(Actor owner, float actorScale)
    {
        return owner.cur_transform_position + Vector3.up * actorScale * 0.52f;
    }

    private static int ResolveSlotForDirection(Vector2 direction, int bladeCount, float ringAngle)
    {
        float directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float relativeAngle = Mathf.Repeat(directionAngle - ringAngle, 360f);
        int slot = Mathf.RoundToInt(relativeAngle / 360f * bladeCount);
        return PositiveModulo(slot, bladeCount);
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

    private static float ResolveLaunchInterval(ref ArtifactSwordArrayExecutionState state, int sequence)
    {
        float expectedTraversalDuration = 2f * Mathf.Sqrt(state.blades.Length) / TraversalSpeed;
        float baseInterval = expectedTraversalDuration / Mathf.Max(1, state.target_in_flight);
        float variation = Mathf.Lerp(
            0.62f,
            1.38f,
            Sample01(state.artifact.Id, sequence, 47));
        return Mathf.Max(0.006f, baseInterval * variation);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private static Vector2 ResolveCircleExit(
        Vector2 origin,
        Vector2 center,
        Vector2 direction,
        float radius)
    {
        Vector2 offset = origin - center;
        float projection = Vector2.Dot(offset, direction);
        float discriminant = projection * projection + radius * radius - offset.sqrMagnitude;
        if (discriminant <= 0f) return center + direction * radius;

        float distance = -projection + Mathf.Sqrt(discriminant);
        return distance > 0.01f
            ? origin + direction * distance
            : center + direction * radius;
    }

    private static float Sample01(int seed, int sequence, int salt)
    {
        unchecked
        {
            uint value = (uint)seed;
            value ^= (uint)sequence * 0x9E3779B9u;
            value ^= (uint)salt * 0x85EBCA6Bu;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }
    }

    private static Vector2 Normalize(Vector2 value, Vector2 fallback)
    {
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.up;
    }

}
