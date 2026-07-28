using System;
using UnityEngine;

namespace Cultiway.Const;

public static class PerformanceSettings
{
    public static bool EnableFramePriorityScheduler { get; private set; } = true;
    public static bool EnableVanillaLargeSimulationStep { get; private set; }
    public static float TargetRenderFps { get; private set; } = 60f;
    public static float MaxSimulationMillisecondsPerFrame { get; private set; } = 8f;
    public static bool EnablePresentationSmoothing { get; private set; } = true;
    public static bool EnableSchedulerDiagnostics { get; private set; }

    public const float RenderReserveMilliseconds = 2f;
    public const float MinimumSliceMilliseconds = 0.15f;
    public const float BackgroundJoinMilliseconds = 0.2f;
    public const float StarvationSliceMilliseconds = 2f;
    // 即使渲染本身已经超过目标帧时长，也必须每帧推进一个安全切片；
    // 否则高负载世界会在零预算状态下永久停在同一逻辑阶段。
    public const int StarvationFrameInterval = 1;
    public const int SimulationBatchSize = 64;
    public const float FixedSimulationStepSeconds = 0.02f;
    public const float BaseSimulationTicksPerSecond = 1f / FixedSimulationStepSeconds;

    public static int TotalParallelBudget => Math.Max(1, Environment.ProcessorCount - 2);
    public static int PathfindingWorkerCount =>
        Math.Min(8, Math.Max(1, TotalParallelBudget / 2));
    public static int ForegroundParallelism =>
        Math.Max(
            1,
            TotalParallelBudget -
            Math.Min(3, PathfindingWorkerCount));

    internal static void ApplyParallelBudget(MapBox map)
    {
        if (map?.parallel_options != null)
        {
            map.parallel_options.MaxDegreeOfParallelism = ForegroundParallelism;
        }
    }

    public static void SwitchFramePriorityScheduler(bool value)
    {
        EnableFramePriorityScheduler = value;
    }

    public static void SwitchVanillaLargeSimulationStep(bool value)
    {
        EnableVanillaLargeSimulationStep = value;
    }

    public static void SetTargetRenderFps(float value)
    {
        TargetRenderFps = Mathf.Clamp(value, 3f, 144f);
    }

    public static void SetMaxSimulationMillisecondsPerFrame(float value)
    {
        MaxSimulationMillisecondsPerFrame = Mathf.Clamp(value, 0.5f, 1000f);
    }

    public static void SwitchPresentationSmoothing(bool value)
    {
        EnablePresentationSmoothing = value;
    }

    public static void SwitchSchedulerDiagnostics(bool value)
    {
        EnableSchedulerDiagnostics = value;
    }
}
