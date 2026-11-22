using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
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

    /// <summary>剑影保留可辨认的器身，绘制与碰撞使用同一比例。</summary>
    internal const float BladeSizeRatio = 0.65f;

    internal static bool Initialize(
        Entity execution,
        Actor owner,
        Entity artifact,
        int bladeCount,
        float attackRange,
        float duration)
    {
        if (!ArtifactAppearanceRenderer.TryGetProjectionGeometry(artifact, out var geometry)) return false;
        int count = Mathf.Clamp(bladeCount, 8, MaxBladeCount);
        float resolvedDuration = Mathf.Max(3f, duration);
        float formationDuration = Mathf.Min(.78f, resolvedDuration * .12f);
        float now = (float)World.world.getCurWorldTime();
        float worldSize = ArtifactManifestationTools.ResolveActiveWorldSize(artifact, owner);
        float bladeSize = Mathf.Max(worldSize * BladeSizeRatio, Mathf.Min(3f, BattleSpellScale.Resolve(owner.GetExtend()) * .3f));
        Vector3 origin = ResolveArtifactPosition(artifact, owner);
        SkillContext bladeContext = execution.GetComponent<SkillContext>();
        ArtifactSwordArrayBladeState[] blades = new ArtifactSwordArrayBladeState[count];
        var pose = ArtifactAppearanceRenderer.Project(in geometry, origin, Vector2.down, 0f, bladeSize, 0f);
        var body = geometry.Contact(pose);

        for (int i = 0; i < count; i++)
        {
            Entity bladeEntity = ArtifactSkillExecutions.SwordArrayBlade.NewEntity();
            bladeEntity.GetComponent<SkillContext>() = bladeContext;
            bladeEntity.GetComponent<Position>().value = origin;
            bladeEntity.GetComponent<ColliderSphere>().Radius = geometry.Radius * pose.Scale.y;
            bladeEntity.AddComponent(new ArtifactSpatialProjection { Pose = pose });
            bladeEntity.AddComponent(body);
            if (!SkillExecutionLifecycle.TryBindSpawnedBody(execution, bladeEntity, "blade"))
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(bladeEntity.Id);
                return false;
            }
            blades[i] = new ArtifactSwordArrayBladeState
            {
                entity = bladeEntity,
                slot_index = i,
                phase = ArtifactSwordArrayBladePhase.Forming,
                position = origin,
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
            collapse_duration = Mathf.Min(.5f, resolvedDuration * .08f),
            next_launch_attempt_at = now + formationDuration,
            attack_range = attackRange,
            ring_angle = 90f,
            angular_speed = RingAngularSpeed,
            geometry = geometry,
            slot_order = new int[count],
            blade_size = bladeSize,
        };
        execution.GetComponent<Position>().value = SpatialActorPose.Foot(owner);
        return true;
    }

    /// <summary>剑影只在本次生成它的根执行仍有效时存在及参与接触。</summary>
    internal static bool IsActiveBlade(Entity blade) => !blade.Tags.Has<TagRecycle>() &&
        blade.TryGetComponent(out SkillExecutionBodyLease lease) &&
        lease.execution.IsAvailable() && !lease.execution.GetComponent<SkillExecution>().end_requested;

    /// <summary>主动收阵即时停伤，保持原会话到剑影实际收束完毕。</summary>
    internal static bool TryRecall(Entity execution)
    {
        if (!execution.IsAvailable() || execution.GetComponent<SkillExecution>().end_requested) return false;
        ref var state = ref execution.GetComponent<ArtifactSwordArrayExecutionState>();
        if (state.recalled || state.collapsing) return false;
        float elapsed = (float)World.world.getCurWorldTime() - state.started_at;
        if (elapsed >= state.duration - state.collapse_duration) return false;
        state.recalled = true;
        state.duration = elapsed + state.collapse_duration;
        DisableBladeColliders(ref state);
        return true;
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
        if (owner == null || owner.isRekt() || owner.id != context.SourceId ||
            !state.artifact.IsAvailable() || !ArtifactEquipmentTools.IsArtifactEquipped(owner.GetExtend(), state.artifact))
        {
            DisableBladeColliders(ref state);
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        float now = (float)World.world.getCurWorldTime();
        float elapsed = now - state.started_at;
        position.value = SpatialActorPose.Foot(owner);
        rotation.z = 0f;
        if (elapsed >= state.duration)
        {
            DisableBladeColliders(ref state);
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        float collapseStart = state.duration - state.collapse_duration;
        if (elapsed >= collapseStart)
        {
            float collapse = Mathf.Clamp01((elapsed - collapseStart) / state.collapse_duration);
            UpdateCollapse(owner, ref state, collapse);
            SyncBladeEntities(ref state, now, deltaTime, 1f - collapse);
            return;
        }

        float actorScale = ResolveFormationScale(owner);
        float ringHeight = owner.position_height + SpatialActorPose.Height(owner) * .6f;
        if (elapsed >= state.formation_duration)
        {
            state.ring_angle = Mathf.Repeat(
                state.ring_angle + state.angular_speed * Mathf.Max(0f, deltaTime),
                360f);
        }

        for (int i = 0; i < state.blades.Length; i++)
        {
            ref ArtifactSwordArrayBladeState blade = ref state.blades[i];
            switch (blade.phase)
            {
                case ArtifactSwordArrayBladePhase.Forming:
                    UpdateForming(ref blade, ResolveRingPosition(owner.current_position, ringHeight, blade.slot_index,
                        state.blades.Length, state.blades.Length, actorScale, state.ring_angle), now, i);
                    break;
                case ArtifactSwordArrayBladePhase.Traversing:
                    UpdateTraversing(owner.current_position, ref state, i, ringHeight, now);
                    break;
            }
        }

        UpdateArrayedBlades(owner.current_position, ref state, actorScale, ringHeight, deltaTime, now);
        float launchStopAt = state.started_at + collapseStart - MaximumTraversalDuration;
        if (elapsed >= state.formation_duration && now < launchStopAt && now >= state.next_launch_attempt_at)
            LaunchDueBlades(owner, ref state, actorScale, now);

        SyncBladeEntities(ref state, now, deltaTime, 1f);
    }

    private static void UpdateForming(
        ref ArtifactSwordArrayBladeState blade,
        Vector3 ringPosition,
        float now,
        int bladeIndex)
    {
        float stagger = bladeIndex % 8 * 0.012f;
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at - stagger) / Mathf.Max(0.01f, blade.phase_duration - stagger));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        blade.position = Vector3.LerpUnclamped(blade.phase_origin, ringPosition, eased);
        if (progress < 1f) return;
        blade.phase = ArtifactSwordArrayBladePhase.Arrayed;
        blade.phase_started_at = now;
    }

    private static void UpdateArrayedBlades(Vector2 center, ref ArtifactSwordArrayExecutionState state,
        float actorScale, float ringHeight, float deltaTime, float now)
    {
        int[] slots = state.slot_order;
        int visible = 0;
        for (int i = 0; i < state.blades.Length; i++)
        {
            var blade = state.blades[i];
            slots[blade.slot_index] = i;
            if (blade.phase == ArtifactSwordArrayBladePhase.Arrayed) visible++;
        }

        float follow = 1f - Mathf.Exp(-RingFollowResponse * Mathf.Max(0f, deltaTime));
        int rank = 0;
        foreach (int index in slots)
        {
            ref var blade = ref state.blades[index];
            if (blade.phase != ArtifactSwordArrayBladePhase.Arrayed) continue;
            Vector3 destination = ResolveRingPosition(center, ringHeight, rank++, visible, state.blades.Length, actorScale, state.ring_angle);
            if (blade.phase_started_at < now) blade.position = Vector3.LerpUnclamped(blade.position, destination, follow);
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

        Vector2 ringCenter = owner.current_position;
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
            Actor target = targets[targetIndex];
            Vector2 targetDirection = Normalize(
                target.current_position - (Vector2)blade.position,
                ringCenter - (Vector2)blade.position);
            float directionJitter = Mathf.Lerp(
                -DirectionJitter,
                DirectionJitter,
                Sample01(state.artifact.Id, sequence, 23));
            Vector2 travelDirection = Rotate(targetDirection, directionJitter);
            BeginTraversal(
                ref blade,
                travelDirection,
                ringCenter,
                target,
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
        Actor target,
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
        float contact = Vector2.Dot(target.current_position - (Vector2)blade.position, travelDirection) /
            Mathf.Max(.01f, distance);
        blade.contact_height = target.position_height + SpatialActorPose.Height(target) * .49f;
        blade.approach_fraction = Mathf.Clamp(contact, .04f, .18f);
        blade.departure_fraction = Mathf.Clamp(contact, .78f, .95f);

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
        blade.entity.GetComponent<SkillHitMemory>().TargetIds.Clear();
    }

    private static void UpdateTraversing(
        Vector2 ringCenter,
        ref ArtifactSwordArrayExecutionState state,
        int bladeIndex,
        float ringHeight,
        float now)
    {
        ref ArtifactSwordArrayBladeState blade = ref state.blades[bladeIndex];
        Vector2 centerTranslation = ringCenter - blade.phase_center;
        Vector2 origin = (Vector2)blade.phase_origin + centerTranslation;
        Vector2 destination = blade.phase_destination + centerTranslation;
        float progress = Mathf.Clamp01(
            (now - blade.phase_started_at) / Mathf.Max(.01f, blade.phase_duration));
        Vector2 point = Vector2.LerpUnclamped(origin, destination, progress);
        float height = progress < blade.approach_fraction
            ? Mathf.SmoothStep(blade.phase_origin.z, blade.contact_height, progress / blade.approach_fraction)
            : Mathf.SmoothStep(blade.contact_height, ringHeight,
                (progress - blade.departure_fraction) / (1f - blade.departure_fraction));
        blade.position = new Vector3(point.x, point.y, height);
        if (progress < 1f) return;

        Vector2 arrivalDirection = Normalize(blade.phase_destination - blade.phase_center, blade.travel_direction);
        int destinationSlot = ResolveSlotForDirection(arrivalDirection, state.blades.Length, state.ring_angle);
        ReinsertBlade(ref state, bladeIndex, destinationSlot);
        blade.phase = ArtifactSwordArrayBladePhase.Arrayed;
        blade.phase_started_at = now;
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

    private static Vector3 ResolveArtifactPosition(Entity artifact, Actor owner) =>
        ArtifactAppearanceRenderer.TryGetSpatialPose(artifact, out var origin) ? origin.Position :
            SpatialActorPose.Foot(owner) + Vector3.forward * (SpatialActorPose.Height(owner) * .55f);

    private static void UpdateCollapse(Actor owner, ref ArtifactSwordArrayExecutionState state, float progress)
    {
        Vector3 destination = ResolveArtifactPosition(state.artifact, owner);
        float portion = Mathf.SmoothStep(0f, 1f, progress);
        for (int i = 0; i < state.blades.Length; i++)
        {
            ref var blade = ref state.blades[i];
            if (!state.collapsing) blade.phase_origin = blade.position;
            blade.position = Vector3.LerpUnclamped(blade.phase_origin, destination, portion);
        }
        state.collapsing = true;
    }

    private static void SyncBladeEntities(ref ArtifactSwordArrayExecutionState state, float now, float deltaTime, float visibility)
    {
        for (int i = 0; i < state.blades.Length; i++)
        {
            ref var blade = ref state.blades[i];
            Entity entity = blade.entity;
            ref Position position = ref entity.GetComponent<Position>();
            Vector3 change = blade.position - position.value;
            Vector2 direction = blade.phase == ArtifactSwordArrayBladePhase.Traversing ? blade.travel_direction : Vector2.down;
            if (state.collapsing) direction = Normalize(change, blade.direction);
            blade.direction = (Vector2)Vector3.RotateTowards(blade.direction, direction, 1120f * Mathf.Deg2Rad * deltaTime, 0f);
            float slope = Mathf.Atan2(change.z, Mathf.Max(.015f, ((Vector2)change).magnitude)) * Mathf.Rad2Deg;
            float pitch = blade.phase == ArtifactSwordArrayBladePhase.Arrayed && !state.collapsing
                ? -16f : Mathf.Clamp(slope, -52f, 52f);
            blade.pitch = Mathf.MoveTowards(blade.pitch, pitch, 480f * deltaTime);
            position.value = blade.position;

            float opacity = blade.phase == ArtifactSwordArrayBladePhase.Forming && !state.collapsing
                ? Mathf.Clamp01((now - blade.phase_started_at) / Mathf.Max(.01f, blade.phase_duration)) : 1f;
            var pose = ArtifactAppearanceRenderer.Project(in state.geometry, blade.position, blade.direction,
                blade.pitch, state.blade_size, visibility * opacity);
            entity.GetComponent<ArtifactSpatialProjection>().Pose = pose;

            var contact = state.geometry.Contact(pose);
            ref ColliderConfig collider = ref entity.GetComponent<ColliderConfig>();
            ref ColliderSweptBody swept = ref entity.GetComponent<ColliderSweptBody>();
            if (collider.Enabled) swept.Move(contact);
            else swept = contact;
            collider.Enabled = !state.collapsing && blade.phase != ArtifactSwordArrayBladePhase.Forming;
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
        CombatTargeting.ForEachHostile(
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

    private static Vector3 ResolveRingPosition(
        Vector2 center,
        float height,
        int rank,
        int visibleCount,
        int totalCount,
        float actorScale,
        float ringAngle)
    {
        float angle = (ringAngle + rank * 360f / Mathf.Max(1, visibleCount)) * Mathf.Deg2Rad;
        float radius = ResolveRingRadius(actorScale, totalCount);
        Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        return new Vector3(point.x, point.y, height);
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

    /// <summary>高阶御剑扩大真实阵位和剑影横贯路径，原版与三维读取同一位置。</summary>
    /// <param name="owner">实际御器者。</param>
    private static float ResolveFormationScale(Actor owner) => Mathf.Max(owner.stats[S.scale], .1f) * 10f *
                                                               Mathf.Sqrt(Combat.BattleSpellScale.Resolve(
                                                                   owner.GetExtend()));

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
