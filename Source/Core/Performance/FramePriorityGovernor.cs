using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal enum SimulationDomain
{
    Vanilla,
    Cultiway
}

internal static class FramePriorityGovernor
{
    private const int BaselineWindowSize = 120;
    private const double BootstrapBaselineMilliseconds = 6.0;

    private static readonly double[] BaselineSamples = new double[BaselineWindowSize];
    private static readonly Dictionary<string, double> PhaseEstimates = new(StringComparer.Ordinal);

    private static int frameId = -1;
    private static long frameStartedAt;
    private static int baselineSampleCount;
    private static int baselineSampleCursor;
    private static double baselineP90 = BootstrapBaselineMilliseconds;
    private static double hostCpuMilliseconds;
    private static double simulationCpuMilliseconds;
    private static double vanillaCpuMilliseconds;
    private static double cultiwayCpuMilliseconds;
    private static double lastFrameDeltaMilliseconds;
    private static double frameBudgetMilliseconds;
    private static int lastVanillaRunFrame = -PerformanceSettings.StarvationFrameInterval;
    private static int lastCultiwayRunFrame = -PerformanceSettings.StarvationFrameInterval;
    private static string longestPhase = string.Empty;
    private static double longestPhaseMilliseconds;
    private static string currentVanillaPhase = "idle";
    private static string currentCultiwayPhase = "idle";
    private static bool criticalHookInstalled;
    private static bool faulted;
    private static string faultMessage = string.Empty;
    private static int simulationPhaseDepth;

    public static long VanillaCyclesStarted { get; private set; }
    public static long VanillaCyclesCompleted { get; private set; }
    public static long CultiwayCyclesStarted { get; private set; }
    public static long CultiwayCyclesCompleted { get; private set; }

    public static bool CriticalHookInstalled => criticalHookInstalled;
    public static bool Faulted => faulted;
    public static string FaultMessage => faultMessage;
    public static bool IsExecutingSimulationPhase => simulationPhaseDepth > 0;

    public static void Initialize()
    {
        JobConst.MAX_ELEMENTS = PerformanceSettings.SimulationBatchSize;
        BeginFrame();
    }

    public static void BeginFrame()
    {
        int currentFrame = Time.frameCount;
        if (frameId == currentFrame)
        {
            return;
        }

        if (frameId >= 0)
        {
            FinalizePreviousFrame();
        }

        frameId = currentFrame;
        frameStartedAt = Stopwatch.GetTimestamp();
        hostCpuMilliseconds = 0.0;
        simulationCpuMilliseconds = 0.0;
        vanillaCpuMilliseconds = 0.0;
        cultiwayCpuMilliseconds = 0.0;
        RecalculateBudget();
    }

    public static long StartHostMeasurement()
    {
        BeginFrame();
        return Stopwatch.GetTimestamp();
    }

    public static void EndHostMeasurement(long startedAt)
    {
        hostCpuMilliseconds += ElapsedMilliseconds(startedAt);
    }

    public static bool CanRun(SimulationDomain domain, string phase)
    {
        BeginFrame();
        if (faulted)
        {
            return false;
        }

        double estimate = GetPhaseEstimate(phase);
        double currentFrameElapsed = ElapsedMilliseconds(frameStartedAt);
        double targetMilliseconds = 1000.0 / PerformanceSettings.TargetRenderFps;
        if (currentFrameElapsed >= targetMilliseconds - PerformanceSettings.RenderReserveMilliseconds)
        {
            return CanUseStarvationSlice(domain);
        }

        double remaining = frameBudgetMilliseconds - simulationCpuMilliseconds;
        if (remaining >= Math.Max(PerformanceSettings.MinimumSliceMilliseconds, estimate))
        {
            return true;
        }

        return CanUseStarvationSlice(domain);
    }

    public static void RunPhase(SimulationDomain domain, string phase, Action action)
    {
        long startedAt = Stopwatch.GetTimestamp();
        simulationPhaseDepth++;
        double elapsed;
        try
        {
            action();
        }
        finally
        {
            simulationPhaseDepth--;
            elapsed = ElapsedMilliseconds(startedAt);
        }

        simulationCpuMilliseconds += elapsed;
        if (domain == SimulationDomain.Vanilla)
        {
            vanillaCpuMilliseconds += elapsed;
            lastVanillaRunFrame = frameId;
            currentVanillaPhase = phase;
        }
        else
        {
            cultiwayCpuMilliseconds += elapsed;
            lastCultiwayRunFrame = frameId;
            currentCultiwayPhase = phase;
        }

        if (elapsed > longestPhaseMilliseconds)
        {
            longestPhaseMilliseconds = elapsed;
            longestPhase = phase;
        }

        if (PhaseEstimates.TryGetValue(phase, out double previous))
        {
            PhaseEstimates[phase] = previous * 0.8 + elapsed * 0.2;
        }
        else
        {
            PhaseEstimates[phase] = Math.Max(PerformanceSettings.MinimumSliceMilliseconds, elapsed);
        }
    }

    public static void SetPhase(SimulationDomain domain, string phase)
    {
        if (domain == SimulationDomain.Vanilla)
        {
            currentVanillaPhase = phase;
        }
        else
        {
            currentCultiwayPhase = phase;
        }
    }

    public static void MarkCriticalHookInstalled()
    {
        criticalHookInstalled = true;
    }

    public static void MarkFault(Exception exception)
    {
        faulted = true;
        faultMessage = exception?.GetType().Name + ": " + exception?.Message;
    }

    public static void ResetFault()
    {
        faulted = false;
        faultMessage = string.Empty;
    }

    public static void RecordVanillaCycleStarted()
    {
        VanillaCyclesStarted++;
    }

    public static void RecordVanillaCycleCompleted()
    {
        VanillaCyclesCompleted++;
    }

    public static void RecordCultiwayCycleStarted()
    {
        CultiwayCyclesStarted++;
    }

    public static void RecordCultiwayCycleCompleted()
    {
        CultiwayCyclesCompleted++;
    }

    public static string GetDiagnostics()
    {
        BeginFrame();
        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        return string.Format(
            CultureInfo.InvariantCulture,
            "target={0:0.#}fps budget={1:0.00}ms baselineP90={2:0.00}ms sim={3:0.00}ms(vanilla={4:0.00},cultiway={5:0.00}) phase={6}/{7} cycles={8}/{9} speed={10:0.#}x/{11:0.00}x credits={12:0.0} ticks={13}/{14} longest={15}:{16:0.00}ms workers={17}/{18}/{19}(total/fg/path) world={20}:{21}@{22:0.00} mode={23}",
            PerformanceSettings.TargetRenderFps,
            frameBudgetMilliseconds,
            baselineP90,
            simulationCpuMilliseconds,
            vanillaCpuMilliseconds,
            cultiwayCpuMilliseconds,
            currentVanillaPhase,
            currentCultiwayPhase,
            VanillaCyclesCompleted,
            CultiwayCyclesCompleted,
            runner.RequestedSpeed,
            runner.ActualSpeed,
            runner.AdmissionCredits,
            runner.LogicalTicksAdmitted,
            runner.LogicalTicksCompleted,
            longestPhase,
            longestPhaseMilliseconds,
            PerformanceSettings.TotalParallelBudget,
            PerformanceSettings.ForegroundParallelism,
            PerformanceSettings.PathfindingWorkerCount,
            SimulationTime.BoundWorldSeedId,
            SimulationTime.Generation,
            SimulationTime.DiagnosticTime,
            runner.AdmissionMode);
    }

    private static void FinalizePreviousFrame()
    {
        double baseline = Math.Max(0.0, hostCpuMilliseconds - simulationCpuMilliseconds);
        if (baseline > 0.0 && baseline < 1000.0)
        {
            BaselineSamples[baselineSampleCursor] = baseline;
            baselineSampleCursor = (baselineSampleCursor + 1) % BaselineWindowSize;
            baselineSampleCount = Math.Min(BaselineWindowSize, baselineSampleCount + 1);
            baselineP90 = CalculatePercentile90();
        }

        lastFrameDeltaMilliseconds = Time.unscaledDeltaTime * 1000.0;
    }

    private static void RecalculateBudget()
    {
        double targetMilliseconds = 1000.0 / PerformanceSettings.TargetRenderFps;
        double rawBudget = targetMilliseconds - PerformanceSettings.RenderReserveMilliseconds - baselineP90;
        double previousFrameOverrun = Math.Max(0.0, lastFrameDeltaMilliseconds - targetMilliseconds - 1.0);
        rawBudget -= previousFrameOverrun;
        frameBudgetMilliseconds = Math.Max(
            0.0,
            Math.Min(PerformanceSettings.MaxSimulationMillisecondsPerFrame, rawBudget));
    }

    private static bool CanUseStarvationSlice(SimulationDomain domain)
    {
        int lastRunFrame = domain == SimulationDomain.Vanilla ? lastVanillaRunFrame : lastCultiwayRunFrame;
        if (frameId - lastRunFrame < PerformanceSettings.StarvationFrameInterval)
        {
            return false;
        }

        double domainSpent = domain == SimulationDomain.Vanilla
            ? vanillaCpuMilliseconds
            : cultiwayCpuMilliseconds;
        return domainSpent < PerformanceSettings.StarvationSliceMilliseconds &&
               simulationCpuMilliseconds < frameBudgetMilliseconds + PerformanceSettings.StarvationSliceMilliseconds;
    }

    private static double GetPhaseEstimate(string phase)
    {
        return PhaseEstimates.TryGetValue(phase, out double estimate)
            ? estimate
            : PerformanceSettings.MinimumSliceMilliseconds;
    }

    private static double CalculatePercentile90()
    {
        var samples = new double[baselineSampleCount];
        Array.Copy(BaselineSamples, samples, baselineSampleCount);
        Array.Sort(samples);
        int index = Math.Max(0, (int)Math.Ceiling(samples.Length * 0.9) - 1);
        return samples[index];
    }

    private static double ElapsedMilliseconds(long startedAt)
    {
        return (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
    }
}
