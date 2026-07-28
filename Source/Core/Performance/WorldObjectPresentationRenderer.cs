using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Cultiway.Const;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 角色之外的世界对象表现读取器。所有绘制数据来自同一份已发布快照。
/// </summary>
internal static class WorldObjectPresentationRenderer
{
    private const int MaximumStockpileSlots = 35;

    private static readonly FieldInfo VisibleBuildingsField =
        AccessTools.Field(
            typeof(BuildingManager),
            "_array_visible_buildings");
    private static readonly FieldInfo VisibleBuildingCountField =
        AccessTools.Field(
            typeof(BuildingManager),
            "_visible_buildings_count");
    private static readonly FieldInfo FireSpriteSetsField =
        AccessTools.Field(
            typeof(QuantumSpriteLibrary),
            "_fire_sprites_sets");
    private static readonly Vector2[] StockpileSlots =
        CreateStockpileSlots();
    private static readonly Dictionary<Building, int>
        BuildingSnapshotIndexes = new(2048);

    private static ActorPresentationSnapshot preparedSnapshot;
    private static int[] stockpileRemaining = Array.Empty<int>();
    private static int[] stockpileActiveIndices = Array.Empty<int>();
    private static int lastPreparedFrame = -1;
    private static int visibleBuildingCount;
    private static int visibleStockpileCount;
    private static int stockpileIconCount;
    private static int lightWindowCount;
    private static int buildingLightCount;
    private static int buildingStatusCount;
    private static int worldLightCount;
    private static int visibleFireCount;
    private static long preparedFrames;
    private static long totalPrepareTicks;
    private static long maximumPrepareTicks;

    internal static ActorPresentationSnapshot PreparedSnapshot =>
        GetPreparedSnapshot();

    internal static bool TryPrepareBuildings(
        BuildingManager manager,
        ActorPresentationSnapshot snapshot)
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            manager == null ||
            snapshot == null ||
            snapshot.WorldGeneration != SimulationTime.Generation ||
            !ReferenceEquals(manager, World.world?.buildings))
        {
            return false;
        }

        long startedAt = Stopwatch.GetTimestamp();
        Building[] visibleBuildings =
            VisibleBuildingsField.GetValue(manager) as Building[] ??
            Array.Empty<Building>();
        if (visibleBuildings.Length < snapshot.BuildingCount)
        {
            Array.Resize(
                ref visibleBuildings,
                Math.Max(2048, snapshot.BuildingCount));
            VisibleBuildingsField.SetValue(manager, visibleBuildings);
        }

        manager.render_data.checkSize(snapshot.BuildingCount);
        manager.visible_stockpiles.Clear();
        manager.sparkles.Clear();
        bool snapshotChanged =
            !ReferenceEquals(snapshot, preparedSnapshot);
        if (snapshotChanged)
        {
            BuildingSnapshotIndexes.Clear();
        }

        visibleBuildingCount = 0;
        visibleStockpileCount = 0;
        stockpileIconCount = 0;
        lightWindowCount = 0;
        buildingLightCount = 0;
        buildingStatusCount = 0;
        worldLightCount = 0;
        if (World.world.quality_changer.shouldRenderBuildings())
        {
            for (int i = 0; i < snapshot.BuildingCount; i++)
            {
                ref readonly BuildingPresentationSample sample =
                    ref snapshot.GetBuildingAt(i);
                Building building = sample.BuildingReference;
                if (snapshotChanged && building != null)
                {
                    BuildingSnapshotIndexes[building] = i;
                }

                if (!IsZoneVisible(sample.ZoneId))
                {
                    continue;
                }

                int outputIndex = visibleBuildingCount++;
                FillRenderData(
                    manager.render_data,
                    outputIndex,
                    in sample);
                if (building == null)
                {
                    visibleBuildings[outputIndex] = null;
                    continue;
                }

                visibleBuildings[outputIndex] = building;
                if (sample.Stockpile)
                {
                    manager.visible_stockpiles.Add(building);
                }

                if (sample.Sparkle)
                {
                    manager.sparkles.Add(building);
                }
            }
        }

        VisibleBuildingCountField.SetValue(manager, visibleBuildingCount);
        preparedSnapshot = snapshot;
        lastPreparedFrame = Time.frameCount;
        RecordPrepareDuration(Stopwatch.GetTimestamp() - startedAt);
        return true;
    }

    internal static bool TryGetPresentationState(
        Building building,
        out BuildingPresentationSample sample,
        out bool visible)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot != null &&
            building != null &&
            BuildingSnapshotIndexes.TryGetValue(
                building,
                out int sampleIndex))
        {
            sample = snapshot.GetBuildingAt(sampleIndex);
            visible = IsZoneVisible(sample.ZoneId);
            return true;
        }

        sample = default;
        visible = false;
        return false;
    }

    internal static bool TryGetPresentationStateForRender(
        Building building,
        out BuildingPresentationSample sample,
        out bool visible)
    {
        if (TryGetPresentationState(
                building,
                out sample,
                out visible))
        {
            return true;
        }

        if (PerformanceSettings.EnableFramePriorityScheduler ||
            CooperativeSimulationRunner.Instance
                .HasMutatingPresentationWorkInFlight ||
            building?.data == null ||
            building.isRekt())
        {
            sample = default;
            visible = false;
            return false;
        }

        sample = new BuildingPresentationSample
        {
            BuildingId = building.getID(),
            BuildingReference = building,
            Position = building.cur_transform_position,
            Scale = building.getCurrentScale(),
            Rotation = building.current_rotation,
            Usable = building.isUsable(),
            UnderConstruction = building.isUnderConstruction()
        };
        visible = building.is_visible;
        return true;
    }

    internal static bool TryDrawBuildingLightWindows(
        QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        lightWindowCount = 0;
        if (!World.world.quality_changer.shouldRenderBuildings() ||
            !World.world.era_manager.shouldShowLights() ||
            !PlayerConfig.optionBoolEnabled("night_lights"))
        {
            return true;
        }

        Color color = Color.white;
        color.a = (Time.frameCount & 1) == 0 ? 0.95f : 1f;
        for (int i = 0; i < snapshot.BuildingCount; i++)
        {
            ref readonly BuildingPresentationSample building =
                ref snapshot.GetBuildingAt(i);
            if (!building.LightWindowVisible ||
                !IsZoneVisible(building.ZoneId))
            {
                continue;
            }

            Vector3 position = building.Position;
            position.z = -0.19f;
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(building.LightWindowSprite);
            visual.set(ref position, building.Scale.y);
            visual.setColor(ref color);
            lightWindowCount++;
        }

        return true;
    }

    internal static bool TryDrawBuildingLights(
        Building building,
        Color color)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null ||
            building == null ||
            !BuildingSnapshotIndexes.TryGetValue(
                building,
                out int sampleIndex))
        {
            return false;
        }

        ref readonly BuildingPresentationSample sample =
            ref snapshot.GetBuildingAt(sampleIndex);
        int end = sample.LightStart + sample.LightCount;
        for (int lightIndex = sample.LightStart;
             lightIndex < end;
             lightIndex++)
        {
            ref readonly BuildingLightPresentationSample light =
                ref snapshot.GetBuildingLightAt(lightIndex);
            QuantumSpriteLibrary.showLightAt(
                light.Position,
                color,
                light.Scale);
            buildingLightCount++;
        }

        return true;
    }

    internal static bool TryDrawLightAreas()
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null ||
            !ReferenceEquals(
                snapshot,
                ActorPresentationRenderer.PreparedSnapshot) ||
            !MapBox.isRenderGameplay())
        {
            return false;
        }

        worldLightCount = 0;
        buildingLightCount = 0;
        if (!PlayerConfig.optionBoolEnabled("night_lights") ||
            !World.world.era_manager.shouldShowLights())
        {
            return true;
        }

        Color white = Color.white;
        Color eraColor = QuantumSpriteLibrary.getColorForLight();
        if (World.world.heat_ray_fx.isReady())
        {
            QuantumSpriteLibrary.showLightAt(
                World.world.heat_ray_fx.getPosForLight(),
                white,
                1.5f);
            worldLightCount++;
        }

        for (int i = 0; i < snapshot.WorldLightCount; i++)
        {
            ref readonly WorldLightPresentationSample light =
                ref snapshot.GetWorldLightAt(i);
            Color color = light.UseEraColor ? eraColor : white;
            QuantumSpriteLibrary.showLightAt(
                light.Position,
                color,
                light.Scale);
            worldLightCount++;
        }

        ActorPresentationOverlays.TryDrawAllVisibleUnitLights(
            eraColor);
        for (int i = 0; i < snapshot.BuildingCount; i++)
        {
            ref readonly BuildingPresentationSample building =
                ref snapshot.GetBuildingAt(i);
            if (building.LightCount == 0 ||
                !IsZoneVisible(building.ZoneId))
            {
                continue;
            }

            int end = building.LightStart + building.LightCount;
            for (int lightIndex = building.LightStart;
                 lightIndex < end;
                 lightIndex++)
            {
                ref readonly BuildingLightPresentationSample light =
                    ref snapshot.GetBuildingLightAt(lightIndex);
                QuantumSpriteLibrary.showLightAt(
                    light.Position,
                    eraColor,
                    light.Scale);
                buildingLightCount++;
            }
        }

        if ((Config.isComputer || Config.isEditor) &&
            PlayerConfig.optionBoolEnabled("cursor_lights"))
        {
            QuantumSpriteLibrary.showLightAt(
                World.world.getMousePos(),
                white,
                0.4f);
            worldLightCount++;
        }

        return true;
    }

    internal static bool TryDrawFires(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        Sprite[][] spriteSets =
            FireSpriteSetsField?.GetValue(null) as Sprite[][];
        if (snapshot == null ||
            !ReferenceEquals(
                snapshot,
                ActorPresentationRenderer.PreparedSnapshot) ||
            spriteSets == null)
        {
            return false;
        }

        visibleFireCount = 0;
        float animationTime =
            AnimationHelper.getAnimationGlobalTime(10f);
        for (int i = 0; i < snapshot.FireCount; i++)
        {
            ref readonly FirePresentationSample sample =
                ref snapshot.GetFireAt(i);
            if ((uint)sample.AnimationSet >=
                (uint)spriteSets.Length)
            {
                continue;
            }

            Sprite[] frames = spriteSets[sample.AnimationSet];
            if (frames == null || frames.Length == 0)
            {
                continue;
            }

            int frameIndex =
                (int)(animationTime + sample.RandomSeed * 100f) %
                frames.Length;
            if (frameIndex < 0)
            {
                frameIndex += frames.Length;
            }

            Sprite sprite = frames[frameIndex];
            if (sprite == null)
            {
                continue;
            }

            Vector3 position = sample.Position;
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(sprite);
            visual.setPosOnly(ref position);
            visibleFireCount++;
        }

        return true;
    }

    internal static bool TryDrawStockpileResources(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        visibleStockpileCount = 0;
        stockpileIconCount = 0;
        float tween =
            World.world.quality_changer.getTweenBuildingsValue();
        for (int i = 0; i < snapshot.BuildingCount; i++)
        {
            ref readonly BuildingPresentationSample building =
                ref snapshot.GetBuildingAt(i);
            if (!building.StockpileVisible ||
                building.StockpileResourceCount <= 0 ||
                !IsZoneVisible(building.ZoneId))
            {
                continue;
            }

            DrawStockpile(asset, snapshot, in building, tween);
            visibleStockpileCount++;
        }

        return true;
    }

    internal static bool TryDrawBuildingStatuses(
        QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        buildingStatusCount = 0;
        if (!World.world.quality_changer.shouldRenderBuildings())
        {
            return true;
        }

        ActorPresentationOverlays.GetStatusTiming(
            snapshot,
            out float snapshotAge,
            out float simulationRate);
        for (int i = 0; i < snapshot.BuildingCount; i++)
        {
            ref readonly BuildingPresentationSample building =
                ref snapshot.GetBuildingAt(i);
            if (building.StatusCount == 0 ||
                !IsZoneVisible(building.ZoneId))
            {
                continue;
            }

            ActorPresentationOverlays.DrawStatusRange(
                asset,
                snapshot,
                building.StatusStart,
                building.StatusCount,
                building.Position,
                building.Rotation,
                snapshotAge,
                simulationRate);
            buildingStatusCount += building.StatusCount;
        }

        return true;
    }

    internal static bool TryDrawProjectiles(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.ProjectileCount; i++)
        {
            ref readonly ProjectilePresentationSample sample =
                ref snapshot.GetProjectileAt(i);
            Sprite sprite = ResolveProjectileSprite(in sample);
            if (sprite == null)
            {
                continue;
            }

            ResolveProjectilePresentation(
                snapshot,
                in sample,
                out Vector3 position,
                out _,
                out Quaternion rotation,
                out float scale,
                out float alpha);
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(sprite);
            visual.set(ref position, scale);
            visual.transform.rotation = rotation;
            Color color = new(1f, 1f, 1f, alpha);
            visual.setColor(ref color);
        }

        return true;
    }

    internal static bool TryDrawProjectileShadows(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        if (!Config.shadows_active)
        {
            return true;
        }

        for (int i = 0; i < snapshot.ProjectileCount; i++)
        {
            ref readonly ProjectilePresentationSample sample =
                ref snapshot.GetProjectileAt(i);
            if (sample.ShadowSprite == null)
            {
                continue;
            }

            ResolveProjectilePresentation(
                snapshot,
                in sample,
                out _,
                out Vector3 position,
                out _,
                out float scale,
                out _);
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(sample.ShadowSprite);
            visual.set(ref position, scale);
            visual.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                sample.ShadowAngle);
        }

        return true;
    }

    internal static bool TryDrawResourceThrows(
        QuantumSpriteAsset asset,
        bool shadows)
    {
        ActorPresentationSnapshot snapshot = GetPreparedSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        if (shadows && !Config.shadows_active)
        {
            return true;
        }

        double now = World.world.getCurSessionTime();
        for (int i = 0; i < snapshot.ResourceThrowCount; i++)
        {
            ref readonly ResourceThrowPresentationSample sample =
                ref snapshot.GetResourceThrowAt(i);
            if (sample.Sprite == null)
            {
                continue;
            }

            float ratio = ResolveThrowRatio(in sample, now);
            Vector3 position = shadows
                ? Vector2.Lerp(sample.Start, sample.End, ratio)
                : Toolbox.Parabola(
                    sample.Start,
                    sample.End,
                    sample.Height,
                    ratio);
            position.z = 4f;
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(sample.Sprite);
            visual.set(ref position, 0.1f);
            visual.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                ratio * 360f);
        }

        return true;
    }

    internal static string GetDiagnostics()
    {
        long frames = preparedFrames;
        return string.Format(
            CultureInfo.InvariantCulture,
            "frame={0} buildings={1} snapshot_buildings={2} " +
            "stockpiles={7}/{8} projectiles={3} throws={4} " +
            "windows={9} lights={10}+{12} statuses={11} fires={13} " +
            "prepare_avg={5:0.00}ms " +
            "max={6:0.00}ms",
            lastPreparedFrame,
            visibleBuildingCount,
            preparedSnapshot?.BuildingCount ?? 0,
            preparedSnapshot?.ProjectileCount ?? 0,
            preparedSnapshot?.ResourceThrowCount ?? 0,
            TicksToMilliseconds(totalPrepareTicks) / Math.Max(1L, frames),
            TicksToMilliseconds(maximumPrepareTicks),
            visibleStockpileCount,
            stockpileIconCount,
            lightWindowCount,
            buildingLightCount,
            buildingStatusCount,
            worldLightCount,
            visibleFireCount);
    }

    internal static void Reset()
    {
        preparedSnapshot = null;
        lastPreparedFrame = -1;
        visibleBuildingCount = 0;
        visibleStockpileCount = 0;
        stockpileIconCount = 0;
        lightWindowCount = 0;
        buildingLightCount = 0;
        buildingStatusCount = 0;
        worldLightCount = 0;
        visibleFireCount = 0;
        BuildingSnapshotIndexes.Clear();
    }

    private static ActorPresentationSnapshot GetPreparedSnapshot()
    {
        if (lastPreparedFrame != Time.frameCount ||
            preparedSnapshot == null ||
            preparedSnapshot.WorldGeneration != SimulationTime.Generation)
        {
            return null;
        }

        return preparedSnapshot;
    }

    private static void FillRenderData(
        BuildingRenderData renderData,
        int index,
        in BuildingPresentationSample sample)
    {
        renderData.positions[index] = sample.Position;
        renderData.scales[index] = sample.Scale;
        renderData.rotations[index] = sample.Rotation;
        renderData.main_sprites[index] = sample.MainSprite;
        renderData.colored_sprites[index] = sample.ColoredSprite;
        renderData.materials[index] = sample.Material;
        renderData.flip_x_states[index] = sample.Flip;
        renderData.colors[index] = sample.Color;
        renderData.shadows[index] = sample.HasShadow;
        renderData.shadow_sprites[index] = sample.ShadowSprite;
    }

    private static void DrawStockpile(
        QuantumSpriteAsset asset,
        ActorPresentationSnapshot snapshot,
        in BuildingPresentationSample building,
        float tween)
    {
        int resourceCount = building.StockpileResourceCount;
        EnsureStockpileScratchCapacity(resourceCount);
        for (int i = 0; i < resourceCount; i++)
        {
            ref readonly StockpileResourcePresentationSample resource =
                ref snapshot.GetStockpileResourceAt(
                    building.StockpileResourceStart + i);
            stockpileRemaining[i] = resource.IconCount;
            stockpileActiveIndices[i] = i;
        }

        Vector3 basePosition = building.Position;
        basePosition.x += building.StockpileOffset.x * tween;
        basePosition.y += building.StockpileOffset.y * tween;
        basePosition.z = 0f;
        int activeCount = resourceCount;
        int activeIndex = 0;
        int slotIndex = 0;
        while (slotIndex < MaximumStockpileSlots &&
               activeCount > 0)
        {
            int resourceIndex = stockpileActiveIndices[activeIndex];
            int remaining = stockpileRemaining[resourceIndex];
            if (remaining <= 0)
            {
                RemoveStockpileActiveIndex(
                    activeIndex,
                    ref activeCount);
                if (activeIndex >= activeCount)
                {
                    activeIndex = 0;
                }

                continue;
            }

            int row = (int)StockpileSlots[slotIndex].x;
            int column = (int)StockpileSlots[slotIndex].y;
            int drawCount = Mathf.Clamp(remaining, 1, 7);
            if ((column & 1) != 0)
            {
                drawCount--;
            }

            stockpileRemaining[resourceIndex] -= drawCount;
            ref readonly StockpileResourcePresentationSample resource =
                ref snapshot.GetStockpileResourceAt(
                    building.StockpileResourceStart + resourceIndex);
            for (int i = 0; i < drawCount; i++)
            {
                Vector3 position = basePosition;
                position.x += 0.58f * row;
                position.y -= 0.5f * column;
                if ((column & 1) != 0)
                {
                    position.x += 0.29f;
                }

                position.y += 0.4f * i;
                position.z += 0.5f * i;
                QuantumSprite visual = asset.group_system.getNext();
                visual.setSprite(resource.Sprite);
                visual.set(ref position, asset.base_scale);
                Color color = building.StockpileColor;
                visual.setColor(ref color);
                stockpileIconCount++;
            }

            slotIndex++;
            activeIndex++;
            if (activeIndex >= activeCount)
            {
                activeIndex = 0;
            }
        }
    }

    private static void RemoveStockpileActiveIndex(
        int index,
        ref int count)
    {
        count--;
        for (int i = index; i < count; i++)
        {
            stockpileActiveIndices[i] =
                stockpileActiveIndices[i + 1];
        }
    }

    private static void EnsureStockpileScratchCapacity(int capacity)
    {
        if (stockpileRemaining.Length < capacity)
        {
            Array.Resize(ref stockpileRemaining, capacity);
            Array.Resize(ref stockpileActiveIndices, capacity);
        }
    }

    private static Vector2[] CreateStockpileSlots()
    {
        var result = new Vector2[MaximumStockpileSlots];
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                result[y * 7 + x] = new Vector2(x, y);
            }
        }

        // 原版也只在首次绘制时打乱一次。使用局部固定种子，
        // 避免表现初始化消耗模拟随机数。
        var random = new System.Random(0x51A7);
        for (int i = result.Length - 1; i > 0; i--)
        {
            int target = random.Next(i + 1);
            (result[i], result[target]) =
                (result[target], result[i]);
        }

        return result;
    }

    private static bool IsZoneVisible(int zoneId)
    {
        ZoneCalculator calculator = World.world?.zone_calculator;
        return calculator != null &&
               zoneId >= 0 &&
               zoneId < calculator.zones.Count &&
               calculator.getZoneByID(zoneId)?.visible == true;
    }

    private static Sprite ResolveProjectileSprite(
        in ProjectilePresentationSample sample)
    {
        Sprite[] frames = sample.Frames;
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        return sample.Animated
            ? AnimationHelper.getSpriteFromList(
                sample.RenderSeed,
                frames,
                sample.AnimationSpeed)
            : frames[0];
    }

    private static void ResolveProjectilePresentation(
        ActorPresentationSnapshot snapshot,
        in ProjectilePresentationSample sample,
        out Vector3 position,
        out Vector3 shadowPosition,
        out Quaternion rotation,
        out float scale,
        out float alpha)
    {
        float prediction = GetProjectilePredictionSeconds(snapshot);
        scale = Mathf.MoveTowards(
            sample.Scale,
            sample.TargetScale,
            0.2f * prediction);
        alpha = sample.DeadAnimation
            ? Mathf.Max(0f, sample.Alpha - 0.5f * prediction)
            : sample.Alpha;
        if (sample.DeadAnimation || prediction <= 0f)
        {
            position = sample.Position;
            shadowPosition = sample.ShadowPosition;
            rotation = sample.Rotation;
            return;
        }

        Vector3 velocity = sample.Velocity;
        float gravity = Math.Max(0.0001f, SimGlobals.m.gravity);
        float motionTime = prediction;
        float discriminant =
            velocity.z * velocity.z +
            2f * gravity * Math.Max(0f, sample.Height);
        float groundTime =
            (velocity.z + Mathf.Sqrt(discriminant)) / gravity;
        if (groundTime >= 0f)
        {
            motionTime = Math.Min(motionTime, groundTime);
        }

        shadowPosition = sample.ShadowPosition;
        shadowPosition.x += velocity.x * motionTime;
        shadowPosition.y += velocity.y * motionTime;
        float height = Math.Max(
            0f,
            sample.Height +
            velocity.z * motionTime -
            0.5f * gravity * motionTime * motionTime);
        position = shadowPosition;
        position.y += height;
        position.z = height;
        float verticalVelocity =
            velocity.z - gravity * motionTime;
        float angle =
            Mathf.Atan2(
                velocity.y + verticalVelocity,
                velocity.x) *
            Mathf.Rad2Deg;
        rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private static float GetProjectilePredictionSeconds(
        ActorPresentationSnapshot snapshot)
    {
        if (!PerformanceSettings.EnablePresentationSmoothing ||
            World.world.isPaused())
        {
            return 0f;
        }

        float realAge = (float)Math.Max(
            0.0,
            (Stopwatch.GetTimestamp() - snapshot.CapturedAt) /
            (double)Stopwatch.Frequency);
        float speed = WorldTimeRateTracker.HasActualSpeed
            ? Math.Max(0f, WorldTimeRateTracker.ActualSpeed)
            : Math.Max(0f, WorldTimeRateTracker.GetRequestedSpeed());
        return Mathf.Clamp(realAge * speed, 0f, 0.25f);
    }

    private static float ResolveThrowRatio(
        in ResourceThrowPresentationSample sample,
        double now)
    {
        double duration = sample.EndTime - sample.StartTime;
        if (duration <= double.Epsilon)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            (float)((now - sample.StartTime) / duration));
    }

    private static void RecordPrepareDuration(long elapsedTicks)
    {
        preparedFrames++;
        totalPrepareTicks += elapsedTicks;
        if (elapsedTicks > maximumPrepareTicks)
        {
            maximumPrepareTicks = elapsedTicks;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
