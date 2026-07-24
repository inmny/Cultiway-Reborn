using System;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 为跨帧模拟阶段恢复本轮时间环境。固定步模式会额外还原一倍速配置，
/// 原版大步模式则保留当前速度资源，让子系统获得与原版一致的倍率上下文。
/// </summary>
internal static class SimulationStepContext
{
    public static void Run(
        MapBox map,
        bool paused,
        float simulationElapsed,
        bool normalizeTimeScale,
        WorldTimeScaleAsset simulationTimeScale,
        Action action)
    {
        WorldTimeScaleAsset previousTimeScaleAsset = Config.time_scale_asset;
        WorldTimeScaleAsset timeScale = normalizeTimeScale
            ? previousTimeScaleAsset
            : simulationTimeScale;
        float previousElapsed = map.elapsed;
        float previousDeltaTime = map.delta_time;
        float previousFixedDeltaTime = map.fixed_delta_time;
        bool previousPaused = map._is_paused;
        float previousMultiplier = timeScale.multiplier;
        int previousTicks = timeScale.ticks;
        int previousConwayTicks = timeScale.conway_ticks;
        bool previousSonic = timeScale.sonic;

        Config.time_scale_asset = timeScale;
        map.elapsed = simulationElapsed;
        map.delta_time = PerformanceSettings.FixedSimulationStepSeconds;
        map.fixed_delta_time = PerformanceSettings.FixedSimulationStepSeconds;
        map._is_paused = paused;
        if (normalizeTimeScale)
        {
            timeScale.multiplier = 1f;
            timeScale.ticks = 1;
            timeScale.conway_ticks = 1;
            timeScale.sonic = false;
        }

        try
        {
            action();
        }
        finally
        {
            timeScale.multiplier = previousMultiplier;
            timeScale.ticks = previousTicks;
            timeScale.conway_ticks = previousConwayTicks;
            timeScale.sonic = previousSonic;
            Config.time_scale_asset = previousTimeScaleAsset;
            map.elapsed = previousElapsed;
            map.delta_time = previousDeltaTime;
            map.fixed_delta_time = previousFixedDeltaTime;
            map._is_paused = previousPaused;
        }
    }
}
