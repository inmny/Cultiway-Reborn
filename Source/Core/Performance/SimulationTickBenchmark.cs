using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal static class SimulationTickBenchmark
{
    internal const string TotalsGroupId = "cultiway_tick_totals";
    internal const string PhasesGroupId = "cultiway_tick_phases";
    internal const string ActorsGroupId = "cultiway_tick_actors";
    internal const string BuildingsGroupId = "cultiway_tick_buildings";
    internal const string WorldBehavioursGroupId = "cultiway_tick_world_behaviours";
    internal const string CultiwaySystemsGroupId = "cultiway_tick_systems";

    internal const string TickTotalId = "tick_total";
    internal const string ActorsTotalId = "actors_total";
    internal const string BuildingsTotalId = "buildings_total";
    internal const string WorldBehavioursTotalId = "world_behaviours_total";
    internal const string CultiwayTotalId = "cultiway_total";

    private const int HistoryCapacity = 64;
    private const string BenchmarkAllId = "Benchmark All";
    private const string TickToolId = "Benchmark Cultiway Tick";
    private const string ActorsToolId = "Benchmark Cultiway Tick Actors";
    private const string BuildingsToolId = "Benchmark Cultiway Tick Buildings";
    private const string WorldBehavioursToolId = "Benchmark Cultiway Tick World Beh";
    private const string CultiwaySystemsToolId = "Benchmark Cultiway Tick Systems";

    private static readonly Queue<TickSnapshot> History = new(HistoryCapacity);
    private static readonly Stack<TickCapture> CapturePool = new(2);
    private static readonly List<TickCapture> PendingCompleted = new(2);

    private static readonly BenchmarkGroupState TotalsGroup = new(TotalsGroupId);
    private static readonly BenchmarkGroupState PhasesGroup = new(PhasesGroupId);
    private static readonly BenchmarkGroupState ActorsGroup = new(ActorsGroupId);
    private static readonly BenchmarkGroupState BuildingsGroup = new(BuildingsGroupId);
    private static readonly BenchmarkGroupState WorldBehavioursGroup =
        new(WorldBehavioursGroupId);
    private static readonly BenchmarkGroupState CultiwaySystemsGroup =
        new(CultiwaySystemsGroupId);

    private static TickCapture current;
    private static int suspendDepth;
    private static bool benchStateInitialized;
    private static bool lastBenchEnabled;
    private static bool debugToolsRegistered;

    internal static bool IsCapturing => current != null && !current.Cancelled;

    internal static void Initialize()
    {
        SyncCaptureState();
        RegisterDebugTools();
    }

    internal static void SyncCaptureState()
    {
        bool enabled = Bench.bench_enabled;
        if (!benchStateInitialized)
        {
            benchStateInitialized = true;
            lastBenchEnabled = enabled;
            if (enabled)
            {
                ResetSession();
            }

            return;
        }

        if (enabled == lastBenchEnabled)
        {
            return;
        }

        lastBenchEnabled = enabled;
        DiscardCaptures();
        if (enabled)
        {
            ResetSession();
        }
    }

    internal static void BeginTick(float simulatedSeconds, bool largeStep)
    {
        if (!Bench.bench_enabled || suspendDepth > 0)
        {
            return;
        }

        if (current != null)
        {
            current.Cancelled = true;
            ReturnCapture(current);
        }

        current = RentCapture();
        current.SimulatedSeconds = Math.Max(0f, simulatedSeconds);
        current.StartFrame = Time.frameCount;
        current.StartedAt = Time.realtimeSinceStartupAsDouble;
        current.Mode = largeStep ? "large" : "fixed";
    }

    internal static void MarkTickCompleted()
    {
        if (current == null)
        {
            return;
        }

        current.EndFrame = Time.frameCount;
        current.CompletedAt = Time.realtimeSinceStartupAsDouble;
        if (!Bench.bench_enabled)
        {
            current.Cancelled = true;
        }

        PendingCompleted.Add(current);
        current = null;
    }

    internal static TickCapture CapturePhaseTarget()
    {
        return current;
    }

    internal static void RecordPhase(
        TickCapture target,
        SimulationDomain domain,
        string phase,
        double elapsedMilliseconds)
    {
        if (!Bench.bench_enabled)
        {
            if (target != null)
            {
                target.Cancelled = true;
            }

            if (current != null)
            {
                current.Cancelled = true;
            }

            return;
        }

        target ??= current;
        if (target == null || target.Cancelled)
        {
            return;
        }

        double seconds = Math.Max(0.0, elapsedMilliseconds) / 1000.0;
        target.TotalSeconds += seconds;
        target.MaxSliceSeconds = Math.Max(target.MaxSliceSeconds, seconds);
        if (domain == SimulationDomain.Vanilla)
        {
            target.VanillaSeconds += seconds;
        }
        else
        {
            target.CultiwaySeconds += seconds;
        }

        AddMetric(target.Phases, NormalizePhase(phase), seconds, 1);
        RecordSpecializedPhase(target, domain, phase, seconds);
    }

    internal static void FlushCompleted()
    {
        if (PendingCompleted.Count == 0)
        {
            return;
        }

        for (int i = 0; i < PendingCompleted.Count; i++)
        {
            TickCapture capture = PendingCompleted[i];
            if (!capture.Cancelled && Bench.bench_enabled && suspendDepth == 0)
            {
                Commit(capture);
            }

            ReturnCapture(capture);
        }

        PendingCompleted.Clear();
    }

    internal static void RecordBatchJobs<TBatch, TObject>(
        string benchmarkId,
        List<TBatch> batches)
        where TBatch : Batch<TObject>, new()
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        Dictionary<string, Metric> target = benchmarkId switch
        {
            "actors" => capture.ActorJobs,
            "buildings" => capture.BuildingJobs,
            _ => null
        };
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < batches.Count; i++)
        {
            TBatch batch = batches[i];
            RecordJobList(target, batch.jobs_pre);
            RecordJobList(target, batch.jobs_post);
        }
    }

    internal static void AbortCurrentTick()
    {
        DiscardCaptures();
    }

    internal static void Suspend()
    {
        suspendDepth++;
        DiscardCaptures();
    }

    internal static void Resume()
    {
        if (suspendDepth <= 0)
        {
            throw new InvalidOperationException("Tick Benchmark 未处于挂起状态");
        }

        suspendDepth--;
    }

    internal static bool AppendReport(
        StringBuilder sb,
        int phaseLimit = 8,
        int detailLimit = 6)
    {
        TickWindowStats stats = GetWindowStats();
        if (stats.Count == 0)
        {
            return false;
        }

        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        sb.AppendLine()
            .Append("  [SimulationTickBenchmark]")
            .Append(" samples=").Append(stats.Count)
            .Append(" mode=").Append(stats.LastMode)
            .Append(" tick=").Append(FormatMilliseconds(stats.AverageWorkSeconds))
            .Append(" max=").Append(FormatMilliseconds(stats.MaximumWorkSeconds))
            .Append(" sliceMax=").Append(FormatMilliseconds(stats.MaximumSliceSeconds))
            .Append(" delta=").Append(stats.AverageSimulatedSeconds.ToString("0.000", CultureInfo.InvariantCulture))
            .Append('s')
            .Append(" frames=").Append(stats.AverageFrames.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" latency=").Append(FormatMilliseconds(stats.AverageLatencySeconds))
            .Append(" theoretical=")
            .Append(stats.TheoreticalTicksPerSecond.ToString("0.00", CultureInfo.InvariantCulture))
            .Append("tps/")
            .Append(stats.TheoreticalSpeed.ToString("0.00", CultureInfo.InvariantCulture))
            .Append('x')
            .Append(" actual=").Append(runner.ActualSpeed.ToString("0.00", CultureInfo.InvariantCulture))
            .Append('x')
            .AppendLine();

        AppendTopRows(sb, "phases", PhasesGroupId, TickTotalId, phaseLimit);
        AppendTopRows(sb, "actors", ActorsGroupId, ActorsTotalId, detailLimit);
        AppendTopRows(sb, "buildings", BuildingsGroupId, BuildingsTotalId, detailLimit);
        AppendTopRows(
            sb,
            "world_beh",
            WorldBehavioursGroupId,
            WorldBehavioursTotalId,
            detailLimit);
        AppendTopRows(sb, "cultiway", CultiwaySystemsGroupId, CultiwayTotalId, detailLimit);
        return true;
    }

    private static void Commit(TickCapture capture)
    {
        AddUnattributedOverhead(capture.ActorJobs, capture.ActorsSeconds);
        AddUnattributedOverhead(capture.BuildingJobs, capture.BuildingsSeconds);

        capture.SetTotal(TickTotalId, capture.TotalSeconds);
        capture.SetTotal("vanilla_total", capture.VanillaSeconds);
        capture.SetTotal(CultiwayTotalId, capture.CultiwaySeconds);
        capture.SetTotal(ActorsTotalId, capture.ActorsSeconds);
        capture.SetTotal(BuildingsTotalId, capture.BuildingsSeconds);
        capture.SetTotal(WorldBehavioursTotalId, capture.WorldBehavioursSeconds);

        int previousSamples = History.Count;
        PublishGroup(TotalsGroup, capture.Totals, previousSamples);
        PublishGroup(PhasesGroup, capture.Phases, previousSamples);
        PublishGroup(ActorsGroup, capture.ActorJobs, previousSamples);
        PublishGroup(BuildingsGroup, capture.BuildingJobs, previousSamples);
        PublishGroup(WorldBehavioursGroup, capture.WorldBehaviours, previousSamples);
        PublishGroup(CultiwaySystemsGroup, capture.CultiwaySystems, previousSamples);

        if (History.Count >= HistoryCapacity)
        {
            History.Dequeue();
        }

        History.Enqueue(new TickSnapshot(
            capture.TotalSeconds,
            capture.MaxSliceSeconds,
            capture.SimulatedSeconds,
            Math.Max(1, capture.EndFrame - capture.StartFrame + 1),
            Math.Max(0.0, capture.CompletedAt - capture.StartedAt),
            capture.Mode));
    }

    private static void RecordSpecializedPhase(
        TickCapture capture,
        SimulationDomain domain,
        string phase,
        double seconds)
    {
        if (phase.StartsWith("vanilla.actors", StringComparison.Ordinal))
        {
            capture.ActorsSeconds += seconds;
            RecordBatchStage(capture.ActorJobs, phase, seconds);
        }
        else if (phase.StartsWith("vanilla.buildings", StringComparison.Ordinal))
        {
            capture.BuildingsSeconds += seconds;
            RecordBatchStage(capture.BuildingJobs, phase, seconds);
        }

        const string worldBehaviourPrefix = "vanilla.world_behaviour.";
        if (phase.StartsWith(worldBehaviourPrefix, StringComparison.Ordinal))
        {
            capture.WorldBehavioursSeconds += seconds;
            AddMetric(
                capture.WorldBehaviours,
                phase.Substring(worldBehaviourPrefix.Length),
                seconds,
                1);
        }

        if (domain == SimulationDomain.Cultiway)
        {
            string id = phase.StartsWith("cultiway.", StringComparison.Ordinal)
                ? phase.Substring("cultiway.".Length)
                : phase;
            AddMetric(capture.CultiwaySystems, id, seconds, 1);
        }
    }

    private static void RecordBatchStage(
        Dictionary<string, Metric> target,
        string phase,
        double seconds)
    {
        string id = null;
        if (phase.Contains(".parallel.", StringComparison.Ordinal))
        {
            id = "update_jobs_parallel";
        }
        else if (phase.EndsWith(".clearparallelresults", StringComparison.Ordinal))
        {
            id = "clear_parallel_results";
        }
        else if (phase.EndsWith(".applyparallelresults", StringComparison.Ordinal))
        {
            id = "apply_parallel_results";
        }

        if (id != null)
        {
            AddMetric(target, id, seconds, 1);
        }
    }

    private static void RecordJobList<TObject>(
        Dictionary<string, Metric> target,
        List<Job<TObject>> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            Job<TObject> job = jobs[i];
            AddMetric(target, job.id, Math.Max(0.0, job.time_benchmark), job.counter);
        }
    }

    private static void AddUnattributedOverhead(
        Dictionary<string, Metric> entries,
        double totalSeconds)
    {
        double detailedSeconds = 0.0;
        foreach (Metric metric in entries.Values)
        {
            detailedSeconds += metric.Seconds;
        }

        double overhead = totalSeconds - detailedSeconds;
        if (overhead > 0.0000001)
        {
            AddMetric(entries, "unattributed_overhead", overhead, 1);
        }
    }

    private static string NormalizePhase(string phase)
    {
        int index = phase.IndexOf(".batch_group.", StringComparison.Ordinal);
        if (index >= 0)
        {
            return phase.Substring(0, index);
        }

        index = phase.IndexOf(".batch.", StringComparison.Ordinal);
        return index >= 0 ? phase.Substring(0, index) : phase;
    }

    private static void AddMetric(
        Dictionary<string, Metric> entries,
        string id,
        double seconds,
        long counter)
    {
        if (!entries.TryGetValue(id, out Metric metric))
        {
            metric = new Metric();
            entries.Add(id, metric);
        }

        metric.Seconds += seconds;
        metric.Counter += counter;
    }

    private static void PublishGroup(
        BenchmarkGroupState state,
        Dictionary<string, Metric> entries,
        int previousSamples)
    {
        foreach (string id in entries.Keys)
        {
            if (state.KnownEntries.Add(id))
            {
                SeedMissingSamples(state.GroupId, id, previousSamples);
            }
        }

        foreach (string id in state.KnownEntries)
        {
            entries.TryGetValue(id, out Metric metric);
            double seconds = metric?.Seconds ?? 0.0;
            int counter = ClampCounter(metric?.Counter ?? 0L);
            Bench.benchSave(id, seconds, counter, state.GroupId);
            Bench.saveAverageCounter(id, state.GroupId);
        }
    }

    private static void SeedMissingSamples(string groupId, string id, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Bench.benchSave(id, 0.0, 0, groupId);
            Bench.saveAverageCounter(id, groupId);
        }
    }

    private static int ClampCounter(long value)
    {
        return value <= 0L
            ? 0
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }

    private static TickCapture RentCapture()
    {
        TickCapture capture = CapturePool.Count > 0 ? CapturePool.Pop() : new TickCapture();
        capture.Reset();
        return capture;
    }

    private static void ReturnCapture(TickCapture capture)
    {
        capture.Reset();
        CapturePool.Push(capture);
    }

    private static void DiscardCaptures()
    {
        if (current != null)
        {
            ReturnCapture(current);
            current = null;
        }

        for (int i = 0; i < PendingCompleted.Count; i++)
        {
            ReturnCapture(PendingCompleted[i]);
        }

        PendingCompleted.Clear();
    }

    private static void ResetSession()
    {
        DiscardCaptures();
        History.Clear();
        ResetGroup(TotalsGroup);
        ResetGroup(PhasesGroup);
        ResetGroup(ActorsGroup);
        ResetGroup(BuildingsGroup);
        ResetGroup(WorldBehavioursGroup);
        ResetGroup(CultiwaySystemsGroup);
    }

    private static void ResetGroup(BenchmarkGroupState state)
    {
        state.KnownEntries.Clear();
        Bench.getGroup(state.GroupId).dict_data.Clear();
    }

    private static TickWindowStats GetWindowStats()
    {
        if (History.Count == 0)
        {
            return default;
        }

        double totalWork = 0.0;
        double maximumWork = 0.0;
        double maximumSlice = 0.0;
        double totalSimulated = 0.0;
        double totalFrames = 0.0;
        double totalLatency = 0.0;
        string lastMode = string.Empty;
        foreach (TickSnapshot snapshot in History)
        {
            totalWork += snapshot.WorkSeconds;
            maximumWork = Math.Max(maximumWork, snapshot.WorkSeconds);
            maximumSlice = Math.Max(maximumSlice, snapshot.MaxSliceSeconds);
            totalSimulated += snapshot.SimulatedSeconds;
            totalFrames += snapshot.Frames;
            totalLatency += snapshot.LatencySeconds;
            lastMode = snapshot.Mode;
        }

        double count = History.Count;
        return new TickWindowStats(
            History.Count,
            totalWork / count,
            maximumWork,
            maximumSlice,
            totalSimulated / count,
            totalFrames / count,
            totalLatency / count,
            lastMode);
    }

    private static void RegisterDebugTools()
    {
        if (debugToolsRegistered)
        {
            return;
        }

        DebugToolLibrary library = AssetManager.debug_tool_library;
        DebugToolAsset template = library.get(BenchmarkAllId);
        if (template?.action_2 == null)
        {
            throw new InvalidOperationException("原版 Benchmark 调试工具尚未初始化");
        }

        RegisterDebugTool(library, template, TickToolId, PhasesGroupId, TickTotalId);
        RegisterDebugTool(library, template, ActorsToolId, ActorsGroupId, ActorsTotalId);
        RegisterDebugTool(library, template, BuildingsToolId, BuildingsGroupId, BuildingsTotalId);
        RegisterDebugTool(
            library,
            template,
            WorldBehavioursToolId,
            WorldBehavioursGroupId,
            WorldBehavioursTotalId);
        RegisterDebugTool(
            library,
            template,
            CultiwaySystemsToolId,
            CultiwaySystemsGroupId,
            CultiwayTotalId);
        debugToolsRegistered = true;
    }

    private static void RegisterDebugTool(
        DebugToolLibrary library,
        DebugToolAsset template,
        string id,
        string groupId,
        string totalId)
    {
        if (library.has(id))
        {
            return;
        }

        library.add(new DebugToolAsset
        {
            id = id,
            name = id,
            type = DebugToolType.Benchmarks,
            priority = 2,
            benchmark_group_id = groupId,
            benchmark_total = totalId,
            benchmark_total_group = TotalsGroupId,
            split_benchmark = true,
            show_benchmark_buttons = true,
            update_timeout = 0.2f,
            action_start = ConfigureDebugTool,
            action_1 = ShowDebugHeader,
            action_2 = template.action_2
        });
    }

    private static void ConfigureDebugTool(DebugTool tool)
    {
        tool.sort_order_reversed = false;
        tool.sort_by_names = false;
        tool.sort_by_values = true;
        tool.show_averages = true;
        tool.hide_zeroes = true;
        tool.show_counter = true;
        tool.show_max = true;
        tool.state = DebugToolState.Percent;
        tool.paused = false;
        tool.percentage_slowest = false;
    }

    private static void ShowDebugHeader(DebugTool tool)
    {
        TickWindowStats stats = GetWindowStats();
        if (stats.Count == 0)
        {
            tool.setText("tick samples:", 0);
            tool.setSeparator();
            return;
        }

        double groupSeconds = GetAverage(tool.asset.benchmark_total, TotalsGroupId);
        double share = stats.AverageWorkSeconds > 0.0
            ? groupSeconds / stats.AverageWorkSeconds * 100.0
            : 0.0;
        tool.setText("tick samples:", stats.Count);
        tool.setText("tick work:", FormatMilliseconds(stats.AverageWorkSeconds));
        tool.setText("tick max:", FormatMilliseconds(stats.MaximumWorkSeconds));
        tool.setText("slice max:", FormatMilliseconds(stats.MaximumSliceSeconds));
        tool.setText(
            "simulated:",
            stats.AverageSimulatedSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s");
        tool.setText("frames/tick:", stats.AverageFrames.ToString("0.00", CultureInfo.InvariantCulture));
        tool.setText(
            "theoretical:",
            stats.TheoreticalTicksPerSecond.ToString("0.00", CultureInfo.InvariantCulture) +
            " TPS | " +
            stats.TheoreticalSpeed.ToString("0.00", CultureInfo.InvariantCulture) +
            "x");
        if (!tool.asset.benchmark_total.Equals(TickTotalId, StringComparison.Ordinal))
        {
            tool.setText("group work:", FormatMilliseconds(groupSeconds));
            tool.setText(
                "share of tick:",
                share.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                (float)share,
                true);
        }

        tool.setSeparator();
    }

    private static void AppendTopRows(
        StringBuilder sb,
        string label,
        string groupId,
        string totalId,
        int limit)
    {
        double total = GetAverage(totalId, TotalsGroupId);
        if (total <= 0.0)
        {
            return;
        }

        var rows = new List<BenchmarkRow>();
        foreach (ToolBenchmarkData data in Bench.getGroup(groupId).dict_data.Values)
        {
            double seconds = data.getAverage();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0.0000001)
            {
                continue;
            }

            rows.Add(new BenchmarkRow(data.id, seconds, data.getAverageCount()));
        }

        rows.Sort((left, right) => right.Seconds.CompareTo(left.Seconds));
        int count = Math.Min(limit, rows.Count);
        if (count == 0)
        {
            return;
        }

        sb.Append("    ").Append(label).Append(": ");
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            BenchmarkRow row = rows[i];
            sb.Append(row.Id)
                .Append('=').Append(FormatMilliseconds(row.Seconds))
                .Append('(')
                .Append((row.Seconds / total * 100.0).ToString("0.0", CultureInfo.InvariantCulture))
                .Append('%');
            if (row.Counter > 0)
            {
                sb.Append('/').Append(row.Counter);
            }

            sb.Append(')');
        }

        sb.AppendLine();
    }

    private static double GetAverage(string id, string groupId)
    {
        double value = Bench.getBenchResultAsDouble(id, groupId, true);
        return double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 ? 0.0 : value;
    }

    private static string FormatMilliseconds(double seconds)
    {
        return (seconds * 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + "ms";
    }

    internal sealed class TickCapture
    {
        internal readonly Dictionary<string, Metric> Totals = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> Phases = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> ActorJobs = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> BuildingJobs = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> WorldBehaviours = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> CultiwaySystems = new(StringComparer.Ordinal);

        internal float SimulatedSeconds;
        internal int StartFrame;
        internal int EndFrame;
        internal double StartedAt;
        internal double CompletedAt;
        internal double TotalSeconds;
        internal double MaxSliceSeconds;
        internal double VanillaSeconds;
        internal double CultiwaySeconds;
        internal double ActorsSeconds;
        internal double BuildingsSeconds;
        internal double WorldBehavioursSeconds;
        internal string Mode = string.Empty;
        internal bool Cancelled;

        internal void SetTotal(string id, double seconds)
        {
            if (!Totals.TryGetValue(id, out Metric metric))
            {
                metric = new Metric();
                Totals.Add(id, metric);
            }

            metric.Seconds = seconds;
            metric.Counter = 1L;
        }

        internal void Reset()
        {
            ResetMetrics(Totals);
            ResetMetrics(Phases);
            ResetMetrics(ActorJobs);
            ResetMetrics(BuildingJobs);
            ResetMetrics(WorldBehaviours);
            ResetMetrics(CultiwaySystems);
            SimulatedSeconds = 0f;
            StartFrame = 0;
            EndFrame = 0;
            StartedAt = 0.0;
            CompletedAt = 0.0;
            TotalSeconds = 0.0;
            MaxSliceSeconds = 0.0;
            VanillaSeconds = 0.0;
            CultiwaySeconds = 0.0;
            ActorsSeconds = 0.0;
            BuildingsSeconds = 0.0;
            WorldBehavioursSeconds = 0.0;
            Mode = string.Empty;
            Cancelled = false;
        }

        private static void ResetMetrics(Dictionary<string, Metric> entries)
        {
            foreach (Metric metric in entries.Values)
            {
                metric.Seconds = 0.0;
                metric.Counter = 0L;
            }
        }
    }

    internal sealed class Metric
    {
        internal double Seconds;
        internal long Counter;
    }

    private sealed class BenchmarkGroupState
    {
        internal BenchmarkGroupState(string groupId)
        {
            GroupId = groupId;
        }

        internal string GroupId { get; }
        internal HashSet<string> KnownEntries { get; } = new(StringComparer.Ordinal);
    }

    private readonly struct TickSnapshot
    {
        internal TickSnapshot(
            double workSeconds,
            double maxSliceSeconds,
            float simulatedSeconds,
            int frames,
            double latencySeconds,
            string mode)
        {
            WorkSeconds = workSeconds;
            MaxSliceSeconds = maxSliceSeconds;
            SimulatedSeconds = simulatedSeconds;
            Frames = frames;
            LatencySeconds = latencySeconds;
            Mode = mode;
        }

        internal double WorkSeconds { get; }
        internal double MaxSliceSeconds { get; }
        internal float SimulatedSeconds { get; }
        internal int Frames { get; }
        internal double LatencySeconds { get; }
        internal string Mode { get; }
    }

    private readonly struct TickWindowStats
    {
        internal TickWindowStats(
            int count,
            double averageWorkSeconds,
            double maximumWorkSeconds,
            double maximumSliceSeconds,
            double averageSimulatedSeconds,
            double averageFrames,
            double averageLatencySeconds,
            string lastMode)
        {
            Count = count;
            AverageWorkSeconds = averageWorkSeconds;
            MaximumWorkSeconds = maximumWorkSeconds;
            MaximumSliceSeconds = maximumSliceSeconds;
            AverageSimulatedSeconds = averageSimulatedSeconds;
            AverageFrames = averageFrames;
            AverageLatencySeconds = averageLatencySeconds;
            LastMode = lastMode;
        }

        internal int Count { get; }
        internal double AverageWorkSeconds { get; }
        internal double MaximumWorkSeconds { get; }
        internal double MaximumSliceSeconds { get; }
        internal double AverageSimulatedSeconds { get; }
        internal double AverageFrames { get; }
        internal double AverageLatencySeconds { get; }
        internal string LastMode { get; }
        internal double TheoreticalTicksPerSecond =>
            AverageWorkSeconds > 0.0 ? 1.0 / AverageWorkSeconds : 0.0;
        internal double TheoreticalSpeed =>
            AverageWorkSeconds > 0.0 ? AverageSimulatedSeconds / AverageWorkSeconds : 0.0;
    }

    private readonly struct BenchmarkRow
    {
        internal BenchmarkRow(string id, double seconds, long counter)
        {
            Id = id;
            Seconds = seconds;
            Counter = counter;
        }

        internal string Id { get; }
        internal double Seconds { get; }
        internal long Counter { get; }
    }
}
