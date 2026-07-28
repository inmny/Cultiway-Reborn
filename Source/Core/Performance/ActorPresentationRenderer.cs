using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 将一份已发布的角色快照物化为原版 QuantumSprite 所需的连续数组。
/// 基础角色、手持物与阴影完全使用快照；原版 Actor 列表只保留稳定句柄到对象的兼容映射。
/// </summary>
internal static class ActorPresentationRenderer
{
    private static readonly HashSet<long> selectedActorIds = new();
    private static readonly HashSet<long> squareSelectionActorIds = new();
    private static readonly Dictionary<Actor, long> actorIds =
        new(ActorReferenceComparer.Instance);

    private static ActorPresentationSnapshot preparedSnapshot;
    private static int[] visibleSampleIndices = Array.Empty<int>();
    private static int[] renderDataIndices = Array.Empty<int>();
    private static int[] continuousSampleIndices = Array.Empty<int>();
    private static Vector3[] presentedPositions = Array.Empty<Vector3>();
    private static Vector2[] presentedShadowPositions = Array.Empty<Vector2>();
    private static bool[] baseVisibleSamples = Array.Empty<bool>();
    private static bool[] presentationVisibleSamples = Array.Empty<bool>();
    private static bool[] zoneVisibleSamples = Array.Empty<bool>();
    private static long lastSnapshotTick;
    private static int lastSnapshotCount;
    private static int lastVisibleCount;
    private static int continuousSampleCount;
    private static int lastBridgedActorCount;
    private static int lastMissingActorCount;
    private static int lastPreparedFrame = -1;
    private static long controlledActorId = long.MinValue;
    private static long primarySelectedActorId = long.MinValue;
    private static bool selectedMetaSet;
    private static MetaType selectedMetaType;
    private static long selectedMetaId;
    private static long preparedFrames;
    private static long fullPreparedFrames;
    private static long reusedPreparedFrames;
    private static long totalPrepareTicks;
    private static long maximumPrepareTicks;
    private static long lastPrepareTicks;
    private static ulong lastVisibilitySignature;
    private static bool lastSmoothingEnabled;

    internal static bool TryPrepare(
        ActorManager manager,
        ActorPresentationSnapshot snapshot)
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            manager == null ||
            snapshot == null ||
            snapshot.WorldGeneration != SimulationTime.Generation ||
            !ReferenceEquals(manager, World.world?.units))
        {
            return false;
        }

        long startedAt = Stopwatch.GetTimestamp();
        EnsurePresentationCapacity(snapshot.Count);
        RefreshSelectionHandles();
        bool renderGameplay = MapBox.isRenderGameplay();
        ulong visibilitySignature =
            PresentationVisibility.GetSignature(renderGameplay);
        if (ReferenceEquals(snapshot, preparedSnapshot) &&
            visibilitySignature == lastVisibilitySignature &&
            lastSmoothingEnabled ==
            PerformanceSettings.EnablePresentationSmoothing)
        {
            RefreshContinuousPresentation(manager, snapshot);
            Interlocked.Increment(ref reusedPreparedFrames);
            lastPreparedFrame = Time.frameCount;
            ActorTransientPresentationFrame.Prepare();
            RecordPrepareDuration(Stopwatch.GetTimestamp() - startedAt);
            return true;
        }

        PrepareArrays(manager, snapshot.Count);
        Interlocked.Increment(ref fullPreparedFrames);
        ActorRenderData renderData = manager.render_data;
        int visibleCount = 0;
        continuousSampleCount = 0;

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample sample = ref snapshot.GetAt(i);
            TileZone zone = GetZone(sample.ZoneId);
            bool zoneVisible = zone?.visible == true;
            bool presentationVisible;
            if (sample.HasFlag(ActorPresentationFlags.InMagnet) ||
                sample.HasFlag(ActorPresentationFlags.InsideSomething))
            {
                presentationVisible = false;
            }
            else
            {
                presentationVisible = renderGameplay
                    ? zoneVisible
                    : sample.HasFlag(ActorPresentationFlags.VisibleOnMinimap);
            }

            baseVisibleSamples[i] = false;
            presentationVisibleSamples[i] = presentationVisible;
            zoneVisibleSamples[i] = zoneVisible;
            renderDataIndices[i] = -1;
            if (!renderGameplay || !zoneVisible || !presentationVisible)
            {
                continue;
            }

            bool selected = selectedActorIds.Contains(sample.Handle.ActorId);
            bool controlled = sample.Handle.ActorId == controlledActorId;
            if (!PresentationInterpolator.TryResolve(
                    in sample,
                    selected,
                    controlled,
                    out Vector3 transformPosition,
                    out Vector2 shadowPosition,
                    out bool requiresContinuousUpdate))
            {
                continue;
            }

            visibleSampleIndices[visibleCount] = i;
            renderDataIndices[i] = visibleCount;
            presentedPositions[i] = transformPosition;
            presentedShadowPositions[i] = shadowPosition;
            baseVisibleSamples[i] = true;
            FillRenderData(
                renderData,
                visibleCount,
                in sample,
                transformPosition,
                shadowPosition);
            if (requiresContinuousUpdate)
            {
                continuousSampleIndices[continuousSampleCount++] = i;
            }

            visibleCount++;
        }

        int missingActorCount = PrepareLegacyLists(manager, snapshot);
        preparedSnapshot = snapshot;
        lastPreparedFrame = Time.frameCount;
        lastSnapshotTick = snapshot.TickSequence;
        lastSnapshotCount = snapshot.Count;
        lastVisibleCount = visibleCount;
        lastVisibilitySignature = visibilitySignature;
        lastSmoothingEnabled =
            PerformanceSettings.EnablePresentationSmoothing;
        lastMissingActorCount = missingActorCount;
        ActorTransientPresentationFrame.Prepare();
        RecordPrepareDuration(Stopwatch.GetTimestamp() - startedAt);
        return true;
    }

    internal static bool TryUseBaseVisibleCount(
        ActorManager manager,
        out int previousCount)
    {
        previousCount = 0;
        if (manager == null ||
            !PerformanceSettings.EnableFramePriorityScheduler ||
            lastPreparedFrame != Time.frameCount ||
            preparedSnapshot == null ||
            preparedSnapshot.WorldGeneration != SimulationTime.Generation ||
            !ReferenceEquals(manager, World.world?.units))
        {
            return false;
        }

        previousCount = manager.visible_units.count;
        manager.visible_units.count = lastVisibleCount;
        return true;
    }

    internal static void RestoreVisibleCount(
        ActorManager manager,
        int previousCount)
    {
        if (manager != null)
        {
            manager.visible_units.count = previousCount;
        }
    }

    internal static bool TryGetPresented(
        long actorId,
        out ActorPresentationSample sample,
        out Vector3 position,
        out Vector2 shadowPosition)
    {
        ActorPresentationSnapshot snapshot = preparedSnapshot;
        if (lastPreparedFrame != Time.frameCount ||
            snapshot == null ||
            snapshot.WorldGeneration != SimulationTime.Generation ||
            !snapshot.TryGetIndex(actorId, out int sampleIndex))
        {
            sample = default;
            position = default;
            shadowPosition = default;
            return false;
        }

        sample = snapshot.GetAt(sampleIndex);
        if (!baseVisibleSamples[sampleIndex] &&
            !PresentationInterpolator.TryResolve(
                in sample,
                selectedActorIds.Contains(actorId),
                actorId == controlledActorId,
                out presentedPositions[sampleIndex],
                out presentedShadowPositions[sampleIndex]))
        {
            position = default;
            shadowPosition = default;
            return false;
        }

        position = presentedPositions[sampleIndex];
        shadowPosition = presentedShadowPositions[sampleIndex];
        return true;
    }

    internal static bool TryGetPresentationState(
        long actorId,
        out ActorPresentationSample sample,
        out Vector3 position,
        out bool presentationVisible,
        out bool zoneVisible)
    {
        ActorPresentationSnapshot snapshot = preparedSnapshot;
        if (snapshot == null ||
            snapshot.WorldGeneration != SimulationTime.Generation ||
            !snapshot.TryGetIndex(actorId, out int sampleIndex) ||
            !TryGetPresented(
                actorId,
                out sample,
                out position,
                out _))
        {
            sample = default;
            position = default;
            presentationVisible = false;
            zoneVisible = false;
            return false;
        }

        presentationVisible = presentationVisibleSamples[sampleIndex];
        zoneVisible = zoneVisibleSamples[sampleIndex];
        return true;
    }

    /// <summary>
    /// Mod 表现系统的统一入口。调度器开启时绝不退回实时 Actor；
    /// 调度器关闭时则保留原有表现行为。
    /// </summary>
    internal static bool TryGetPresentationStateForRender(
        long actorId,
        Actor liveActor,
        out ActorPresentationSample sample,
        out Vector3 position,
        out bool presentationVisible,
        out bool zoneVisible)
    {
        if (TryGetPresentationState(
                actorId,
                out sample,
                out position,
                out presentationVisible,
                out zoneVisible))
        {
            return true;
        }

        if (PerformanceSettings.EnableFramePriorityScheduler ||
            CooperativeSimulationRunner.Instance
                .HasMutatingPresentationWorkInFlight ||
            liveActor?.data == null ||
            liveActor.isRekt())
        {
            sample = default;
            position = default;
            presentationVisible = false;
            zoneVisible = false;
            return false;
        }

        ActorPresentationFlags flags = ActorPresentationFlags.None;
        if (liveActor.isAlive())
        {
            flags |= ActorPresentationFlags.Alive;
        }

        bool flying = liveActor.data.hasFlag(
            global::Cultiway.Content.Const.ContentActorDataKeys
                .IsFlying_flag);
        Sprite flyingVehicleSprite = null;
        bool flyingVehicleVertical = false;
        Sprite flyingScaleReferenceSprite = null;
        if (flying)
        {
            flags |= ActorPresentationFlags.Flying;
            if (liveActor.asset.has_override_sprite)
            {
                flyingScaleReferenceSprite =
                    liveActor.calculateMainSprite();
            }
            else
            {
                liveActor.checkAnimationContainer();
                Sprite[] idleFrames =
                    liveActor.animation_container?.idle?.frames;
                flyingScaleReferenceSprite =
                    idleFrames is { Length: > 0 }
                        ? idleFrames[0]
                        : liveActor._last_main_sprite;
            }

            if (liveActor.hasWeapon())
            {
                flyingVehicleSprite =
                    ItemRendering.getItemMainSpriteFrame(
                        liveActor.getWeaponAsset());
                flyingVehicleVertical =
                    flyingVehicleSprite != null &&
                    flyingVehicleSprite.rect.width <
                    flyingVehicleSprite.rect.height;
            }
        }

        Sprite mainSprite = liveActor._last_main_sprite;
        if (mainSprite == null &&
            liveActor.asset?.ignore_generic_render != true)
        {
            mainSprite = liveActor.calculateMainSprite();
        }

        sample = new ActorPresentationSample
        {
            Handle = new ActorPresentationHandle(
                SimulationTime.Generation,
                actorId),
            Position = liveActor.current_position,
            Scale = liveActor.current_scale,
            Rotation = liveActor.current_rotation,
            MainSprite = mainSprite,
            VisualScale = liveActor.stats[strings.S.scale],
            Flip = liveActor.flip,
            FlyingVehicleSprite = flyingVehicleSprite,
            FlyingVehicleVertical = flyingVehicleVertical,
            FlyingScaleReferenceSprite =
                flyingScaleReferenceSprite,
            Flags = flags
        };
        position = liveActor.cur_transform_position;
        presentationVisible = liveActor.is_visible;
        zoneVisible = liveActor.current_tile?.zone?.visible == true;
        return true;
    }

    internal static ActorPresentationSnapshot PreparedSnapshot =>
        PerformanceSettings.EnableFramePriorityScheduler &&
        lastPreparedFrame == Time.frameCount &&
        preparedSnapshot?.WorldGeneration == SimulationTime.Generation
            ? preparedSnapshot
            : null;

    internal static bool IsSelected(long actorId)
    {
        return selectedActorIds.Contains(actorId);
    }

    internal static bool IsControlled(long actorId)
    {
        return actorId == controlledActorId;
    }

    internal static bool IsSquareSelected(long actorId)
    {
        return squareSelectionActorIds.Contains(actorId);
    }

    internal static long PrimarySelectedActorId =>
        primarySelectedActorId;

    internal static bool TryGetActorId(Actor actor, out long actorId)
    {
        if (actor != null && actorIds.TryGetValue(actor, out actorId))
        {
            return true;
        }

        actorId = 0L;
        return false;
    }

    internal static bool TryGetPreparedSample(
        Actor actor,
        out ActorPresentationSample sample)
    {
        ActorPresentationSnapshot snapshot = PreparedSnapshot;
        if (snapshot != null &&
            TryGetActorId(actor, out long actorId) &&
            snapshot.TryGet(actorId, out sample))
        {
            return true;
        }

        sample = default;
        return false;
    }

    internal static bool HasSelectedMeta => selectedMetaSet;
    internal static MetaType SelectedMetaType => selectedMetaType;
    internal static long SelectedMetaId => selectedMetaId;

    internal static int BaseVisibleCount => lastVisibleCount;

    internal static ref readonly ActorPresentationSample GetVisibleSample(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)lastVisibleCount ||
            preparedSnapshot == null)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        return ref preparedSnapshot.GetAt(visibleSampleIndices[visibleIndex]);
    }

    internal static Vector3 GetVisiblePosition(int visibleIndex)
    {
        if ((uint)visibleIndex >= (uint)lastVisibleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        }

        return presentedPositions[visibleSampleIndices[visibleIndex]];
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "tick={0} snapshot={1} visible={2} active={8} " +
            "prepare_frames={9}/{10}(full/reuse) " +
            "bridged={3} missing={4} " +
            "prepare={5:0.00}ms(avg={6:0.00},max={7:0.00})",
            lastSnapshotTick,
            lastSnapshotCount,
            lastVisibleCount,
            lastBridgedActorCount,
            lastMissingActorCount,
            TicksToMilliseconds(Interlocked.Read(ref lastPrepareTicks)),
            TicksToMilliseconds(Interlocked.Read(ref totalPrepareTicks)) /
            Math.Max(1L, Interlocked.Read(ref preparedFrames)),
            TicksToMilliseconds(Interlocked.Read(ref maximumPrepareTicks)),
            continuousSampleCount,
            Interlocked.Read(ref fullPreparedFrames),
            Interlocked.Read(ref reusedPreparedFrames));
    }

    internal static void Reset()
    {
        preparedSnapshot = null;
        selectedActorIds.Clear();
        squareSelectionActorIds.Clear();
        actorIds.Clear();
        controlledActorId = long.MinValue;
        primarySelectedActorId = long.MinValue;
        selectedMetaSet = false;
        selectedMetaType = MetaType.None;
        selectedMetaId = 0L;
        lastPreparedFrame = -1;
        lastSnapshotTick = 0;
        lastSnapshotCount = 0;
        lastVisibleCount = 0;
        continuousSampleCount = 0;
        lastBridgedActorCount = 0;
        lastMissingActorCount = 0;
        lastVisibilitySignature = 0UL;
        lastSmoothingEnabled = false;
        ActorTransientPresentationFrame.Reset();
    }

    private static TileZone GetZone(int zoneId)
    {
        ZoneCalculator calculator = World.world?.zone_calculator;
        if (calculator == null ||
            zoneId < 0 ||
            zoneId >= calculator.zones.Count)
        {
            return null;
        }

        return calculator.getZoneByID(zoneId);
    }

    private static void EnsurePresentationCapacity(int capacity)
    {
        if (visibleSampleIndices.Length < capacity)
        {
            int nextCapacity = Math.Max(4096, visibleSampleIndices.Length);
            while (nextCapacity < capacity)
            {
                nextCapacity = checked(nextCapacity * 2);
            }

            Array.Resize(ref visibleSampleIndices, nextCapacity);
            Array.Resize(ref renderDataIndices, nextCapacity);
            Array.Resize(ref continuousSampleIndices, nextCapacity);
            Array.Resize(ref presentedPositions, nextCapacity);
            Array.Resize(ref presentedShadowPositions, nextCapacity);
            Array.Resize(ref baseVisibleSamples, nextCapacity);
            Array.Resize(ref presentationVisibleSamples, nextCapacity);
            Array.Resize(ref zoneVisibleSamples, nextCapacity);
        }
    }

    private static void RefreshContinuousPresentation(
        ActorManager manager,
        ActorPresentationSnapshot snapshot)
    {
        ActorRenderData renderData = manager.render_data;
        int writeIndex = 0;
        for (int i = 0; i < continuousSampleCount; i++)
        {
            int sampleIndex = continuousSampleIndices[i];
            int renderIndex = renderDataIndices[sampleIndex];
            if (renderIndex < 0)
            {
                continue;
            }

            ref readonly ActorPresentationSample sample =
                ref snapshot.GetAt(sampleIndex);
            long actorId = sample.Handle.ActorId;
            if (!PresentationInterpolator.TryResolve(
                    in sample,
                    selectedActorIds.Contains(actorId),
                    actorId == controlledActorId,
                    out Vector3 transformPosition,
                    out Vector2 shadowPosition,
                    out bool requiresContinuousUpdate))
            {
                continue;
            }

            presentedPositions[sampleIndex] = transformPosition;
            presentedShadowPositions[sampleIndex] = shadowPosition;
            FillDynamicRenderData(
                renderData,
                renderIndex,
                in sample,
                transformPosition,
                shadowPosition);
            if (requiresContinuousUpdate)
            {
                continuousSampleIndices[writeIndex++] = sampleIndex;
            }
        }

        continuousSampleCount = writeIndex;
        manager.visible_units.count = lastVisibleCount;
    }

    private static void RefreshSelectionHandles()
    {
        selectedActorIds.Clear();
        List<Actor> selectedActors = SelectedUnit.getAllSelectedList();
        for (int i = 0; i < selectedActors.Count; i++)
        {
            ActorData data = selectedActors[i]?.data;
            if (data != null)
            {
                selectedActorIds.Add(data.id);
            }
        }

        primarySelectedActorId =
            SelectedUnit.unit?.data?.id ?? long.MinValue;
        squareSelectionActorIds.Clear();
        PlayerControl playerControl = World.world?.player_control;
        if (playerControl?.square_selection_started == true)
        {
            using ListPool<Actor> candidates =
                playerControl.getUnitsToBeSelected();
            if (candidates != null)
            {
                foreach (ref Actor candidate in candidates)
                {
                    ActorData data = candidate?.data;
                    if (data != null)
                    {
                        squareSelectionActorIds.Add(data.id);
                    }
                }
            }
        }

        Actor controlled = ControllableUnit.getControllableUnit();
        controlledActorId = controlled?.data?.id ?? long.MinValue;

        NanoObject selectedMeta = SelectedObjects.getSelectedNanoObject();
        selectedMetaSet = selectedMeta != null;
        selectedMetaType = selectedMeta?.getMetaType() ?? MetaType.None;
        selectedMetaId = selectedMeta?.getID() ?? 0L;
    }

    private static void PrepareArrays(ActorManager manager, int capacity)
    {
        manager.visible_units.prepare(capacity);
        manager.visible_units_avatars.prepare(capacity);
        manager.visible_units_alive.prepare(capacity);
        manager.visible_units_with_status.prepare(capacity);
        manager.visible_units_with_favorite.prepare(capacity);
        manager.visible_units_with_banner.prepare(capacity);
        manager.visible_units_just_ate.prepare(capacity);
        manager.visible_units_socialize.prepare(capacity);
        manager.render_data.checkSize(capacity);

        manager.visible_units.count = 0;
        manager.visible_units_avatars.count = 0;
        manager.visible_units_alive.count = 0;
        manager.visible_units_with_status.count = 0;
        manager.visible_units_with_favorite.count = 0;
        manager.visible_units_with_banner.count = 0;
        manager.visible_units_just_ate.count = 0;
        manager.visible_units_socialize.count = 0;
    }

    private static int PrepareLegacyLists(
        ActorManager manager,
        ActorPresentationSnapshot snapshot)
    {
        int visibleCount = 0;
        int missingActorCount = 0;
        bool snapshotChanged =
            !ReferenceEquals(snapshot, preparedSnapshot);
        if (snapshotChanged)
        {
            actorIds.Clear();
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample sample = ref snapshot.GetAt(i);
            Actor actor = sample.ActorReference;
            if (actor == null)
            {
                missingActorCount++;
                continue;
            }

            if (snapshotChanged)
            {
                actorIds[actor] = sample.Handle.ActorId;
            }

            if (!baseVisibleSamples[i])
            {
                continue;
            }

            manager.visible_units.array[visibleCount++] = actor;
            if (sample.HasFlag(ActorPresentationFlags.Alive))
            {
                AddLegacyAliveLists(manager, actor);
            }

        }

        manager.visible_units.count = visibleCount;
        lastBridgedActorCount = actorIds.Count;
        return missingActorCount;
    }

    private static void AddLegacyAliveLists(ActorManager manager, Actor actor)
    {
        manager.visible_units_alive.array[manager.visible_units_alive.count++] = actor;
    }

    private static void FillRenderData(
        ActorRenderData renderData,
        int index,
        in ActorPresentationSample sample,
        Vector3 transformPosition,
        Vector2 shadowPosition)
    {
        Vector3 scale = sample.Scale;
        Vector3 rotation = sample.Rotation;
        bool normalRender = sample.HasFlag(ActorPresentationFlags.NormalRender);
        bool hasItem = sample.HasFlag(ActorPresentationFlags.HasItem);
        bool hasShadow = sample.HasFlag(ActorPresentationFlags.HasShadow);

        renderData.positions[index] = transformPosition;
        renderData.scales[index] = scale;
        renderData.rotations[index] = rotation;
        renderData.flip_x_states[index] = sample.Flip;
        renderData.colors[index] = sample.Color;
        renderData.has_normal_render[index] = normalRender;
        renderData.main_sprites[index] = sample.MainSprite;
        renderData.main_sprite_colored[index] = sample.MainSprite;
        renderData.materials[index] = null;

        renderData.has_item[index] = hasItem;
        renderData.item_sprites[index] = sample.ItemSprite;
        if (hasItem)
        {
            float itemScale = DebugConfig.isOn(DebugOption.RenderBigItems) ? 10f : 1f;
            renderData.item_scale[index] = scale * itemScale;
            renderData.item_pos[index] = CalculateItemPosition(
                transformPosition,
                rotation,
                scale,
                sample.ItemOffset);
        }

        renderData.shadows[index] = hasShadow;
        renderData.shadow_sprites[index] = sample.ShadowSprite;
        if (hasShadow)
        {
            CalculateShadow(
                in sample,
                shadowPosition,
                out Vector3 finalShadowPosition,
                out Vector3 finalShadowScale);
            renderData.shadow_position[index] = finalShadowPosition;
            renderData.shadow_scales[index] = finalShadowScale;
        }
    }

    private static void FillDynamicRenderData(
        ActorRenderData renderData,
        int index,
        in ActorPresentationSample sample,
        Vector3 transformPosition,
        Vector2 shadowPosition)
    {
        renderData.positions[index] = transformPosition;
        if (sample.HasFlag(ActorPresentationFlags.HasItem))
        {
            float itemScale =
                DebugConfig.isOn(DebugOption.RenderBigItems)
                    ? 10f
                    : 1f;
            renderData.item_scale[index] = sample.Scale * itemScale;
            renderData.item_pos[index] = CalculateItemPosition(
                transformPosition,
                sample.Rotation,
                sample.Scale,
                sample.ItemOffset);
        }

        if (sample.HasFlag(ActorPresentationFlags.HasShadow))
        {
            CalculateShadow(
                in sample,
                shadowPosition,
                out Vector3 finalShadowPosition,
                out Vector3 finalShadowScale);
            renderData.shadow_position[index] = finalShadowPosition;
            renderData.shadow_scales[index] = finalShadowScale;
        }
    }

    private static Vector3 CalculateItemPosition(
        Vector3 transformPosition,
        Vector3 rotation,
        Vector3 scale,
        Vector2 itemOffset)
    {
        float offsetX = itemOffset.x * scale.x;
        float offsetY = itemOffset.y * scale.y;
        Vector3 point = new(
            transformPosition.x + offsetX,
            transformPosition.y + offsetY);
        if (rotation.y != 0f || rotation.z != 0f)
        {
            Vector3 pivot = new(transformPosition.x, transformPosition.y, 0f);
            point = Toolbox.RotatePointAroundPivot(ref point, ref pivot, ref rotation);
        }

        point.z = -0.01f + offsetY;
        return point;
    }

    private static void CalculateShadow(
        in ActorPresentationSample sample,
        Vector2 shadowPosition,
        out Vector3 finalPosition,
        out Vector3 finalScale)
    {
        Vector2 shadowSize = sample.ShadowSize * (Vector2)sample.Scale;
        float rotation = sample.Rotation.z;
        float rotationAmount = Mathf.Abs(rotation);
        float flipDirection = sample.Flip ? 1f : -1f;
        shadowPosition.x += shadowSize.x * 0.5f * rotation * flipDirection / 90f;
        shadowPosition.y -= shadowSize.y * 0.6f * rotationAmount / 90f;
        finalPosition = shadowPosition;

        Vector2 frameSize = sample.FrameUnitSize;
        if (frameSize != default && shadowSize.x != 0f)
        {
            float rotatedScale =
                (frameSize * (Vector2)sample.Scale).y /
                shadowSize.x *
                sample.Scale.x;
            finalScale = new Vector3(
                Mathf.Lerp(sample.Scale.x, rotatedScale, rotationAmount / 90f),
                sample.Scale.y,
                sample.Scale.z);
            return;
        }

        finalScale = sample.Scale;
    }

    private static void RecordPrepareDuration(long elapsedTicks)
    {
        Interlocked.Increment(ref preparedFrames);
        Interlocked.Exchange(ref lastPrepareTicks, elapsedTicks);
        Interlocked.Add(ref totalPrepareTicks, elapsedTicks);
        long maximum = Interlocked.Read(ref maximumPrepareTicks);
        while (elapsedTicks > maximum)
        {
            long previous = Interlocked.CompareExchange(
                ref maximumPrepareTicks,
                elapsedTicks,
                maximum);
            if (previous == maximum)
            {
                break;
            }

            maximum = previous;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private sealed class ActorReferenceComparer : IEqualityComparer<Actor>
    {
        internal static ActorReferenceComparer Instance { get; } = new();

        public bool Equals(Actor left, Actor right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(Actor actor)
        {
            return RuntimeHelpers.GetHashCode(actor);
        }
    }
}
