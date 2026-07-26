using System;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>按世界时间推进量测量玩家实际感受到的模拟速度。</summary>
internal static class WorldTimeRateTracker
{
    private const float SampleWindowSeconds = 0.75f;
    private const float SampleSmoothing = 0.35f;

    private static MapStats sampledMapStats;
    private static int sampledWorldSeedId = -1;
    private static double previousWorldTime;
    private static double accumulatedWorldTime;
    private static float accumulatedRealTime;
    private static float sampledRequestedSpeed = -1f;
    private static bool sampledPaused;

    internal static float ActualSpeed { get; private set; }
    internal static bool HasActualSpeed { get; private set; }

    internal static void Update(MapBox world)
    {
        MapStats mapStats = world?.map_stats;
        if (!Config.game_loaded || mapStats == null)
        {
            Reset();
            return;
        }

        int worldSeedId = MapBox.current_world_seed_id;
        double worldTime = mapStats.world_time;
        bool paused = world.isPaused();
        float requestedSpeed = GetRequestedSpeed();
        if (!ReferenceEquals(sampledMapStats, mapStats) ||
            sampledWorldSeedId != worldSeedId ||
            worldTime < previousWorldTime ||
            Math.Abs(requestedSpeed - sampledRequestedSpeed) > 0.001f ||
            paused != sampledPaused)
        {
            BeginMeasurement(mapStats, worldSeedId, worldTime, requestedSpeed, paused);
            return;
        }

        double worldDelta = Math.Max(0.0, worldTime - previousWorldTime);
        previousWorldTime = worldTime;
        if (paused)
        {
            return;
        }

        accumulatedWorldTime += worldDelta;
        accumulatedRealTime += Math.Max(0f, Time.unscaledDeltaTime);
        if (accumulatedRealTime < SampleWindowSeconds)
        {
            return;
        }

        float sampledSpeed = (float)(accumulatedWorldTime / accumulatedRealTime);
        ActualSpeed = HasActualSpeed
            ? Mathf.Lerp(ActualSpeed, sampledSpeed, SampleSmoothing)
            : sampledSpeed;
        HasActualSpeed = true;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
    }

    internal static float GetRequestedSpeed()
    {
        WorldTimeScaleAsset timeScale = Config.time_scale_asset;
        if (timeScale == null)
        {
            return 0f;
        }

        return Math.Max(0f, timeScale.multiplier) * Math.Max(1, timeScale.ticks);
    }

    private static void BeginMeasurement(
        MapStats mapStats,
        int worldSeedId,
        double worldTime,
        float requestedSpeed,
        bool paused)
    {
        sampledMapStats = mapStats;
        sampledWorldSeedId = worldSeedId;
        previousWorldTime = worldTime;
        sampledRequestedSpeed = requestedSpeed;
        sampledPaused = paused;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
        ActualSpeed = 0f;
        HasActualSpeed = false;
    }

    private static void Reset()
    {
        sampledMapStats = null;
        sampledWorldSeedId = -1;
        previousWorldTime = 0.0;
        accumulatedWorldTime = 0.0;
        accumulatedRealTime = 0f;
        sampledRequestedSpeed = -1f;
        sampledPaused = false;
        ActualSpeed = 0f;
        HasActualSpeed = false;
    }
}
