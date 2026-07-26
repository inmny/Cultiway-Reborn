using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal static class SimulationTickBenchmark
{
    internal const string TotalsGroupId = "cultiway_tick_totals";
    internal const string PhasesGroupId = "cultiway_tick_phases";
    internal const string ActorsGroupId = "cultiway_tick_actors";
    internal const string ActorPostJobsGroupId = "cultiway_tick_actor_post_jobs";
    internal const string ActorTileActionsGroupId = "cultiway_tick_actor_tile_actions";
    internal const string GoToGroupId = "cultiway_tick_goto";
    internal const string PathfindingGroupId = "cultiway_tick_pathfinding";
    internal const string DirtyManagersGroupId = "cultiway_tick_dirty_managers";
    internal const string BuildingsGroupId = "cultiway_tick_buildings";
    internal const string WorldBehavioursGroupId = "cultiway_tick_world_behaviours";
    internal const string CultiwaySystemsGroupId = "cultiway_tick_systems";

    internal const string TickTotalId = "tick_total";
    internal const string ActorsTotalId = "actors_total";
    internal const string ActorPostJobsTotalId = "actor_post_jobs_total";
    internal const string ActorTileActionsTotalId = "actor_tile_actions_total";
    internal const string GoToTotalId = "goto_total";
    internal const string PathfindingTotalId = "pathfinding_total";
    internal const string DirtyManagersTotalId = "dirty_managers_total";
    internal const string BuildingsTotalId = "buildings_total";
    internal const string WorldBehavioursTotalId = "world_behaviours_total";
    internal const string CultiwayTotalId = "cultiway_total";

    private const int HistoryCapacity = 64;
    private const string BenchmarkAllId = "Benchmark All";
    private const string TickToolId = "Benchmark Cultiway Tick";
    private const string ActorsToolId = "Benchmark Cultiway Tick Actors";
    private const string ActorTileActionsToolId = "Benchmark Cultiway Tile Actions";
    private const string PathfindingToolId = "Benchmark Cultiway Pathfinding";
    private const string BuildingsToolId = "Benchmark Cultiway Tick Buildings";
    private const string WorldBehavioursToolId = "Benchmark Cultiway Tick World Beh";
    private const string CultiwaySystemsToolId = "Benchmark Cultiway Tick Systems";

    private static readonly Queue<TickSnapshot> History = new(HistoryCapacity);
    private static readonly Stack<TickCapture> CapturePool = new(2);
    private static readonly List<TickCapture> PendingCompleted = new(2);

    private static readonly BenchmarkGroupState TotalsGroup = new(TotalsGroupId);
    private static readonly BenchmarkGroupState PhasesGroup = new(PhasesGroupId);
    private static readonly BenchmarkGroupState ActorsGroup = new(ActorsGroupId);
    private static readonly BenchmarkGroupState ActorPostJobsGroup =
        new(ActorPostJobsGroupId, true);
    private static readonly BenchmarkGroupState ActorTileActionsGroup =
        new(ActorTileActionsGroupId);
    private static readonly BenchmarkGroupState GoToGroup =
        new(GoToGroupId, true);
    private static readonly BenchmarkGroupState PathfindingGroup =
        new(PathfindingGroupId, true);
    private static readonly BenchmarkGroupState DirtyManagersGroup =
        new(DirtyManagersGroupId, true);
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
    internal static bool ShouldCollectAiDetails =>
        Bench.bench_enabled && SystemUtils.IsUnderDeveloper();

    internal static void Initialize()
    {
        SyncCaptureState();
        RegisterDebugTools();
    }

    internal static void SyncCaptureState()
    {
        ApplyAiDetailsPolicy();
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

    internal static void ApplyAiDetailsPolicy()
    {
        bool enabled = ShouldCollectAiDetails;
        if (DebugConfig.isOn(DebugOption.BenchAiEnabled) != enabled)
        {
            DebugConfig.setOption(
                DebugOption.BenchAiEnabled,
                enabled,
                pUpdateSpecialSettings: false);
        }

        Bench.bench_ai_enabled = enabled;
        PathfindingProfiler.SetEnabled(enabled);
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
        current.StartGen0Collections = GC.CollectionCount(0);
        current.StartGen1Collections = GC.CollectionCount(1);
        current.StartGen2Collections = GC.CollectionCount(2);
        current.Mode = largeStep ? "large" : "fixed";
        current.PathfindingStart = PathfindingProfiler.CaptureSnapshot();
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
            if (benchmarkId.Equals("actors", StringComparison.Ordinal))
            {
                RecordJobList(capture.ActorPostJobs, batch.jobs_post);
            }
        }
    }

    internal static void RecordActorJobMetric(
        string id,
        double seconds,
        long counter)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        AddMetric(
            capture.ActorJobs,
            id,
            Math.Max(0.0, seconds),
            counter);
        AddMetric(
            capture.ActorPostJobs,
            id,
            Math.Max(0.0, seconds),
            counter);
    }

    internal static void RecordDirtyManagerMetric(
        string id,
        double seconds)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        AddMetric(
            capture.DirtyManagers,
            id,
            Math.Max(0.0, seconds),
            1L);
    }

    internal static void RecordActorTileActionMetric(
        string id,
        double seconds,
        long counter)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        AddMetric(
            capture.ActorTileActions,
            id,
            Math.Max(0.0, seconds),
            counter);
    }

    internal static void RecordGoToActionMetric(string id, double seconds)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        seconds = Math.Max(0.0, seconds);
        AddMetric(capture.GoTo, id, seconds, 1L);
        capture.GoToActionSeconds += seconds;
    }

    internal static void RecordGoToDetailMetric(string id, double seconds)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || !Bench.bench_enabled)
        {
            return;
        }

        AddMetric(capture.GoTo, id, Math.Max(0.0, seconds), 1L);
    }

    internal static bool TryClaimGoToSpikeLog()
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || capture.GoToSpikeLogs >= 4)
        {
            return false;
        }

        capture.GoToSpikeLogs++;
        return true;
    }

    internal static void QueueGoToSpike(string message)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled || string.IsNullOrEmpty(message))
        {
            return;
        }

        capture.GoToSpikeMessages.Add(message);
    }

    internal static void GetCurrentGcDeltas(
        out int gen0Collections,
        out int gen1Collections,
        out int gen2Collections)
    {
        TickCapture capture = current;
        if (capture == null || capture.Cancelled)
        {
            gen0Collections = 0;
            gen1Collections = 0;
            gen2Collections = 0;
            return;
        }

        gen0Collections = Math.Max(0, GC.CollectionCount(0) - capture.StartGen0Collections);
        gen1Collections = Math.Max(0, GC.CollectionCount(1) - capture.StartGen1Collections);
        gen2Collections = Math.Max(0, GC.CollectionCount(2) - capture.StartGen2Collections);
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
        AppendTopRows(
            sb,
            "actor_post",
            ActorPostJobsGroupId,
            ActorPostJobsTotalId,
            detailLimit,
            TotalsGroupId,
            ActorPostJobsGroup);
        AppendTopRows(
            sb,
            "dirty_managers",
            DirtyManagersGroupId,
            DirtyManagersTotalId,
            detailLimit,
            TotalsGroupId,
            DirtyManagersGroup);
        AppendTopRows(
            sb,
            "tile_actions",
            ActorTileActionsGroupId,
            ActorTileActionsTotalId,
            detailLimit);
        if (ShouldCollectAiDetails)
        {
            AppendTopRows(
                sb,
                "ai_tasks",
                "ai_tasks",
                "ai_tasks",
                detailLimit,
                "ai_tasks_total");
            AppendTopRows(
                sb,
                "ai_actions",
                "ai_actions",
                "ai_actions",
                detailLimit,
                "ai_actions_total");
        }

        AppendTopRows(
            sb,
            "goto",
            GoToGroupId,
            GoToTotalId,
            Math.Max(detailLimit, 12),
            TotalsGroupId,
            GoToGroup);
        AppendTopRows(
            sb,
            "pathfinding",
            PathfindingGroupId,
            PathfindingTotalId,
            Math.Max(detailLimit, 12),
            TotalsGroupId,
            PathfindingGroup);
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
        RecordPathfindingMetrics(capture);

        capture.SetTotal(TickTotalId, capture.TotalSeconds);
        capture.SetTotal("vanilla_total", capture.VanillaSeconds);
        capture.SetTotal(CultiwayTotalId, capture.CultiwaySeconds);
        capture.SetTotal(ActorsTotalId, capture.ActorsSeconds);
        capture.SetTotal(
            ActorPostJobsTotalId,
            SumMetricSeconds(capture.ActorPostJobs));
        capture.SetTotal(
            ActorTileActionsTotalId,
            SumMetricSeconds(capture.ActorTileActions));
        capture.SetTotal(GoToTotalId, capture.GoToActionSeconds);
        capture.SetTotal(
            PathfindingTotalId,
            SumMetricSeconds(capture.Pathfinding));
        capture.SetTotal(
            DirtyManagersTotalId,
            SumMetricSeconds(capture.DirtyManagers));
        capture.SetTotal(BuildingsTotalId, capture.BuildingsSeconds);
        capture.SetTotal(WorldBehavioursTotalId, capture.WorldBehavioursSeconds);

        int previousSamples = History.Count;
        PublishGroup(TotalsGroup, capture.Totals, previousSamples);
        PublishGroup(PhasesGroup, capture.Phases, previousSamples);
        PublishGroup(ActorsGroup, capture.ActorJobs, previousSamples);
        PublishGroup(ActorPostJobsGroup, capture.ActorPostJobs, previousSamples);
        PublishGroup(ActorTileActionsGroup, capture.ActorTileActions, previousSamples);
        PublishGroup(GoToGroup, capture.GoTo, previousSamples);
        PublishGroup(PathfindingGroup, capture.Pathfinding, previousSamples);
        PublishGroup(DirtyManagersGroup, capture.DirtyManagers, previousSamples);
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
        for (int i = 0; i < capture.GoToSpikeMessages.Count; i++)
        {
            ModClass.LogInfo(capture.GoToSpikeMessages[i]);
        }
    }

    private static void RecordPathfindingMetrics(TickCapture capture)
    {
        PathfindingProfiler.Snapshot delta =
            PathfindingProfiler.CaptureSnapshot().DeltaFrom(capture.PathfindingStart);
        AddPathfindingMetric(capture.Pathfinding, "reuse", delta.Reuse);
        AddPathfindingMetric(capture.Pathfinding, "reuse_miss", delta.ReuseMiss);
        AddPathfindingMetric(capture.Pathfinding, "create", delta.Create);
        AddPathfindingMetric(capture.Pathfinding, "task_create", delta.TaskCreate);
        AddPathfindingMetric(capture.Pathfinding, "cancel", delta.Cancel);
        AddPathfindingMetric(capture.Pathfinding, "cancel_empty", delta.CancelEmpty);
        AddPathfindingMetric(capture.Pathfinding, "enqueue", delta.Enqueue);
        AddPathfindingMetric(capture.Pathfinding, "queue_wait", delta.QueueWait);
        AddPathfindingMetric(capture.Pathfinding, "background_path", delta.BackgroundPath);
    }

    private static void AddPathfindingMetric(
        Dictionary<string, Metric> target,
        string id,
        PathfindingProfiler.MetricSnapshot metric)
    {
        if (metric.Counter == 0L && metric.ElapsedTicks == 0L)
        {
            return;
        }

        AddMetric(target, id, metric.Seconds, metric.Counter);
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
        const string parallelMarker = ".parallel.";
        int parallelIndex = phase.IndexOf(parallelMarker, StringComparison.Ordinal);
        if (parallelIndex >= 0)
        {
            int jobStart = parallelIndex + parallelMarker.Length;
            int groupIndex = phase.IndexOf(".batch_group.", jobStart, StringComparison.Ordinal);
            id = groupIndex > jobStart
                ? "parallel." + phase.Substring(jobStart, groupIndex - jobStart)
                : "update_jobs_parallel";
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
            if (job.id.Equals("b3_findEnemyTarget", StringComparison.Ordinal))
            {
                continue;
            }

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
        const string dirtyManagerPrefix = "vanilla.maintenance.dirtymanagers.";
        if (phase.StartsWith(dirtyManagerPrefix, StringComparison.Ordinal))
        {
            return "vanilla.maintenance.dirtymanagers";
        }

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
        metric.MaximumSeconds = Math.Max(metric.MaximumSeconds, seconds);
    }

    private static double SumMetricSeconds(Dictionary<string, Metric> entries)
    {
        double total = 0.0;
        foreach (Metric metric in entries.Values)
        {
            total += metric.Seconds;
        }

        return total;
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
                state.SeedMaximum(id, previousSamples);
            }
        }

        foreach (string id in state.KnownEntries)
        {
            entries.TryGetValue(id, out Metric metric);
            double seconds = metric?.Seconds ?? 0.0;
            int counter = ClampCounter(metric?.Counter ?? 0L);
            Bench.benchSave(id, seconds, counter, state.GroupId);
            Bench.saveAverageCounter(id, state.GroupId);
            state.RecordMaximum(id, metric?.MaximumSeconds ?? 0.0);
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
        ResetGroup(ActorPostJobsGroup);
        ResetGroup(ActorTileActionsGroup);
        ResetGroup(GoToGroup);
        ResetGroup(PathfindingGroup);
        ResetGroup(DirtyManagersGroup);
        ResetGroup(BuildingsGroup);
        ResetGroup(WorldBehavioursGroup);
        ResetGroup(CultiwaySystemsGroup);
    }

    private static void ResetGroup(BenchmarkGroupState state)
    {
        state.KnownEntries.Clear();
        state.MaximumHistory.Clear();
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
        RegisterDebugTool(
            library,
            template,
            ActorTileActionsToolId,
            ActorTileActionsGroupId,
            ActorTileActionsTotalId,
            "Cultiway.Benchmark.ActorTileActions");
        if (SystemUtils.IsUnderDeveloper())
        {
            RegisterDebugTool(
                library,
                template,
                PathfindingToolId,
                PathfindingGroupId,
                PathfindingTotalId,
                "Cultiway.Benchmark.Pathfinding",
                ConfigurePathfindingDebugTool,
                ShowPathfindingDebugHeader);
        }

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
        string totalId,
        string nameLocaleKey = null,
        DebugToolAssetAction configureAction = null,
        DebugToolAssetAction headerAction = null)
    {
        if (library.has(id))
        {
            return;
        }

        library.add(new DebugToolAsset
        {
            id = id,
            name = nameLocaleKey == null ? id : nameLocaleKey.Localize(),
            type = DebugToolType.Benchmarks,
            priority = 2,
            benchmark_group_id = groupId,
            benchmark_total = totalId,
            benchmark_total_group = TotalsGroupId,
            split_benchmark = true,
            show_benchmark_buttons = true,
            update_timeout = 0.2f,
            action_start = configureAction ?? ConfigureDebugTool,
            action_1 = headerAction ?? ShowDebugHeader,
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

    private static void ConfigurePathfindingDebugTool(DebugTool tool)
    {
        ConfigureDebugTool(tool);
        tool.state = DebugToolState.Values;
    }

    private static void ShowPathfindingDebugHeader(DebugTool tool)
    {
        TickWindowStats stats = GetWindowStats();
        tool.setText(
            "Cultiway.Benchmark.Pathfinding.Samples".Localize() + ":",
            stats.Count);
        if (stats.Count == 0)
        {
            tool.setSeparator();
            return;
        }

        tool.setText(
            "Cultiway.Benchmark.Pathfinding.Measured".Localize() + ":",
            FormatMilliseconds(GetAverage(PathfindingTotalId, TotalsGroupId)));
        tool.setText(
            "Cultiway.Benchmark.Pathfinding.Format".Localize() + ":",
            "Cultiway.Benchmark.Pathfinding.FormatValue".Localize());
        tool.setText(
            "Cultiway.Benchmark.Pathfinding.Note".Localize() + ":",
            "Cultiway.Benchmark.Pathfinding.WorkerNote".Localize());
        tool.setSeparator();
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
            "Cultiway.Benchmark.Tick.ActualSpeed".Localize() + ":",
            WorldTimeRateTracker.HasActualSpeed
                ? WorldTimeRateTracker.ActualSpeed.ToString("0.00", CultureInfo.InvariantCulture) + "x"
                : "Cultiway.Benchmark.Tick.Measuring".Localize());
        tool.setText(
            "Cultiway.Benchmark.Tick.WorkCapacity".Localize() + ":",
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
        int limit,
        string totalGroupId = TotalsGroupId,
        BenchmarkGroupState maximumState = null)
    {
        double total = GetAverage(totalId, totalGroupId);
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

            rows.Add(new BenchmarkRow(
                data.id,
                seconds,
                data.getAverageCount(),
                maximumState?.GetMaximum(data.id) ?? 0.0));
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
                .Append('=').Append(FormatMilliseconds(row.Seconds));
            if (row.MaximumSeconds > 0.0)
            {
                sb.Append("|max=").Append(FormatMilliseconds(row.MaximumSeconds));
            }

            sb.Append('(')
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
        internal readonly Dictionary<string, Metric> ActorPostJobs =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> ActorTileActions =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> GoTo =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> Pathfinding =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> DirtyManagers =
            new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> BuildingJobs = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> WorldBehaviours = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Metric> CultiwaySystems = new(StringComparer.Ordinal);
        internal readonly List<string> GoToSpikeMessages = new(4);

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
        internal double GoToActionSeconds;
        internal int GoToSpikeLogs;
        internal int StartGen0Collections;
        internal int StartGen1Collections;
        internal int StartGen2Collections;
        internal string Mode = string.Empty;
        internal bool Cancelled;
        internal PathfindingProfiler.Snapshot PathfindingStart;

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
            ResetMetrics(ActorPostJobs);
            ResetMetrics(ActorTileActions);
            ResetMetrics(GoTo);
            ResetMetrics(Pathfinding);
            ResetMetrics(DirtyManagers);
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
            GoToActionSeconds = 0.0;
            GoToSpikeLogs = 0;
            GoToSpikeMessages.Clear();
            StartGen0Collections = 0;
            StartGen1Collections = 0;
            StartGen2Collections = 0;
            Mode = string.Empty;
            Cancelled = false;
            PathfindingStart = default;
        }

        private static void ResetMetrics(Dictionary<string, Metric> entries)
        {
            foreach (Metric metric in entries.Values)
            {
                metric.Seconds = 0.0;
                metric.Counter = 0L;
                metric.MaximumSeconds = 0.0;
            }
        }
    }

    internal sealed class Metric
    {
        internal double Seconds;
        internal long Counter;
        internal double MaximumSeconds;
    }

    private sealed class BenchmarkGroupState
    {
        internal BenchmarkGroupState(
            string groupId,
            bool trackMaximum = false)
        {
            GroupId = groupId;
            TrackMaximum = trackMaximum;
        }

        internal string GroupId { get; }
        internal bool TrackMaximum { get; }
        internal HashSet<string> KnownEntries { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, Queue<double>> MaximumHistory { get; } =
            new(StringComparer.Ordinal);

        internal void SeedMaximum(string id, int count)
        {
            if (!TrackMaximum)
            {
                return;
            }

            var values = new Queue<double>(HistoryCapacity);
            int seedCount = Math.Min(count, HistoryCapacity);
            for (int i = 0; i < seedCount; i++)
            {
                values.Enqueue(0.0);
            }

            MaximumHistory.Add(id, values);
        }

        internal void RecordMaximum(string id, double seconds)
        {
            if (!TrackMaximum)
            {
                return;
            }

            Queue<double> values = MaximumHistory[id];
            if (values.Count >= HistoryCapacity)
            {
                values.Dequeue();
            }

            values.Enqueue(Math.Max(0.0, seconds));
        }

        internal double GetMaximum(string id)
        {
            if (!TrackMaximum ||
                !MaximumHistory.TryGetValue(id, out Queue<double> values))
            {
                return 0.0;
            }

            double maximum = 0.0;
            foreach (double value in values)
            {
                maximum = Math.Max(maximum, value);
            }

            return maximum;
        }
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
        internal BenchmarkRow(
            string id,
            double seconds,
            long counter,
            double maximumSeconds)
        {
            Id = id;
            Seconds = seconds;
            Counter = counter;
            MaximumSeconds = maximumSeconds;
        }

        internal string Id { get; }
        internal double Seconds { get; }
        internal long Counter { get; }
        internal double MaximumSeconds { get; }
    }
}
