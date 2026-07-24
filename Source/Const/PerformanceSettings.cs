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
    public const float StarvationSliceMilliseconds = 0.5f;
    public const int StarvationFrameInterval = 8;
    public const int SimulationBatchSize = 64;
    public const float FixedSimulationStepSeconds = 0.02f;
    public const float BaseSimulationTicksPerSecond = 1f / FixedSimulationStepSeconds;

    public static int TotalParallelBudget => Math.Max(1, Environment.ProcessorCount - 2);
    public static int PathfindingWorkerCount =>
        Math.Min(4, Math.Max(1, TotalParallelBudget / 4));
    public static int ForegroundParallelism =>
        Math.Max(1, TotalParallelBudget - PathfindingWorkerCount);

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
        TargetRenderFps = Mathf.Clamp(value, 30f, 144f);
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
