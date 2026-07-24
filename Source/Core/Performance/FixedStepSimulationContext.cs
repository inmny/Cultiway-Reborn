using System;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 在单个模拟阶段内还原一倍速环境，避免子系统再次读取高倍速配置并重复放大。
/// </summary>
internal static class FixedStepSimulationContext
{
    public static void Run(MapBox map, bool paused, Action action)
    {
        WorldTimeScaleAsset timeScale = Config.time_scale_asset;
        float previousElapsed = map.elapsed;
        float previousDeltaTime = map.delta_time;
        float previousFixedDeltaTime = map.fixed_delta_time;
        bool previousPaused = map._is_paused;
        float previousMultiplier = timeScale.multiplier;
        int previousTicks = timeScale.ticks;
        int previousConwayTicks = timeScale.conway_ticks;
        bool previousSonic = timeScale.sonic;

        map.elapsed = PerformanceSettings.FixedSimulationStepSeconds;
        map.delta_time = PerformanceSettings.FixedSimulationStepSeconds;
        map.fixed_delta_time = PerformanceSettings.FixedSimulationStepSeconds;
        map._is_paused = paused;
        timeScale.multiplier = 1f;
        timeScale.ticks = 1;
        timeScale.conway_ticks = 1;
        timeScale.sonic = false;

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
            map.elapsed = previousElapsed;
            map.delta_time = previousDeltaTime;
            map.fixed_delta_time = previousFixedDeltaTime;
            map._is_paused = previousPaused;
        }
    }
}
