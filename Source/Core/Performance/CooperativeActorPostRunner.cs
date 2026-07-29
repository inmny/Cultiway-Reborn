using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Cultiway.Const;
using Cultiway.Core.Pathfinding;
using Cultiway.Patch;
using UnityEngine;
using ai.behaviours;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeActorPostRunner : ICooperativeBatchPostRunner<BatchActors, Actor>
{
    private const string EnemySearchJobId = "b3_findEnemyTarget";
    private const string TileActionJobId = "u5_curTileAction";
    private const string DeadCheckJobId = "u4_deadCheck";
    private const string FrozenCheckJobId = "u6_checkFrozen";
    private const string InsideBoatJobId = "u1_checkInside";
    private const string UpdateTimersJobId = "u8_checkUpdateTimers";
    private const string UnderForceJobId = "b1_checkUnderForce";
    private const string CurrentEnemyTargetJobId =
        "b2_checkCurrentEnemyTarget";
    private const string TaskVerifierJobId = "b4_checkTaskVerifier";
    private const string PathMovementJobId = "b5_checkPathMovement";
    private const string NaturalDeathJobId = "b55_update_natural_death";
    private const string UpdateAiJobId = "b6_update_ai";
    private const string SmoothMovementJobId =
        "u10_checkSmoothMovement";

    private static long enemyFinderCalls;
    private static long enemyFinderReuses;
    private static long enemyFinderEmptyResults;
    private static long enemyFinderLargeResults;
    private static long enemyFinderCandidates;
    private static long enemyFinderMaximumCandidates;

    private readonly ActorTileActionProfiler tileActionProfiler = new();
    private readonly Action<int> actorGateWorkItemAction;
    private readonly Action<int> tileActionWorkItemAction;
    private readonly Action<int> updateEligibilityWorkItemAction;
    private readonly Action<int> enemyPrepareWorkItemAction;
    private readonly Action<int> taskVerifierWorkItemAction;
    private readonly Action<int> searchWorkItemAction;
    private readonly Action<int> pathMovementWorkItemAction;
    private readonly Action<int> smoothMovementWorkItemAction;

    private enum PostStage
    {
        Idle,
        BeforeDeadCheck,
        BeforeTileAction,
        ScheduleTileAction,
        AwaitTileAction,
        CommitTileAction,
        BeforeFrozenCheck,
        BeforeUpdateEligibility,
        BeforeEnemySearch,
        PrepareEnemySearch,
        ScheduleEnemySearch,
        AwaitEnemySearch,
        CommitEnemySearch,
        BeforePathMovement,
        SchedulePathMovement,
        AwaitPathMovement,
        CommitPathMovement,
        AfterPathMovement,
        ScheduleSmoothMovement,
        AwaitSmoothMovement,
        CommitSmoothMovement,
        AfterSmoothMovement,
        Finish
    }

    private ActorGateBatchWork[] actorGateWorkItems =
        Array.Empty<ActorGateBatchWork>();
    private TileActionBatchWork[] tileActionWorkItems =
        Array.Empty<TileActionBatchWork>();
    private UpdateEligibilityBatchWork[] updateEligibilityWorkItems =
        Array.Empty<UpdateEligibilityBatchWork>();
    private EnemyPrepareBatchWork[] enemyPrepareWorkItems =
        Array.Empty<EnemyPrepareBatchWork>();
    private TaskVerifierBatchWork[] taskVerifierWorkItems =
        Array.Empty<TaskVerifierBatchWork>();
    private SearchWorkItem[] workItems = Array.Empty<SearchWorkItem>();
    private PathMovementBatchWork[] pathMovementWorkItems =
        Array.Empty<PathMovementBatchWork>();
    private SmoothMovementBatchWork[] smoothMovementWorkItems =
        Array.Empty<SmoothMovementBatchWork>();
    private Actor[][] activeBehaviorActorsByBatch =
        Array.Empty<Actor[]>();
    private int[] activeBehaviorActorCounts =
        Array.Empty<int>();
    private int[] underForceCheckedCounts =
        Array.Empty<int>();
    private bool[] activeBehaviorPartitionsValid =
        Array.Empty<bool>();
    private List<BatchActors> batches;
    private PostStage stage;
    private float elapsed;
    private int deadCheckJobIndex;
    private int tileActionJobIndex;
    private int frozenCheckJobIndex;
    private int updateTimersJobIndex;
    private int underForceJobIndex;
    private int currentEnemyTargetJobIndex;
    private int enemySearchJobIndex;
    private int taskVerifierJobIndex;
    private int pathMovementJobIndex;
    private int smoothMovementJobIndex;
    private int batchIndex;
    private int postJobIndex;
    private int workIndex;
    private int workCount;
    private int tileActionCommitIndex;
    private int pathCommitIndex;
    private int smoothCommitIndex;
    private int workGroupSize;
    private bool splitPostJobs;
    private bool taskVerifierStageCompleted;
    private SimulationWorkerPool.WorkTicket tileActionTicket;
    private SimulationWorkerPool.WorkTicket searchTicket;
    private SimulationWorkerPool.WorkTicket pathMovementTicket;
    private SimulationWorkerPool.WorkTicket smoothMovementTicket;
    private long searchScheduleStartedAt;
    private long searchScheduleCompletedAt;
    private long tileActionScheduleStartedAt;
    private long tileActionScheduleCompletedAt;
    private long pathMovementScheduleStartedAt;
    private long pathMovementScheduleCompletedAt;
    private long smoothMovementScheduleStartedAt;
    private long smoothMovementScheduleCompletedAt;

    internal CooperativeActorPostRunner()
    {
        actorGateWorkItemAction =
            RunActorGateWorkItemAt;
        tileActionWorkItemAction =
            RunTileActionWorkItemAt;
        updateEligibilityWorkItemAction =
            RunUpdateEligibilityWorkItemAt;
        enemyPrepareWorkItemAction =
            RunEnemyPrepareWorkItemAt;
        taskVerifierWorkItemAction =
            RunTaskVerifierWorkItemAt;
        searchWorkItemAction = SearchWorkItemAt;
        pathMovementWorkItemAction =
            RunPathMovementWorkItemAt;
        smoothMovementWorkItemAction =
            RunSmoothMovementWorkItemAt;
    }

    internal static string GetEnemyFinderDiagnostics()
    {
        long calls =
            Interlocked.Read(ref enemyFinderCalls);
        long candidates =
            Interlocked.Read(ref enemyFinderCandidates);
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "calls={0} reused={1} ({2:0.0}%) empty={3} large={4} " +
            "candidates={5:0.0}(avg) max={6}",
            calls,
            Interlocked.Read(ref enemyFinderReuses),
            calls == 0L
                ? 0.0
                : Interlocked.Read(ref enemyFinderReuses) *
                  100.0 /
                  calls,
            Interlocked.Read(ref enemyFinderEmptyResults),
            Interlocked.Read(ref enemyFinderLargeResults),
            calls == 0L
                ? 0.0
                : candidates / (double)calls,
            Interlocked.Read(
                ref enemyFinderMaximumCandidates));
    }

    public void Start(
        List<BatchActors> activeBatches,
        float cycleElapsed)
    {
        EnemyPresenceCache.EndPreparation();
        batches = activeBatches;
        elapsed = cycleElapsed;
        workGroupSize = Math.Max(1, PerformanceSettings.ForegroundParallelism * 4);
        batchIndex = 0;
        postJobIndex = 0;
        workIndex = 0;
        workCount = 0;
        tileActionCommitIndex = 0;
        pathCommitIndex = 0;
        smoothCommitIndex = 0;
        splitPostJobs =
            SimulationTickBenchmark.ShouldSplitActorPostJobs;
        taskVerifierStageCompleted = false;
        tileActionTicket = default;
        searchTicket = default;
        pathMovementTicket = default;
        smoothMovementTicket = default;
        searchScheduleStartedAt = 0L;
        searchScheduleCompletedAt = 0L;
        tileActionScheduleStartedAt = 0L;
        tileActionScheduleCompletedAt = 0L;
        pathMovementScheduleStartedAt = 0L;
        pathMovementScheduleCompletedAt = 0L;
        smoothMovementScheduleStartedAt = 0L;
        smoothMovementScheduleCompletedAt = 0L;
        PathFinder.Instance.ApplyWorkerWakeups();
        PrepareActiveBehaviorPartitions(batches.Count);
        tileActionProfiler.Start(batches.Count);
        DeferredPathRequestBatch.StartCycle();

        if (batches.Count == 0)
        {
            enemySearchJobIndex = -1;
            stage = PostStage.Finish;
            return;
        }

        enemySearchJobIndex = FindEnemySearchJobIndex(batches[0].jobs_post);
        if (enemySearchJobIndex < 0)
        {
            throw new InvalidOperationException("Actor post jobs 中不存在 b3_findEnemyTarget");
        }

        tileActionJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            TileActionJobId);
        if (tileActionJobIndex < 0 ||
            tileActionJobIndex >= enemySearchJobIndex)
        {
            throw new InvalidOperationException(
                "Actor post jobs 中 u5_curTileAction 顺序无效");
        }

        deadCheckJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            DeadCheckJobId);
        frozenCheckJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            FrozenCheckJobId);
        if (deadCheckJobIndex < 0 ||
            deadCheckJobIndex >= tileActionJobIndex ||
            frozenCheckJobIndex <= tileActionJobIndex)
        {
            throw new InvalidOperationException(
                "Actor post jobs 中 u4/u5/u6 顺序无效");
        }

        updateTimersJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            UpdateTimersJobId);
        underForceJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            UnderForceJobId);
        currentEnemyTargetJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            CurrentEnemyTargetJobId);
        if (updateTimersJobIndex <= tileActionJobIndex ||
            underForceJobIndex != updateTimersJobIndex + 1 ||
            currentEnemyTargetJobIndex != underForceJobIndex + 1 ||
            enemySearchJobIndex != currentEnemyTargetJobIndex + 1)
        {
            throw new InvalidOperationException(
                "Actor post jobs 中 u8/b1/b2/b3 顺序无效");
        }

        ValidateUpdateEligibilityJobs();
        ValidateActorGateJobs();

        pathMovementJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            PathMovementJobId);
        taskVerifierJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            TaskVerifierJobId);
        if (taskVerifierJobIndex !=
                enemySearchJobIndex + 1 ||
            pathMovementJobIndex !=
                taskVerifierJobIndex + 1)
        {
            throw new InvalidOperationException(
                "Actor post jobs 中 b3/b4/b5 顺序无效");
        }

        smoothMovementJobIndex = FindPostJobIndex(
            batches[0].jobs_post,
            SmoothMovementJobId);
        if (smoothMovementJobIndex <= pathMovementJobIndex)
        {
            throw new InvalidOperationException(
                "Actor post jobs 中 u10_checkSmoothMovement 顺序无效");
        }

        stage = PostStage.BeforeDeadCheck;
    }

    public bool WaitingForBackgroundWork =>
        (stage == PostStage.AwaitTileAction &&
         tileActionTicket.IsValid) ||
        (stage == PostStage.AwaitEnemySearch &&
         searchTicket.IsValid) ||
        (stage == PostStage.AwaitPathMovement &&
         pathMovementTicket.IsValid) ||
        (stage == PostStage.AwaitSmoothMovement &&
         smoothMovementTicket.IsValid);

    public bool IsBackgroundWorkCompleted =>
        stage switch
        {
            PostStage.AwaitTileAction when tileActionTicket.IsValid =>
                SimulationWorkerPool.Instance.IsCompleted(
                    tileActionTicket),
            PostStage.AwaitEnemySearch when searchTicket.IsValid =>
                SimulationWorkerPool.Instance.IsCompleted(searchTicket),
            PostStage.AwaitPathMovement when pathMovementTicket.IsValid =>
                SimulationWorkerPool.Instance.IsCompleted(pathMovementTicket),
            PostStage.AwaitSmoothMovement when smoothMovementTicket.IsValid =>
                SimulationWorkerPool.Instance.IsCompleted(smoothMovementTicket),
            _ => false
        };

    public bool TryJoinBackgroundWork(double maximumMilliseconds)
    {
        return stage switch
        {
            PostStage.AwaitTileAction when tileActionTicket.IsValid =>
                SimulationWorkerPool.Instance.TryWait(
                    tileActionTicket,
                    maximumMilliseconds),
            PostStage.AwaitEnemySearch when searchTicket.IsValid =>
                SimulationWorkerPool.Instance.TryWait(
                    searchTicket,
                    maximumMilliseconds),
            PostStage.AwaitPathMovement when pathMovementTicket.IsValid =>
                SimulationWorkerPool.Instance.TryWait(
                    pathMovementTicket,
                    maximumMilliseconds),
            PostStage.AwaitSmoothMovement when smoothMovementTicket.IsValid =>
                SimulationWorkerPool.Instance.TryWait(
                    smoothMovementTicket,
                    maximumMilliseconds),
            _ => true
        };
    }

    public void WaitForBackgroundWork()
    {
        if (stage == PostStage.AwaitTileAction &&
            tileActionTicket.IsValid)
        {
            SimulationWorkerPool.Instance.Wait(
                tileActionTicket);
        }
        else if (stage == PostStage.AwaitEnemySearch &&
            searchTicket.IsValid)
        {
            SimulationWorkerPool.Instance.Wait(searchTicket);
        }
        else if (stage == PostStage.AwaitPathMovement &&
                 pathMovementTicket.IsValid)
        {
            SimulationWorkerPool.Instance.Wait(pathMovementTicket);
        }
        else if (stage == PostStage.AwaitSmoothMovement &&
                 smoothMovementTicket.IsValid)
        {
            SimulationWorkerPool.Instance.Wait(
                smoothMovementTicket);
        }
    }

    public string GetNextPhaseName(string phasePrefix)
    {
        if (stage == PostStage.BeforeDeadCheck &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.u4.parallel";
        }

        if (stage == PostStage.BeforeTileAction &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.u5.schedule";
        }

        if (stage == PostStage.CommitTileAction &&
            tileActionCommitIndex >= batches.Count)
        {
            return GetNextPostRangePhaseName(
                phasePrefix,
                tileActionJobIndex + 1,
                frozenCheckJobIndex,
                "before_u6",
                restartRange: true);
        }

        if (stage == PostStage.BeforeFrozenCheck &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.u6.parallel";
        }

        if (stage == PostStage.BeforeUpdateEligibility &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.u8_b1.parallel";
        }

        if (stage == PostStage.BeforeEnemySearch &&
            batchIndex >= batches.Count)
        {
            return batches.Count == 0
                ? phasePrefix + ".post.finish"
                : phasePrefix + ".post.b3.prepare.batch.0";
        }

        if (stage == PostStage.PrepareEnemySearch &&
            batchIndex >= batches.Count)
        {
            return workCount > 0
                ? phasePrefix + ".post.b3.search.schedule"
                : GetNextPostRangePhaseName(
                    phasePrefix,
                    enemySearchJobIndex + 1,
                    pathMovementJobIndex,
                    "before_b5",
                    restartRange: true);
        }

        if (stage == PostStage.CommitEnemySearch && workIndex >= workCount)
        {
            return GetNextPostRangePhaseName(
                phasePrefix,
                enemySearchJobIndex + 1,
                pathMovementJobIndex,
                "before_b5",
                restartRange: true);
        }

        if (stage == PostStage.BeforePathMovement &&
            taskVerifierStageCompleted)
        {
            return phasePrefix + ".post.b5.schedule";
        }

        if (stage == PostStage.CommitPathMovement &&
            pathCommitIndex >= batches.Count)
        {
            return GetNextPostRangePhaseName(
                phasePrefix,
                pathMovementJobIndex + 1,
                smoothMovementJobIndex,
                "before_u10",
                restartRange: true);
        }

        if (stage == PostStage.AfterPathMovement &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.u10.schedule";
        }

        if (stage == PostStage.CommitSmoothMovement &&
            smoothCommitIndex >= batches.Count)
        {
            return GetNextPostRangePhaseName(
                phasePrefix,
                smoothMovementJobIndex + 1,
                int.MaxValue,
                "after_u10",
                restartRange: true);
        }

        if (stage == PostStage.AfterSmoothMovement &&
            batchIndex >= batches.Count)
        {
            return DeferredPathRequestBatch.HasPendingRequests
                ? phasePrefix + ".post.path_requests.flush"
                : phasePrefix + ".post.finish";
        }

        return stage switch
        {
            PostStage.BeforeDeadCheck =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    0,
                    deadCheckJobIndex,
                    "before_u4"),
            PostStage.BeforeTileAction =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    deadCheckJobIndex + 1,
                    tileActionJobIndex,
                    "before_u5"),
            PostStage.ScheduleTileAction =>
                phasePrefix + ".post.u5.schedule",
            PostStage.AwaitTileAction =>
                IsBackgroundWorkCompleted
                    ? phasePrefix + ".post.u5.complete"
                    : phasePrefix + ".post.u5.await",
            PostStage.CommitTileAction =>
                phasePrefix + ".post.u5.commit.batch." +
                tileActionCommitIndex,
            PostStage.BeforeFrozenCheck =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    tileActionJobIndex + 1,
                    frozenCheckJobIndex,
                    "before_u6"),
            PostStage.BeforeUpdateEligibility =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    frozenCheckJobIndex + 1,
                    updateTimersJobIndex,
                    "before_u8"),
            PostStage.BeforeEnemySearch =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    currentEnemyTargetJobIndex,
                    enemySearchJobIndex,
                    "before_b3"),
            PostStage.PrepareEnemySearch =>
                phasePrefix + ".post.b3.prepare.batch." + batchIndex,
            PostStage.ScheduleEnemySearch =>
                phasePrefix + ".post.b3.search.schedule",
            PostStage.AwaitEnemySearch =>
                IsBackgroundWorkCompleted
                    ? phasePrefix + ".post.b3.search.complete"
                    : phasePrefix + ".post.b3.search.await",
            PostStage.CommitEnemySearch =>
                phasePrefix + ".post.b3.commit.batch_group." + workIndex,
            PostStage.BeforePathMovement =>
                phasePrefix + ".post.b4.parallel",
            PostStage.SchedulePathMovement =>
                phasePrefix + ".post.b5.schedule",
            PostStage.AwaitPathMovement =>
                IsBackgroundWorkCompleted
                    ? phasePrefix + ".post.b5.complete"
                    : phasePrefix + ".post.b5.await",
            PostStage.CommitPathMovement =>
                phasePrefix + ".post.b5.commit.batch." +
                pathCommitIndex,
            PostStage.AfterPathMovement =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    pathMovementJobIndex + 1,
                    smoothMovementJobIndex,
                    "before_u10"),
            PostStage.ScheduleSmoothMovement =>
                phasePrefix + ".post.u10.schedule",
            PostStage.AwaitSmoothMovement =>
                IsBackgroundWorkCompleted
                    ? phasePrefix + ".post.u10.complete"
                    : phasePrefix + ".post.u10.await",
            PostStage.CommitSmoothMovement =>
                phasePrefix + ".post.u10.commit.batch." +
                smoothCommitIndex,
            PostStage.AfterSmoothMovement =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    smoothMovementJobIndex + 1,
                    int.MaxValue,
                    "after_u10"),
            PostStage.Finish =>
                DeferredPathRequestBatch.HasPendingRequests
                    ? phasePrefix + ".post.path_requests.flush"
                    : phasePrefix + ".post.finish",
            _ => phasePrefix + ".post.idle"
        };
    }

    public bool Step()
    {
        while (true)
        {
            switch (stage)
            {
                case PostStage.Idle:
                    return true;
                case PostStage.BeforeDeadCheck:
                    if (TryRunNextPostRange(
                            0,
                            deadCheckJobIndex))
                    {
                        return false;
                    }

                    RunActorGateJobs(
                        deadCheckJobIndex,
                        ActorGateKind.DeadCheck,
                        DeadCheckJobId);
                    batchIndex = 0;
                    postJobIndex =
                        deadCheckJobIndex + 1;
                    stage = PostStage.BeforeTileAction;
                    return false;
                case PostStage.BeforeTileAction:
                    if (TryRunNextPostRange(
                            deadCheckJobIndex + 1,
                            tileActionJobIndex))
                    {
                        return false;
                    }

                    PrepareTileActionWorkItems();
                    stage = PostStage.ScheduleTileAction;
                    continue;
                case PostStage.ScheduleTileAction:
                    tileActionScheduleStartedAt =
                        StartBenchmarkMeasurement();
                    try
                    {
                        tileActionTicket =
                            SimulationWorkerPool.Instance
                                .BeginIndexed(
                                    0,
                                    batches.Count,
                                    tileActionWorkItemAction);
                    }
                    finally
                    {
                        if (tileActionScheduleStartedAt != 0L)
                        {
                            tileActionScheduleCompletedAt =
                                Stopwatch.GetTimestamp();
                        }
                    }

                    stage = PostStage.AwaitTileAction;
                    return false;
                case PostStage.AwaitTileAction:
                    SimulationWorkerPool.Instance.Wait(
                        tileActionTicket);
                    SimulationWorkerPool.WorkResult tileResult;
                    try
                    {
                        tileResult =
                            SimulationWorkerPool.Instance
                                .Complete(tileActionTicket);
                    }
                    finally
                    {
                        tileActionTicket = default;
                    }

                    RecordTileActionBenchmark(tileResult);
                    tileActionCommitIndex = 0;
                    stage = PostStage.CommitTileAction;
                    return false;
                case PostStage.CommitTileAction:
                    if (tileActionCommitIndex < batches.Count)
                    {
                        int tileCommitEnd = splitPostJobs
                            ? tileActionCommitIndex + 1
                            : Math.Min(
                                batches.Count,
                                tileActionCommitIndex +
                                workGroupSize);
                        while (tileActionCommitIndex <
                               tileCommitEnd)
                        {
                            CommitTileActionWorkItem(
                                tileActionCommitIndex++);
                        }

                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex =
                        tileActionJobIndex + 1;
                    stage =
                        PostStage.BeforeFrozenCheck;
                    continue;
                case PostStage.BeforeFrozenCheck:
                    if (TryRunNextPostRange(
                            tileActionJobIndex + 1,
                            frozenCheckJobIndex))
                    {
                        return false;
                    }

                    RunActorGateJobs(
                        frozenCheckJobIndex,
                        ActorGateKind.FrozenCheck,
                        FrozenCheckJobId);
                    batchIndex = 0;
                    postJobIndex =
                        frozenCheckJobIndex + 1;
                    stage =
                        PostStage.BeforeUpdateEligibility;
                    return false;
                case PostStage.BeforeUpdateEligibility:
                    if (TryRunNextPostRange(
                            frozenCheckJobIndex + 1,
                            updateTimersJobIndex))
                    {
                        return false;
                    }

                    PrepareUpdateEligibilityWorkItems();
                    SimulationWorkerPool.WorkResult
                        eligibilityResult =
                            SimulationWorkerPool.Instance
                                .RunIndexed(
                                    0,
                                    batches.Count,
                                    updateEligibilityWorkItemAction);

                    RecordUpdateEligibilityBenchmark(
                        eligibilityResult);
                    CommitUpdateEligibilityWorkItems();
                    batchIndex = 0;
                    postJobIndex =
                        currentEnemyTargetJobIndex;
                    stage = PostStage.BeforeEnemySearch;
                    return false;
                case PostStage.BeforeEnemySearch:
                    if (TryRunNextPostRange(
                            currentEnemyTargetJobIndex,
                            enemySearchJobIndex))
                    {
                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex =
                        currentEnemyTargetJobIndex;
                    PrepareEnemySearchClassifications();
                    EnemyPresenceCache.BeginPreparation();
                    stage = PostStage.PrepareEnemySearch;
                    continue;
                case PostStage.PrepareEnemySearch:
                    int prepareEnd = splitPostJobs
                        ? batchIndex + 1
                        : Math.Min(
                            batches.Count,
                            batchIndex + workGroupSize);
                    while (batchIndex < prepareEnd &&
                           TryPrepareNextBatch())
                    {
                    }

                    if (batchIndex < batches.Count)
                    {
                        return false;
                    }

                    EnemyPresenceCache.EndPreparation();
                    workIndex = 0;
                    if (workCount == 0)
                    {
                        batchIndex = 0;
                        postJobIndex = enemySearchJobIndex + 1;
                        stage = PostStage.BeforePathMovement;
                        continue;
                    }

                    stage = PostStage.ScheduleEnemySearch;
                    continue;
                case PostStage.ScheduleEnemySearch:
                    // 搜索阶段只读取准备好的候选集；模拟停在此屏障，
                    // worker 完成后再由主线程按 workItems 原顺序提交。
                    searchScheduleStartedAt = StartBenchmarkMeasurement();
                    try
                    {
                        searchTicket = SimulationWorkerPool.Instance.BeginIndexed(
                            0,
                            workCount,
                            searchWorkItemAction);
                    }
                    finally
                    {
                        if (searchScheduleStartedAt != 0L)
                        {
                            searchScheduleCompletedAt = Stopwatch.GetTimestamp();
                        }
                    }

                    stage = PostStage.AwaitEnemySearch;
                    return false;
                case PostStage.AwaitEnemySearch:
                    SimulationWorkerPool.Instance.Wait(searchTicket);
                    SimulationWorkerPool.WorkResult searchResult;
                    try
                    {
                        searchResult = SimulationWorkerPool.Instance.Complete(searchTicket);
                    }
                    finally
                    {
                        searchTicket = default;
                    }

                    RecordSearchBenchmark(searchResult);
                    workIndex = 0;
                    stage = PostStage.CommitEnemySearch;
                    return false;
                case PostStage.CommitEnemySearch:
                    if (TryCommitNextGroup())
                    {
                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex = enemySearchJobIndex + 1;
                    stage = PostStage.BeforePathMovement;
                    continue;
                case PostStage.BeforePathMovement:
                    if (!taskVerifierStageCompleted)
                    {
                        RunTaskVerifierJobs();
                        taskVerifierStageCompleted = true;
                        return false;
                    }

                    PreparePathMovementWorkItems();
                    stage = PostStage.SchedulePathMovement;
                    continue;
                case PostStage.SchedulePathMovement:
                    pathMovementScheduleStartedAt =
                        StartBenchmarkMeasurement();
                    try
                    {
                        pathMovementTicket =
                            SimulationWorkerPool.Instance.BeginIndexed(
                                0,
                                batches.Count,
                                pathMovementWorkItemAction);
                    }
                    finally
                    {
                        if (pathMovementScheduleStartedAt != 0L)
                        {
                            pathMovementScheduleCompletedAt =
                                Stopwatch.GetTimestamp();
                        }
                    }

                    stage = PostStage.AwaitPathMovement;
                    return false;
                case PostStage.AwaitPathMovement:
                    SimulationWorkerPool.Instance.Wait(
                        pathMovementTicket);
                    SimulationWorkerPool.WorkResult pathResult;
                    try
                    {
                        pathResult =
                            SimulationWorkerPool.Instance.Complete(
                                pathMovementTicket);
                    }
                    finally
                    {
                        pathMovementTicket = default;
                    }

                    RecordPathMovementBenchmark(pathResult);
                    pathCommitIndex = 0;
                    stage = PostStage.CommitPathMovement;
                    return false;
                case PostStage.CommitPathMovement:
                    if (pathCommitIndex < batches.Count)
                    {
                        int pathCommitEnd = splitPostJobs
                            ? pathCommitIndex + 1
                            : Math.Min(
                                batches.Count,
                                pathCommitIndex +
                                workGroupSize);
                        while (pathCommitIndex <
                               pathCommitEnd)
                        {
                            CommitPathMovementWorkItem(
                                pathCommitIndex++);
                        }

                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex = pathMovementJobIndex + 1;
                    stage = PostStage.AfterPathMovement;
                    continue;
                case PostStage.AfterPathMovement:
                    if (TryRunNextPostRange(
                            pathMovementJobIndex + 1,
                            smoothMovementJobIndex))
                    {
                        return false;
                    }

                    PrepareSmoothMovementWorkItems();
                    stage = PostStage.ScheduleSmoothMovement;
                    continue;
                case PostStage.ScheduleSmoothMovement:
                    smoothMovementScheduleStartedAt =
                        StartBenchmarkMeasurement();
                    try
                    {
                        smoothMovementTicket =
                            SimulationWorkerPool.Instance.BeginIndexed(
                                0,
                                batches.Count,
                                smoothMovementWorkItemAction);
                    }
                    finally
                    {
                        if (smoothMovementScheduleStartedAt != 0L)
                        {
                            smoothMovementScheduleCompletedAt =
                                Stopwatch.GetTimestamp();
                        }
                    }

                    stage = PostStage.AwaitSmoothMovement;
                    return false;
                case PostStage.AwaitSmoothMovement:
                    SimulationWorkerPool.Instance.Wait(
                        smoothMovementTicket);
                    SimulationWorkerPool.WorkResult smoothResult;
                    try
                    {
                        smoothResult =
                            SimulationWorkerPool.Instance.Complete(
                                smoothMovementTicket);
                    }
                    finally
                    {
                        smoothMovementTicket = default;
                    }

                    RecordSmoothMovementBenchmark(
                        smoothResult);
                    smoothCommitIndex = 0;
                    stage = PostStage.CommitSmoothMovement;
                    return false;
                case PostStage.CommitSmoothMovement:
                    if (smoothCommitIndex < batches.Count)
                    {
                        int smoothCommitEnd = splitPostJobs
                            ? smoothCommitIndex + 1
                            : Math.Min(
                                batches.Count,
                                smoothCommitIndex +
                                workGroupSize);
                        while (smoothCommitIndex <
                               smoothCommitEnd)
                        {
                            CommitSmoothMovementWorkItem(
                                smoothCommitIndex++);
                        }

                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex =
                        smoothMovementJobIndex + 1;
                    stage = PostStage.AfterSmoothMovement;
                    continue;
                case PostStage.AfterSmoothMovement:
                    if (TryRunNextPostRange(
                            smoothMovementJobIndex + 1,
                            int.MaxValue))
                    {
                        return false;
                    }

                    stage = PostStage.Finish;
                    continue;
                case PostStage.Finish:
                    DeferredPathRequestBatch.CompleteCycle();
                    tileActionProfiler.Finish();
                    ResetCycleReferences(
                        clearPendingWork: false);
                    stage = PostStage.Idle;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Abort()
    {
        if (tileActionTicket.IsValid)
        {
            SimulationWorkerPool.Instance.WaitAndDiscard(
                tileActionTicket);
            tileActionTicket = default;
        }

        if (searchTicket.IsValid)
        {
            SimulationWorkerPool.Instance.WaitAndDiscard(searchTicket);
            searchTicket = default;
        }

        if (pathMovementTicket.IsValid)
        {
            SimulationWorkerPool.Instance.WaitAndDiscard(
                pathMovementTicket);
            pathMovementTicket = default;
        }

        if (smoothMovementTicket.IsValid)
        {
            SimulationWorkerPool.Instance.WaitAndDiscard(
                smoothMovementTicket);
            smoothMovementTicket = default;
        }

        DeferredPathRequestBatch.AbortCycle();
        EnemyPresenceCache.EndPreparation();
        tileActionProfiler.Abort();
        ClearActiveBehaviorPartitions();
        ResetCycleReferences(
            clearPendingWork: true);
        stage = PostStage.Idle;
    }

    private bool TryRunNextPostRange(int startJobIndex, int endJobIndex)
    {
        if (splitPostJobs)
        {
            while (batchIndex < batches.Count)
            {
                int currentBatchIndex = batchIndex;
                BatchActors batch = batches[currentBatchIndex];
                List<Job<Actor>> jobs = batch.jobs_post;
                int end = Math.Min(endJobIndex, jobs.Count);
                postJobIndex = Math.Max(postJobIndex, startJobIndex);
                if (postJobIndex < end)
                {
                    RunPostJob(batch, jobs[postJobIndex++], currentBatchIndex);
                    if (postJobIndex >= end)
                    {
                        batchIndex++;
                        postJobIndex = startJobIndex;
                    }

                    return true;
                }

                batchIndex++;
                postJobIndex = startJobIndex;
            }

            return false;
        }

        if (batchIndex >= batches.Count)
        {
            return false;
        }

        int batchEnd = Math.Min(
            batches.Count,
            batchIndex + workGroupSize);
        while (batchIndex < batchEnd)
        {
            int aggregateBatchIndex = batchIndex;
            BatchActors aggregateBatch =
                batches[batchIndex++];
            List<Job<Actor>> aggregateJobs =
                aggregateBatch.jobs_post;
            int aggregateEnd = Math.Min(
                endJobIndex,
                aggregateJobs.Count);
            for (int i = startJobIndex;
                 i < aggregateEnd;
                 i++)
            {
                RunPostJob(
                    aggregateBatch,
                    aggregateJobs[i],
                    aggregateBatchIndex);
            }
        }

        return true;
    }

    private void RunPostJob(
        BatchActors batch,
        Job<Actor> job,
        int currentBatchIndex)
    {
        batch._elapsed = elapsed;
        batch._cur_container = job.container;
        if (job.current_skips > 0)
        {
            job.current_skips--;
            return;
        }

        double startedAt = splitPostJobs
            ? Time.realtimeSinceStartupAsDouble
            : 0.0;
        bool profileTileAction =
            tileActionProfiler.Active &&
            ReferenceEquals(job.container, batch.c_main_tile_action);
        bool deferPathRequests =
            job.id.Equals(UpdateAiJobId, StringComparison.Ordinal);
        if (deferPathRequests)
        {
            DeferredPathRequestBatch.BeginCapture();
        }

        int actorsChecked = job.container.Count;
        bool completed = false;
        try
        {
            if (job.id.Equals(
                    InsideBoatJobId,
                    StringComparison.Ordinal))
            {
                actorsChecked = RunInsideBoatJob(batch);
            }
            else if (job.id.Equals(
                    PathMovementJobId,
                    StringComparison.Ordinal))
            {
                if (!TryRunActiveBehaviorJob(
                        job,
                        currentBatchIndex,
                        out actorsChecked))
                {
                    RunPathMovementJob(job.container);
                }
            }
            else if (job.id.Equals(
                         TileActionJobId,
                         StringComparison.Ordinal) &&
                     (!profileTileAction ||
                      !tileActionProfiler.TryRunSampledJob(
                          batch,
                          job,
                          currentBatchIndex)))
            {
                RunTileActionJob(job.container);
            }
            else if (IsActiveBehaviorJob(job.id) &&
                     TryRunActiveBehaviorJob(
                         job,
                         currentBatchIndex,
                         out actorsChecked))
            {
            }
            else if (!profileTileAction ||
                     !tileActionProfiler.TryRunSampledJob(
                         batch,
                         job,
                         currentBatchIndex))
            {
                job.job_updater();
            }

            completed = true;
        }
        finally
        {
            if (deferPathRequests)
            {
                if (completed)
                {
                    DeferredPathRequestBatch.EndCapture();
                }
                else
                {
                    DeferredPathRequestBatch.AbortCycle();
                }
            }
        }

        if (profileTileAction)
        {
            tileActionProfiler.RecordFullCalls(job.container);
        }

        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(0, job.random_tick_skips);
        }

        if (splitPostJobs)
        {
            job.time_benchmark +=
                Time.realtimeSinceStartupAsDouble - startedAt;
            job.counter += actorsChecked;
        }
    }

    private int RunInsideBoatJob(BatchActors batch)
    {
        if (!InsideBoatActorIndex.TryGetSnapshot(
                batch,
                out Actor[] actors,
                out int count))
        {
            return 0;
        }

        int processed = 0;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (actor == null ||
                actor.data == null ||
                !ReferenceEquals(actor.batch, batch) ||
                !actor.is_inside_boat)
            {
                InsideBoatActorIndex.Notify(
                    actor,
                    isInsideBoat: false);
                continue;
            }

            actor.u1_checkInside(elapsed);
            processed++;
        }

        InsideBoatActorIndex.RecordProcessed(processed);
        return processed;
    }

    private void PrepareActiveBehaviorPartitions(int batchCount)
    {
        if (activeBehaviorActorsByBatch.Length < batchCount)
        {
            Array.Resize(
                ref activeBehaviorActorsByBatch,
                batchCount);
            Array.Resize(
                ref activeBehaviorActorCounts,
                batchCount);
            Array.Resize(
                ref underForceCheckedCounts,
                batchCount);
            Array.Resize(
                ref activeBehaviorPartitionsValid,
                batchCount);
        }

        Array.Clear(
            activeBehaviorActorCounts,
            0,
            batchCount);
        Array.Clear(
            underForceCheckedCounts,
            0,
            batchCount);
        Array.Clear(
            activeBehaviorPartitionsValid,
            0,
            batchCount);
    }

    private void ValidateUpdateEligibilityJobs()
    {
        for (int i = 0; i < batches.Count; i++)
        {
            List<Job<Actor>> jobs = batches[i].jobs_post;
            if (jobs.Count <= currentEnemyTargetJobIndex)
            {
                throw new InvalidOperationException(
                    "Actor post jobs 数量不足，无法并行 u8/b1");
            }

            Job<Actor> updateTimersJob =
                jobs[updateTimersJobIndex];
            Job<Actor> underForceJob =
                jobs[underForceJobIndex];
            Job<Actor> currentEnemyTargetJob =
                jobs[currentEnemyTargetJobIndex];
            if (!updateTimersJob.id.Equals(
                    UpdateTimersJobId,
                    StringComparison.Ordinal) ||
                !underForceJob.id.Equals(
                    UnderForceJobId,
                    StringComparison.Ordinal) ||
                !currentEnemyTargetJob.id.Equals(
                    CurrentEnemyTargetJobId,
                    StringComparison.Ordinal) ||
                !ReferenceEquals(
                    updateTimersJob.container,
                    underForceJob.container) ||
                updateTimersJob.random_tick_skips != 0 ||
                underForceJob.random_tick_skips != 0)
            {
                throw new InvalidOperationException(
                    "Actor post jobs 的 u8/b1 并行不变量已改变");
            }
        }
    }

    private void ValidateActorGateJobs()
    {
        for (int i = 0; i < batches.Count; i++)
        {
            List<Job<Actor>> jobs =
                batches[i].jobs_post;
            if (jobs.Count <= frozenCheckJobIndex)
            {
                throw new InvalidOperationException(
                    "Actor post jobs 数量不足，无法并行 u4/u6");
            }

            Job<Actor> deadCheckJob =
                jobs[deadCheckJobIndex];
            Job<Actor> frozenCheckJob =
                jobs[frozenCheckJobIndex];
            if (!deadCheckJob.id.Equals(
                    DeadCheckJobId,
                    StringComparison.Ordinal) ||
                !frozenCheckJob.id.Equals(
                    FrozenCheckJobId,
                    StringComparison.Ordinal) ||
                !ReferenceEquals(
                    deadCheckJob.container,
                    frozenCheckJob.container) ||
                deadCheckJob.random_tick_skips != 0 ||
                frozenCheckJob.random_tick_skips != 0)
            {
                throw new InvalidOperationException(
                    "Actor post jobs 的 u4/u6 并行不变量已改变");
            }
        }
    }

    private void RunActorGateJobs(
        int jobIndex,
        ActorGateKind kind,
        string benchmarkId)
    {
        int count = batches.Count;
        if (actorGateWorkItems.Length < count)
        {
            int previousLength =
                actorGateWorkItems.Length;
            Array.Resize(
                ref actorGateWorkItems,
                count);
            for (int i = previousLength;
                 i < count;
                 i++)
            {
                actorGateWorkItems[i] =
                    new ActorGateBatchWork();
            }
        }

        bool enabled =
            kind != ActorGateKind.FrozenCheck ||
            !World.world.isPaused();
        int actorsClassified = 0;
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[jobIndex];
            ActorGateBatchWork work =
                actorGateWorkItems[i];
            batch._elapsed = elapsed;
            batch._cur_container = job.container;
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            if (container.Count > 0 ||
                container.isDirtyContainer())
            {
                container.checkAddRemove();
            }

            Actor[] actors =
                container.getFastSimpleArray() ??
                Array.Empty<Actor>();
            int actorCount = container.Count;
            batch._array = actors;
            batch._count = actorCount;
            work.Configure(
                job,
                actors,
                actorCount,
                kind,
                enabled);
            actorsClassified +=
                enabled
                    ? actorCount
                    : 0;
        }

        SimulationWorkerPool.WorkResult result =
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                count,
                actorGateWorkItemAction);
        if (SimulationTickBenchmark.IsCapturing)
        {
            SimulationTickBenchmark.RecordActorJobMetric(
                benchmarkId + ".classify_parallel",
                result.WallSeconds,
                actorsClassified);
        }

        long commitStartedAt =
            StartBenchmarkMeasurement();
        int actorsChecked = 0;
        for (int i = 0; i < count; i++)
        {
            ActorGateBatchWork work =
                actorGateWorkItems[i];
            if (work.Skipped)
            {
                work.Reset();
                continue;
            }

            long jobStartedAt = splitPostJobs
                ? Stopwatch.GetTimestamp()
                : 0L;
            Actor[] serialActors =
                work.SerialActors;
            for (int actorIndex = 0;
                 actorIndex < work.SerialCount;
                 actorIndex++)
            {
                Actor actor =
                    serialActors[actorIndex];
                if (kind ==
                    ActorGateKind.DeadCheck)
                {
                    actor.u4_deadCheck(elapsed);
                }
                else
                {
                    actor.u6_checkFrozen(elapsed);
                }
            }

            actorsChecked += work.Count;
            if (splitPostJobs)
            {
                work.Job.time_benchmark +=
                    (Stopwatch.GetTimestamp() -
                     jobStartedAt) /
                    (double)Stopwatch.Frequency;
                work.Job.counter += work.Count;
            }

            work.Reset();
        }

        RecordBenchmarkMeasurement(
            benchmarkId,
            commitStartedAt,
            actorsChecked);
    }

    private void RunActorGateWorkItemAt(int index)
    {
        actorGateWorkItems[index]
            .RunParallel();
    }

    private void PrepareUpdateEligibilityWorkItems()
    {
        int count = batches.Count;
        if (updateEligibilityWorkItems.Length < count)
        {
            int previousLength =
                updateEligibilityWorkItems.Length;
            Array.Resize(
                ref updateEligibilityWorkItems,
                count);
            for (int i = previousLength; i < count; i++)
            {
                updateEligibilityWorkItems[i] =
                    new UpdateEligibilityBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> updateTimersJob =
                batch.jobs_post[updateTimersJobIndex];
            Job<Actor> underForceJob =
                batch.jobs_post[underForceJobIndex];
            UpdateEligibilityBatchWork work =
                updateEligibilityWorkItems[i];
            activeBehaviorActorCounts[i] = 0;
            underForceCheckedCounts[i] = 0;
            activeBehaviorPartitionsValid[i] = false;
            if (updateTimersJob.current_skips != 0 ||
                underForceJob.current_skips != 0)
            {
                throw new InvalidOperationException(
                    "u8/b1 不应具有运行时跳帧状态");
            }

            batch._elapsed = elapsed;
            batch._cur_container =
                updateTimersJob.container;
            if (paused)
            {
                work.ConfigureSkipped(
                    batch,
                    updateTimersJob,
                    underForceJob);
                continue;
            }

            ObjectContainer<Actor> container =
                updateTimersJob.container;
            if (container.Count > 0 ||
                container.isDirtyContainer())
            {
                container.checkAddRemove();
            }

            Actor[] actors =
                container.getFastSimpleArray() ??
                Array.Empty<Actor>();
            int actorCount = container.Count;
            batch._array = actors;
            batch._count = actorCount;
            Actor[] activeActors =
                actorCount == 0
                    ? Array.Empty<Actor>()
                    : EnsureActiveBehaviorActorCapacity(
                        i,
                        actorCount);
            work.Configure(
                batch,
                updateTimersJob,
                underForceJob,
                actors,
                actorCount,
                activeActors,
                elapsed);
        }
    }

    private void RunUpdateEligibilityWorkItemAt(int index)
    {
        updateEligibilityWorkItems[index]
            .RunParallel();
    }

    private void CommitUpdateEligibilityWorkItems()
    {
        for (int i = 0; i < batches.Count; i++)
        {
            UpdateEligibilityBatchWork work =
                updateEligibilityWorkItems[i];
            if (work.Skipped)
            {
                work.Reset();
                continue;
            }

            activeBehaviorActorCounts[i] =
                work.ActiveCount;
            underForceCheckedCounts[i] =
                work.UnderForceChecked;
            activeBehaviorPartitionsValid[i] = true;
            if (splitPostJobs)
            {
                work.UpdateTimersJob.counter +=
                    work.Count;
                work.UnderForceJob.counter +=
                    work.UnderForceChecked;
            }

            work.Reset();
        }
    }

    private Actor[] EnsureActiveBehaviorActorCapacity(
        int currentBatchIndex,
        int count)
    {
        Actor[] actors =
            activeBehaviorActorsByBatch[currentBatchIndex];
        if (actors != null &&
            actors.Length >= count)
        {
            return actors;
        }

        int capacity = Math.Max(
            PerformanceSettings.SimulationBatchSize,
            count);
        actors = new Actor[capacity];
        activeBehaviorActorsByBatch[currentBatchIndex] =
            actors;
        return actors;
    }

    private bool TryRunActiveBehaviorJob(
        Job<Actor> job,
        int currentBatchIndex,
        out int actorsChecked)
    {
        actorsChecked = job.container.Count;
        if (!activeBehaviorPartitionsValid[currentBatchIndex])
        {
            return false;
        }

        ObjectContainer<Actor> container = job.container;
        if (container.isDirtyContainer())
        {
            // u8 之后发生角色增删时，旧分区不再代表原版容器顺序；
            // 本 tick 剩余阶段全部退回原路径。
            activeBehaviorPartitionsValid[currentBatchIndex] =
                false;
            return false;
        }

        Actor[] actors =
            activeBehaviorActorsByBatch[currentBatchIndex];
        int count =
            activeBehaviorActorCounts[currentBatchIndex];
        switch (job.id)
        {
            case UnderForceJobId:
            {
                // u8 与 b1 在原版中相邻且都只修改当前角色；
                // 活跃分区已在同一次顺序遍历中完成 b1。
                actorsChecked =
                    underForceCheckedCounts[
                        currentBatchIndex];
                return true;
            }
            case TaskVerifierJobId:
            {
                actorsChecked = 0;
                int writeIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    Actor actor = actors[i];
                    if (actor._update_done ||
                        actor._beh_skip)
                    {
                        if (writeIndex < 0)
                        {
                            writeIndex = i;
                        }

                        continue;
                    }

                    actorsChecked++;
                    var task = actor.ai.task;
                    if (task != null &&
                        task.has_verifier &&
                        task.task_verifier.execute(actor) ==
                        BehResult.Stop)
                    {
                        actor.cancelAllBeh();
                        actor.skipBehaviour();
                    }
                    else if (actor.is_moving)
                    {
                        actor.skipBehaviour();
                    }

                    if (actor._beh_skip)
                    {
                        if (writeIndex < 0)
                        {
                            writeIndex = i;
                        }

                        continue;
                    }

                    if (writeIndex >= 0)
                    {
                        actors[writeIndex++] = actor;
                    }
                }

                activeBehaviorActorCounts[currentBatchIndex] =
                    writeIndex < 0
                        ? count
                        : writeIndex;
                return true;
            }
            case PathMovementJobId:
                activeBehaviorActorCounts[currentBatchIndex] =
                    RunPathMovementJob(
                        actors,
                        count,
                        out actorsChecked);
                return true;
            case NaturalDeathJobId:
            {
                actorsChecked = 0;
                int writeIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    Actor actor = actors[i];
                    if (actor._update_done ||
                        actor._beh_skip)
                    {
                        if (writeIndex < 0)
                        {
                            writeIndex = i;
                        }

                        continue;
                    }

                    actorsChecked++;
                    actor.b55_updateNaturalDeaths(elapsed);
                    if (actor._update_done ||
                        actor._beh_skip)
                    {
                        if (writeIndex < 0)
                        {
                            writeIndex = i;
                        }

                        continue;
                    }

                    if (writeIndex >= 0)
                    {
                        actors[writeIndex++] = actor;
                    }
                }

                activeBehaviorActorCounts[currentBatchIndex] =
                    writeIndex < 0
                        ? count
                        : writeIndex;
                return true;
            }
            case UpdateAiJobId:
                actorsChecked = 0;
                for (int i = 0; i < count; i++)
                {
                    Actor actor = actors[i];
                    if (actor._update_done ||
                        actor._beh_skip)
                    {
                        continue;
                    }

                    actorsChecked++;
                    actor.b6_updateAI(elapsed);
                }

                return true;
            default:
                return false;
        }
    }

    private static bool IsActiveBehaviorJob(string jobId)
    {
        return jobId.Equals(
                   UnderForceJobId,
                   StringComparison.Ordinal) ||
               jobId.Equals(
                   TaskVerifierJobId,
                   StringComparison.Ordinal) ||
               jobId.Equals(
                   NaturalDeathJobId,
                   StringComparison.Ordinal) ||
               jobId.Equals(
                   UpdateAiJobId,
                   StringComparison.Ordinal);
    }

    private void RunTaskVerifierJobs()
    {
        int count = batches.Count;
        if (taskVerifierWorkItems.Length < count)
        {
            int previousLength =
                taskVerifierWorkItems.Length;
            Array.Resize(
                ref taskVerifierWorkItems,
                count);
            for (int i = previousLength;
                 i < count;
                 i++)
            {
                taskVerifierWorkItems[i] =
                    new TaskVerifierBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        int actorsClassified = 0;
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[taskVerifierJobIndex];
            TaskVerifierBatchWork work =
                taskVerifierWorkItems[i];
            batch._elapsed = elapsed;
            batch._cur_container = job.container;
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(batch, job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            bool containerDirty =
                container.isDirtyContainer();
            if (containerDirty)
            {
                activeBehaviorPartitionsValid[i] =
                    false;
            }

            if (paused)
            {
                if (containerDirty)
                {
                    container.checkAddRemove();
                }

                work.ConfigureSkipped(batch, job);
                continue;
            }

            bool activePartition =
                activeBehaviorPartitionsValid[i];
            if (activePartition)
            {
                Actor[] activeActors =
                    activeBehaviorActorsByBatch[i];
                int activeCount =
                    activeBehaviorActorCounts[i];
                work.Configure(
                    batch,
                    job,
                    activeActors,
                    activeCount,
                    activePartition: true);
                actorsClassified += activeCount;
                continue;
            }

            if (container.Count > 0 ||
                containerDirty)
            {
                container.checkAddRemove();
            }

            Actor[] containerActors =
                container.getFastSimpleArray() ??
                Array.Empty<Actor>();
            int containerCount = container.Count;
            batch._array = containerActors;
            batch._count = containerCount;
            work.Configure(
                batch,
                job,
                containerActors,
                containerCount,
                activePartition: false);
            actorsClassified += containerCount;
        }

        long startedAt = StartBenchmarkMeasurement();
        if (count > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                count,
                taskVerifierWorkItemAction);
        }
        else if (count == 1)
        {
            RunTaskVerifierWorkItemAt(0);
        }

        RecordBenchmarkMeasurement(
            "b4_checkTaskVerifier.classify_parallel",
            startedAt,
            actorsClassified);
        for (int i = 0; i < count; i++)
        {
            CommitTaskVerifierWorkItem(i);
        }
    }

    private void RunTaskVerifierWorkItemAt(int index)
    {
        taskVerifierWorkItems[index].RunParallel();
    }

    private void CommitTaskVerifierWorkItem(int index)
    {
        TaskVerifierBatchWork work =
            taskVerifierWorkItems[index];
        if (work.Skipped)
        {
            work.Reset();
            return;
        }

        long startedAt = StartBenchmarkMeasurement();
        Actor[] actors = work.Actors;
        TaskVerifierKind[] kinds = work.Kinds;
        Actor[] activeActors = work.ActivePartition
            ? actors
            : EnsureActiveBehaviorActorCapacity(
                index,
                work.Count);
        int activeCount = 0;
        int actorsChecked = 0;
        for (int i = 0; i < work.Count; i++)
        {
            TaskVerifierKind kind = kinds[i];
            if (kind == TaskVerifierKind.Inactive)
            {
                continue;
            }

            Actor actor = actors[i];
            actorsChecked++;
            switch (kind)
            {
                case TaskVerifierKind.Verifier:
                {
                    var task = actor.ai.task;
                    if (task != null &&
                        task.has_verifier &&
                        task.task_verifier.execute(actor) ==
                        BehResult.Stop)
                    {
                        actor.cancelAllBeh();
                        actor.skipBehaviour();
                    }
                    else if (actor.is_moving)
                    {
                        actor.skipBehaviour();
                    }

                    break;
                }
                case TaskVerifierKind.Moving:
                    if (actor.is_moving)
                    {
                        actor.skipBehaviour();
                    }

                    break;
            }

            if (!actor._update_done &&
                !actor._beh_skip)
            {
                activeActors[activeCount++] = actor;
            }
        }

        activeBehaviorActorCounts[index] =
            activeCount;
        activeBehaviorPartitionsValid[index] =
            true;
        Job<Actor> job = work.Job;
        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(
                0,
                job.random_tick_skips);
        }

        RecordBenchmarkMeasurement(
            "b4_checkTaskVerifier",
            startedAt,
            actorsChecked);
        work.Reset();
    }

    private void PrepareTileActionWorkItems()
    {
        int count = batches.Count;
        if (tileActionWorkItems.Length < count)
        {
            int previousLength =
                tileActionWorkItems.Length;
            Array.Resize(
                ref tileActionWorkItems,
                count);
            for (int i = previousLength; i < count; i++)
            {
                tileActionWorkItems[i] =
                    new TileActionBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        bool[] fires =
            World.world.tile_manager.fires;
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[tileActionJobIndex];
            TileActionBatchWork work =
                tileActionWorkItems[i];
            batch._elapsed = elapsed;
            batch._cur_container = job.container;
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(batch, job);
                continue;
            }

            if (paused)
            {
                work.ConfigureSkipped(batch, job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            if (container.Count == 0 &&
                !container.isDirtyContainer())
            {
                work.Configure(
                    batch,
                    job,
                    Array.Empty<Actor>(),
                    0,
                    fires);
                continue;
            }

            container.checkAddRemove();
            Actor[] actors =
                container.getFastSimpleArray();
            int actorCount = container.Count;
            batch._array = actors;
            batch._count = actorCount;
            work.Configure(
                batch,
                job,
                actors,
                actorCount,
                fires);
        }
    }

    private void RunTileActionWorkItemAt(int index)
    {
        tileActionWorkItems[index]
            .RunParallel();
    }

    private void CommitTileActionWorkItem(int index)
    {
        TileActionBatchWork work =
            tileActionWorkItems[index];
        if (work.Skipped)
        {
            work.Reset();
            return;
        }

        Job<Actor> job = work.Job;
        long startedAt = StartBenchmarkMeasurement();
        bool profiled =
            tileActionProfiler.Active &&
            tileActionProfiler.TryRunSampledJob(
                work.Batch,
                job,
                index);
        if (!profiled)
        {
            Actor[] serialActors =
                work.SerialActors;
            for (int i = 0;
                 i < work.SerialCount;
                 i++)
            {
                serialActors[i]
                    .u5_curTileAction();
            }
        }

        if (tileActionProfiler.Active)
        {
            tileActionProfiler.RecordFullCalls(
                job.container);
        }

        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(
                0,
                job.random_tick_skips);
        }

        if (splitPostJobs)
        {
            job.time_benchmark +=
                (Stopwatch.GetTimestamp() - startedAt) /
                (double)Stopwatch.Frequency;
            job.counter += work.Checked;
        }

        work.Reset();
    }

    private static void RunTileActionJob(
        ObjectContainer<Actor> container)
    {
        if (container.Count == 0 &&
            !container.isDirtyContainer())
        {
            return;
        }

        container.checkAddRemove();
        if (World.world.isPaused())
        {
            return;
        }

        Actor[] actors = container.getFastSimpleArray();
        int count = container.Count;
        bool[] fires = World.world.tile_manager.fires;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (CanSkipSafeGroundTileAction(
                    actor,
                    fires))
            {
                continue;
            }

            actor.u5_curTileAction();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanSkipSafeGroundTileAction(
        Actor actor,
        bool[] fires)
    {
        if (actor._update_done)
        {
            return true;
        }

        WorldTile tile = actor.current_tile;
        TileTypeBase type = tile.Type;
        ActorAsset asset = actor.asset;
        Building building = tile.building;
        if (type.ground &&
            !type.block &&
            !type.damage_units &&
            !fires[tile.tile_id] &&
            !asset.is_boat &&
            (building == null ||
             !building.asset.has_step_action))
        {
            bool waterCreature =
                asset.force_ocean_creature ||
                actor.subspecies
                    ?.has_trait_water_creature == true;
            if (!waterCreature ||
                asset.force_land_creature)
            {
                return true;
            }
        }

        return actor.position_height > 0f ||
               actor.isFlying();
    }

    private void PreparePathMovementWorkItems()
    {
        int count = batches.Count;
        if (pathMovementWorkItems.Length < count)
        {
            int previousLength =
                pathMovementWorkItems.Length;
            Array.Resize(
                ref pathMovementWorkItems,
                count);
            for (int i = previousLength; i < count; i++)
            {
                pathMovementWorkItems[i] =
                    new PathMovementBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[pathMovementJobIndex];
            PathMovementBatchWork work =
                pathMovementWorkItems[i];
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(batch, job);
                continue;
            }

            if (paused)
            {
                work.ConfigureSkipped(batch, job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            if (activeBehaviorPartitionsValid[i] &&
                !container.isDirtyContainer())
            {
                work.ConfigureParallel(
                    batch,
                    job,
                    activeBehaviorActorsByBatch[i],
                    activeBehaviorActorCounts[i]);
                continue;
            }

            activeBehaviorPartitionsValid[i] = false;
            work.ConfigureFallback(batch, job);
        }
    }

    private void RunPathMovementWorkItemAt(int index)
    {
        pathMovementWorkItems[index].RunParallel();
    }

    private void CommitPathMovementWorkItem(int index)
    {
        PathMovementBatchWork work =
            pathMovementWorkItems[index];
        Job<Actor> job = work.Job;
        if (work.Skipped)
        {
            work.Reset();
            return;
        }

        long startedAt = StartBenchmarkMeasurement();
        int actorsChecked;
        if (work.Fallback)
        {
            RunPathMovementJob(
                job.container,
                out actorsChecked);
        }
        else
        {
            actorsChecked = work.Checked;
            Actor[] actors = work.Actors;
            int count = work.Count;
            PathMovementWorkEntry[] entries =
                work.Entries;
            int writeIndex = -1;
            for (int i = 0; i < count; i++)
            {
                Actor actor = actors[i];
                PathMovementWorkEntry entry =
                    entries[i];
                bool retain =
                    entry.Kind ==
                    PathMovementWorkKind.Retain;
                if (entry.Kind ==
                    PathMovementWorkKind.RequiresSerial)
                {
                    PatchAboutPathfinding
                        .CommitPreparedPathMovement(
                            actor,
                            entry.Prepared);
                    actor.skipBehaviour();
                    retain = false;
                }

                if (!retain)
                {
                    if (writeIndex < 0)
                    {
                        writeIndex = i;
                    }

                    continue;
                }

                if (writeIndex >= 0)
                {
                    actors[writeIndex++] = actor;
                }
            }

            activeBehaviorActorCounts[index] =
                writeIndex < 0
                    ? count
                    : writeIndex;
        }

        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(
                0,
                job.random_tick_skips);
        }

        if (splitPostJobs)
        {
            job.time_benchmark +=
                (Stopwatch.GetTimestamp() - startedAt) /
                (double)Stopwatch.Frequency;
            job.counter += actorsChecked;
        }

        work.Reset();
    }

    private void PrepareSmoothMovementWorkItems()
    {
        int count = batches.Count;
        if (smoothMovementWorkItems.Length < count)
        {
            int previousLength =
                smoothMovementWorkItems.Length;
            Array.Resize(
                ref smoothMovementWorkItems,
                count);
            for (int i = previousLength; i < count; i++)
            {
                smoothMovementWorkItems[i] =
                    new SmoothMovementBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[smoothMovementJobIndex];
            SmoothMovementBatchWork work =
                smoothMovementWorkItems[i];
            batch._elapsed = elapsed;
            batch._cur_container = job.container;
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(batch, job);
                continue;
            }

            if (paused)
            {
                work.ConfigureSkipped(batch, job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            if (container.Count == 0 &&
                !container.isDirtyContainer())
            {
                work.Configure(
                    batch,
                    job,
                    Array.Empty<Actor>(),
                    0,
                    elapsed);
                continue;
            }

            container.checkAddRemove();
            Actor[] actors =
                container.getFastSimpleArray();
            int actorCount = container.Count;
            batch._array = actors;
            batch._count = actorCount;
            work.Configure(
                batch,
                job,
                actors,
                actorCount,
                elapsed);
        }
    }

    private void RunSmoothMovementWorkItemAt(int index)
    {
        smoothMovementWorkItems[index].RunParallel();
    }

    private void CommitSmoothMovementWorkItem(int index)
    {
        SmoothMovementBatchWork work =
            smoothMovementWorkItems[index];
        Job<Actor> job = work.Job;
        if (work.Skipped)
        {
            work.Reset();
            return;
        }

        long startedAt = StartBenchmarkMeasurement();
        Actor[] actors = work.SerialActors;
        PatchAboutPathfinding.PreparedSmoothMovement[]
            entries = work.Entries;
        for (int i = 0;
             i < work.SerialCount;
             i++)
        {
            PatchAboutPathfinding.PreparedSmoothMovement
                entry = entries[i];
            PatchAboutPathfinding
                .CommitPreparedSmoothMovement(
                    actors[i],
                    work.Elapsed,
                    entry);
        }

        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(
                0,
                job.random_tick_skips);
        }

        if (splitPostJobs)
        {
            job.time_benchmark +=
                (Stopwatch.GetTimestamp() - startedAt) /
                (double)Stopwatch.Frequency;
            job.counter += work.Checked;
        }

        work.Reset();
    }

    private void RunPathMovementJob(
        ObjectContainer<Actor> container)
    {
        RunPathMovementJob(container, out _);
    }

    private void RunPathMovementJob(
        ObjectContainer<Actor> container,
        out int actorsChecked)
    {
        actorsChecked = 0;
        if (container.Count == 0 &&
            !container.isDirtyContainer())
        {
            return;
        }

        container.checkAddRemove();
        if (World.world.isPaused())
        {
            return;
        }

        RunPathMovementJob(
            container.getFastSimpleArray(),
            container.Count,
            out actorsChecked);
    }

    private static int RunPathMovementJob(
        Actor[] actors,
        int count,
        out int actorsChecked)
    {
        actorsChecked = 0;
        int writeIndex = -1;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (actor._update_done ||
                actor._beh_skip)
            {
                if (writeIndex < 0)
                {
                    writeIndex = i;
                }

                continue;
            }

            actorsChecked++;
            if (!PatchAboutPathfinding
                    .TryUpdateActivePathMovement(actor))
            {
                if (writeIndex >= 0)
                {
                    actors[writeIndex++] = actor;
                }

                continue;
            }

            actor.skipBehaviour();
            if (writeIndex < 0)
            {
                writeIndex = i;
            }
        }

        return writeIndex < 0
            ? count
            : writeIndex;
    }

    private void PrepareEnemySearchClassifications()
    {
        int count = batches.Count;
        if (enemyPrepareWorkItems.Length < count)
        {
            int previousLength =
                enemyPrepareWorkItems.Length;
            Array.Resize(
                ref enemyPrepareWorkItems,
                count);
            for (int i = previousLength;
                 i < count;
                 i++)
            {
                enemyPrepareWorkItems[i] =
                    new EnemyPrepareBatchWork();
            }
        }

        bool paused = World.world.isPaused();
        int actorsClassified = 0;
        for (int i = 0; i < count; i++)
        {
            BatchActors batch = batches[i];
            Job<Actor> job =
                batch.jobs_post[enemySearchJobIndex];
            EnemyPrepareBatchWork work =
                enemyPrepareWorkItems[i];
            batch._elapsed = elapsed;
            batch._cur_container = job.container;
            if (job.current_skips > 0)
            {
                job.current_skips--;
                work.ConfigureSkipped(batch, job);
                continue;
            }

            ObjectContainer<Actor> container =
                job.container;
            bool containerDirty =
                container.isDirtyContainer();
            if (containerDirty)
            {
                activeBehaviorPartitionsValid[i] =
                    false;
            }

            if (paused)
            {
                if (containerDirty)
                {
                    container.checkAddRemove();
                }

                work.Configure(
                    batch,
                    job,
                    Array.Empty<Actor>(),
                    0,
                    activePartition: false);
                continue;
            }

            bool activePartition =
                activeBehaviorPartitionsValid[i];
            if (activePartition)
            {
                Actor[] activeActors =
                    activeBehaviorActorsByBatch[i];
                int activeCount =
                    activeBehaviorActorCounts[i];
                work.Configure(
                    batch,
                    job,
                    activeActors,
                    activeCount,
                    activePartition: true);
                actorsClassified += activeCount;
                continue;
            }

            if (container.Count == 0 &&
                !containerDirty)
            {
                work.Configure(
                    batch,
                    job,
                    Array.Empty<Actor>(),
                    0,
                    activePartition: false);
                continue;
            }

            container.checkAddRemove();
            Actor[] containerActors =
                container.getFastSimpleArray();
            int containerCount = container.Count;
            batch._array = containerActors;
            batch._count = containerCount;
            work.Configure(
                batch,
                job,
                containerActors,
                containerCount,
                activePartition: false);
            actorsClassified += containerCount;
        }

        long startedAt = StartBenchmarkMeasurement();
        if (count > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                count,
                enemyPrepareWorkItemAction);
        }
        else if (count == 1)
        {
            RunEnemyPrepareWorkItemAt(0);
        }

        RecordBenchmarkMeasurement(
            "b3_findEnemyTarget.classify_parallel",
            startedAt,
            actorsClassified);
    }

    private void RunEnemyPrepareWorkItemAt(int index)
    {
        enemyPrepareWorkItems[index]
            .RunParallel();
    }

    private bool TryPrepareNextBatch()
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        int currentBatchIndex = batchIndex++;
        EnemyPrepareBatchWork work =
            enemyPrepareWorkItems[currentBatchIndex];
        if (work.Skipped)
        {
            work.Reset();
            return true;
        }

        long startedAt = StartBenchmarkMeasurement();
        int actorsChecked =
            CommitEnemySearchClassification(
                work,
                currentBatchIndex);
        Job<Actor> job = work.Job;
        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(
                0,
                job.random_tick_skips);
        }

        job.counter += actorsChecked;
        RecordBenchmarkMeasurement(
            "b3_findEnemyTarget.prepare",
            startedAt,
            actorsChecked);
        work.Reset();
        return true;
    }

    private int CommitEnemySearchClassification(
        EnemyPrepareBatchWork work,
        int currentBatchIndex)
    {
        Actor[] actionActors = work.ActionActors;
        EnemyPrepareKind[] actionKinds =
            work.ActionKinds;
        for (int i = 0; i < work.ActionCount; i++)
        {
            EnemyPrepareKind kind = actionKinds[i];
            Actor actor = actionActors[i];

            switch (kind)
            {
                case EnemyPrepareKind.AttackTarget:
                    if (!actor.hasTask() ||
                        !actor.ai.task.in_combat)
                    {
                        actor.setTask(
                            "fighting",
                            pClean: true,
                            pCleanJob: true);
                    }

                    break;
                case EnemyPrepareKind.Search:
                    PrepareEnemySearch(actor);
                    break;
            }
        }

        if (work.ActivePartition)
        {
            activeBehaviorActorCounts[
                currentBatchIndex] =
                work.RetainedCount;
            return work.Checked;
        }

        return work.Count;
    }

    private void ClearActiveBehaviorPartitions()
    {
        for (int i = 0;
             i < activeBehaviorActorsByBatch.Length;
             i++)
        {
            Actor[] actors =
                activeBehaviorActorsByBatch[i];
            if (actors != null)
            {
                Array.Clear(actors, 0, actors.Length);
            }
        }

        Array.Clear(
            activeBehaviorActorCounts,
            0,
            activeBehaviorActorCounts.Length);
        Array.Clear(
            underForceCheckedCounts,
            0,
            underForceCheckedCounts.Length);
        Array.Clear(
            activeBehaviorPartitionsValid,
            0,
            activeBehaviorPartitionsValid.Length);
    }

    private void PrepareEnemySearch(
        Actor actor)
    {
        actor._timeout_targets =
            0.1f + Randy.randomFloat(0f, 1f);
        bool collectDiagnostics =
            Bench.bench_enabled;
        int reusedBefore =
            collectDiagnostics
                ? EnemiesFinder.counter_reused
                : 0;
        EnemyFinderData enemyData;
        if (!EnemyPresenceCache
                .TryGetPreparationEmptyResult(
                    actor.current_tile,
                    actor.kingdom,
                    SimGlobals.m.unit_chunk_sight_range,
                    out enemyData))
        {
            enemyData =
                EnemiesFinder.findEnemiesFrom(
                    actor.current_tile,
                    actor.kingdom);
        }

        List<BaseSimObject> primaryCandidates =
            enemyData.list;
        if (collectDiagnostics)
        {
            int candidateCount =
                primaryCandidates.Count;
            Interlocked.Increment(
                ref enemyFinderCalls);
            if (EnemiesFinder.counter_reused >
                reusedBefore)
            {
                Interlocked.Increment(
                    ref enemyFinderReuses);
            }

            if (candidateCount == 0)
            {
                Interlocked.Increment(
                    ref enemyFinderEmptyResults);
            }
            else if (candidateCount > 50)
            {
                Interlocked.Increment(
                    ref enemyFinderLargeResults);
            }

            Interlocked.Add(
                ref enemyFinderCandidates,
                candidateCount);
            UpdateMaximum(
                ref enemyFinderMaximumCandidates,
                candidateCount);
        }

        bool findClosest = true;
        int randomOffset = 0;
        if (primaryCandidates.Count > 50)
        {
            findClosest = Randy.randomChance(0.6f);
            if (!findClosest)
            {
                randomOffset = Randy.randomInt(
                    0,
                    primaryCandidates.Count);
            }
        }

        int aggressionSourceCount =
            actor._aggression_targets.Count;
        if (primaryCandidates.Count == 0 &&
            aggressionSourceCount == 0)
        {
            return;
        }

        SearchWorkItem item = RentWorkItem();
        item.Configure(
            actor,
            primaryCandidates,
            findClosest,
            randomOffset,
            aggressionSourceCount);
    }

    private static void UpdateMaximum(
        ref long target,
        long value)
    {
        long maximum = Interlocked.Read(ref target);
        while (value > maximum)
        {
            long previous =
                Interlocked.CompareExchange(
                    ref target,
                    value,
                    maximum);
            if (previous == maximum)
            {
                return;
            }

            maximum = previous;
        }
    }

    private void SearchWorkItemAt(int index)
    {
        workItems[index].Search();
    }

    private bool TryCommitNextGroup()
    {
        if (workIndex >= workCount)
        {
            return false;
        }

        int startIndex = workIndex;
        int endIndex = Math.Min(workCount, startIndex + workGroupSize);
        long startedAt = StartBenchmarkMeasurement();
        for (int i = startIndex; i < endIndex; i++)
        {
            workItems[i].Commit();
        }

        workIndex = endIndex;
        RecordBenchmarkMeasurement(
            "b3_findEnemyTarget.commit",
            startedAt,
            endIndex - startIndex);
        return true;
    }

    private SearchWorkItem RentWorkItem()
    {
        if (workCount >= workItems.Length)
        {
            int previousLength = workItems.Length;
            int nextLength = Math.Max(64, previousLength * 2);
            Array.Resize(ref workItems, nextLength);
            for (int i = previousLength; i < nextLength; i++)
            {
                workItems[i] = new SearchWorkItem();
            }
        }

        return workItems[workCount++];
    }

    private string GetNextPostRangePhaseName(
        string phasePrefix,
        int startJobIndex,
        int endJobIndex,
        string aggregateName,
        bool restartRange = false)
    {
        int phaseBatchIndex = restartRange ? 0 : batchIndex;
        int phaseJobIndex = restartRange ? startJobIndex : postJobIndex;
        if (splitPostJobs &&
            TryPeekNextPostJob(
                startJobIndex,
                endJobIndex,
                phaseBatchIndex,
                phaseJobIndex,
                out Job<Actor> nextJob))
        {
            return phasePrefix +
                   ".post.serial." +
                   nextJob.id;
        }

        return phasePrefix +
               ".post." +
               aggregateName +
               ".batch." +
               phaseBatchIndex;
    }

    private bool TryPeekNextPostJob(
        int startJobIndex,
        int endJobIndex,
        int initialBatchIndex,
        int initialJobIndex,
        out Job<Actor> nextJob)
    {
        int candidateBatchIndex = initialBatchIndex;
        int candidateJobIndex = Math.Max(initialJobIndex, startJobIndex);
        while (candidateBatchIndex < batches.Count)
        {
            List<Job<Actor>> jobs = batches[candidateBatchIndex].jobs_post;
            int end = Math.Min(endJobIndex, jobs.Count);
            if (candidateJobIndex < end)
            {
                nextJob = jobs[candidateJobIndex];
                return true;
            }

            candidateBatchIndex++;
            candidateJobIndex = startJobIndex;
        }

        nextJob = null;
        return false;
    }

    private void ResetCycleReferences(
        bool clearPendingWork)
    {
        for (int i = 0; i < workCount; i++)
        {
            workItems[i].Reset();
        }

        if (clearPendingWork)
        {
            for (int i = 0;
                 i < actorGateWorkItems.Length;
                 i++)
            {
                actorGateWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < tileActionWorkItems.Length;
                 i++)
            {
                tileActionWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < updateEligibilityWorkItems.Length;
                 i++)
            {
                updateEligibilityWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < enemyPrepareWorkItems.Length;
                 i++)
            {
                enemyPrepareWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < taskVerifierWorkItems.Length;
                 i++)
            {
                taskVerifierWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < pathMovementWorkItems.Length;
                 i++)
            {
                pathMovementWorkItems[i]?.Reset();
            }

            for (int i = 0;
                 i < smoothMovementWorkItems.Length;
                 i++)
            {
                smoothMovementWorkItems[i]?.Reset();
            }
        }

        workCount = 0;
        workIndex = 0;
        tileActionCommitIndex = 0;
        pathCommitIndex = 0;
        smoothCommitIndex = 0;
        batchIndex = 0;
        postJobIndex = 0;
        batches = null;
        splitPostJobs = false;
        tileActionTicket = default;
        searchTicket = default;
        pathMovementTicket = default;
        smoothMovementTicket = default;
        tileActionScheduleStartedAt = 0L;
        tileActionScheduleCompletedAt = 0L;
        searchScheduleStartedAt = 0L;
        searchScheduleCompletedAt = 0L;
        pathMovementScheduleStartedAt = 0L;
        pathMovementScheduleCompletedAt = 0L;
        smoothMovementScheduleStartedAt = 0L;
        smoothMovementScheduleCompletedAt = 0L;
    }

    private static int FindEnemySearchJobIndex(List<Job<Actor>> jobs)
    {
        return FindPostJobIndex(jobs, EnemySearchJobId);
    }

    private static int FindPostJobIndex(
        List<Job<Actor>> jobs,
        string jobId)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            if (jobs[i].id.Equals(
                    jobId,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static long StartBenchmarkMeasurement()
    {
        return SimulationTickBenchmark.IsCapturing
            ? Stopwatch.GetTimestamp()
            : 0L;
    }

    private static void RecordBenchmarkMeasurement(
        string id,
        long startedAt,
        int counter)
    {
        if (startedAt == 0L)
        {
            return;
        }

        double seconds = (Stopwatch.GetTimestamp() - startedAt) / (double)Stopwatch.Frequency;
        SimulationTickBenchmark.RecordActorJobMetric(id, seconds, counter);
    }

    private void RecordTileActionBenchmark(
        SimulationWorkerPool.WorkResult result)
    {
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        int actorsHandled = 0;
        for (int i = 0; i < batches.Count; i++)
        {
            TileActionBatchWork work =
                tileActionWorkItems[i];
            actorsHandled +=
                work.Checked -
                work.SerialCount;
        }

        long mainThreadOverlap =
            CalculateOverlap(
                result.StartedAt,
                result.CompletedAt,
                tileActionScheduleStartedAt,
                tileActionScheduleCompletedAt) +
            Math.Min(
                result.WallTicks,
                result.MainWaitTicks);
        double backgroundSeconds = Math.Max(
            0L,
            result.WallTicks - mainThreadOverlap) /
            (double)Stopwatch.Frequency;
        SimulationTickBenchmark.RecordActorBackgroundMetric(
            "u5_curTileAction.classify_parallel",
            "vanilla.actors.post.u5.background",
            result.WallSeconds,
            backgroundSeconds,
            actorsHandled);
    }

    private void RecordUpdateEligibilityBenchmark(
        SimulationWorkerPool.WorkResult result)
    {
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        int actorsHandled = 0;
        for (int i = 0; i < batches.Count; i++)
        {
            actorsHandled +=
                updateEligibilityWorkItems[i].Count;
        }

        SimulationTickBenchmark.RecordActorJobMetric(
            "u8_b1.parallel",
            result.WallSeconds,
            actorsHandled);
    }

    private void RecordSearchBenchmark(SimulationWorkerPool.WorkResult result)
    {
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        long mainThreadOverlap =
            CalculateOverlap(
                result.StartedAt,
                result.CompletedAt,
                searchScheduleStartedAt,
                searchScheduleCompletedAt) +
            Math.Min(result.WallTicks, result.MainWaitTicks);
        double backgroundSeconds = Math.Max(
            0L,
            result.WallTicks - mainThreadOverlap) /
            (double)Stopwatch.Frequency;
        SimulationTickBenchmark.RecordActorBackgroundMetric(
            "b3_findEnemyTarget.search_parallel",
            "vanilla.actors.post.b3.search.background",
            result.WallSeconds,
            backgroundSeconds,
            result.ExecutedItems);
    }

    private void RecordPathMovementBenchmark(
        SimulationWorkerPool.WorkResult result)
    {
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        int actorsChecked = 0;
        for (int i = 0; i < batches.Count; i++)
        {
            actorsChecked +=
                pathMovementWorkItems[i].Checked;
        }

        long mainThreadOverlap =
            CalculateOverlap(
                result.StartedAt,
                result.CompletedAt,
                pathMovementScheduleStartedAt,
                pathMovementScheduleCompletedAt) +
            Math.Min(
                result.WallTicks,
                result.MainWaitTicks);
        double backgroundSeconds = Math.Max(
            0L,
            result.WallTicks - mainThreadOverlap) /
            (double)Stopwatch.Frequency;
        SimulationTickBenchmark.RecordActorBackgroundMetric(
            "b5_checkPathMovement.parallel",
            "vanilla.actors.post.b5.background",
            result.WallSeconds,
            backgroundSeconds,
            actorsChecked);
    }

    private void RecordSmoothMovementBenchmark(
        SimulationWorkerPool.WorkResult result)
    {
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        int actorsHandled = 0;
        for (int i = 0; i < batches.Count; i++)
        {
            SmoothMovementBatchWork work =
                smoothMovementWorkItems[i];
            actorsHandled +=
                work.Checked -
                work.SerialCount;
        }

        long mainThreadOverlap =
            CalculateOverlap(
                result.StartedAt,
                result.CompletedAt,
                smoothMovementScheduleStartedAt,
                smoothMovementScheduleCompletedAt) +
            Math.Min(
                result.WallTicks,
                result.MainWaitTicks);
        double backgroundSeconds = Math.Max(
            0L,
            result.WallTicks - mainThreadOverlap) /
            (double)Stopwatch.Frequency;
        SimulationTickBenchmark.RecordActorBackgroundMetric(
            "u10_checkSmoothMovement.parallel",
            "vanilla.actors.post.u10.background",
            result.WallSeconds,
            backgroundSeconds,
            actorsHandled);
    }

    private static long CalculateOverlap(
        long startedAt,
        long completedAt,
        long rangeStartedAt,
        long rangeCompletedAt)
    {
        if (rangeStartedAt == 0L ||
            rangeCompletedAt <= rangeStartedAt ||
            completedAt <= startedAt)
        {
            return 0L;
        }

        long overlapStart = Math.Max(startedAt, rangeStartedAt);
        long overlapEnd = Math.Min(completedAt, rangeCompletedAt);
        return Math.Max(0L, overlapEnd - overlapStart);
    }

    private enum TaskVerifierKind : byte
    {
        Retain,
        Inactive,
        Moving,
        Verifier
    }

    private sealed class TaskVerifierBatchWork
    {
        internal BatchActors Batch { get; private set; }
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal int Count { get; private set; }
        internal bool ActivePartition { get; private set; }
        internal bool Skipped { get; private set; }
        internal TaskVerifierKind[] Kinds { get; private set; } =
            Array.Empty<TaskVerifierKind>();

        internal void Configure(
            BatchActors batch,
            Job<Actor> job,
            Actor[] actors,
            int count,
            bool activePartition)
        {
            Batch = batch;
            Job = job;
            Actors = actors;
            Count = count;
            ActivePartition = activePartition;
            Skipped = false;
            if (Kinds.Length < count)
            {
                Kinds = new TaskVerifierKind[
                    Math.Max(
                        PerformanceSettings.SimulationBatchSize,
                        count)];
            }
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> job)
        {
            Batch = batch;
            Job = job;
            Actors = null;
            Count = 0;
            ActivePartition = false;
            Skipped = true;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Count == 0)
            {
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                Actor actor = Actors[i];
                TaskVerifierKind kind;
                if (actor._update_done ||
                    actor._beh_skip)
                {
                    kind = TaskVerifierKind.Inactive;
                }
                else
                {
                    var task = actor.ai.task;
                    if (task != null &&
                        task.has_verifier)
                    {
                        kind = TaskVerifierKind.Verifier;
                    }
                    else if (actor.is_moving)
                    {
                        kind = TaskVerifierKind.Moving;
                    }
                    else
                    {
                        kind = TaskVerifierKind.Retain;
                    }
                }

                Kinds[i] = kind;
            }
        }

        internal void Reset()
        {
            if (Count > 0)
            {
                Array.Clear(Kinds, 0, Count);
            }

            Batch = null;
            Job = null;
            Actors = null;
            Count = 0;
            ActivePartition = false;
            Skipped = false;
        }
    }

    private enum EnemyPrepareKind : byte
    {
        NoSearch,
        AttackTarget,
        Search
    }

    private sealed class EnemyPrepareBatchWork
    {
        internal BatchActors Batch { get; private set; }
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal int Count { get; private set; }
        internal bool ActivePartition { get; private set; }
        internal bool Skipped { get; private set; }
        internal Actor[] ActionActors { get; private set; } =
            Array.Empty<Actor>();
        internal EnemyPrepareKind[] ActionKinds { get; private set; } =
            Array.Empty<EnemyPrepareKind>();
        internal int ActionCount { get; private set; }
        internal int RetainedCount { get; private set; }
        internal int Checked { get; private set; }

        internal void Configure(
            BatchActors batch,
            Job<Actor> job,
            Actor[] actors,
            int count,
            bool activePartition)
        {
            Batch = batch;
            Job = job;
            Actors = actors;
            Count = count;
            ActivePartition = activePartition;
            Skipped = false;
            ActionCount = 0;
            RetainedCount = 0;
            Checked = 0;
            if (ActionActors.Length < count)
            {
                int capacity = Math.Max(
                    PerformanceSettings.SimulationBatchSize,
                    count);
                ActionActors = new Actor[capacity];
                ActionKinds =
                    new EnemyPrepareKind[capacity];
            }
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> job)
        {
            Batch = batch;
            Job = job;
            Actors = null;
            Count = 0;
            ActivePartition = false;
            Skipped = true;
            ActionCount = 0;
            RetainedCount = 0;
            Checked = 0;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Count == 0)
            {
                return;
            }

            Actor[] actors = Actors;
            Actor[] actionActors = ActionActors;
            EnemyPrepareKind[] actionKinds =
                ActionKinds;
            int actionCount = 0;
            int retainedCount = 0;
            int checkedActors = 0;
            for (int i = 0; i < Count; i++)
            {
                Actor actor = actors[i];
                EnemyPrepareKind kind;
                if (actor._update_done ||
                    actor._beh_skip)
                {
                    continue;
                }

                checkedActors++;
                if (ActivePartition)
                {
                    actors[retainedCount++] = actor;
                }

                if (
                    !actor.isAllowedToLookForEnemies() ||
                    actor.isInWaterAndCantAttack() ||
                    actor._has_status_strange_urge)
                {
                    kind = EnemyPrepareKind.NoSearch;
                }
                else if (actor.has_attack_target)
                {
                    kind = EnemyPrepareKind.AttackTarget;
                }
                else if (actor._timeout_targets > 0f)
                {
                    kind = EnemyPrepareKind.NoSearch;
                }
                else
                {
                    kind = EnemyPrepareKind.Search;
                }

                if (kind is EnemyPrepareKind.AttackTarget or
                    EnemyPrepareKind.Search)
                {
                    actionActors[actionCount] = actor;
                    actionKinds[actionCount] = kind;
                    actionCount++;
                }
            }

            ActionCount = actionCount;
            RetainedCount = ActivePartition
                ? retainedCount
                : Count;
            Checked = ActivePartition
                ? checkedActors
                : Count;
        }

        internal void Reset()
        {
            if (ActionCount > 0)
            {
                Array.Clear(
                    ActionActors,
                    0,
                    ActionCount);
                Array.Clear(
                    ActionKinds,
                    0,
                    ActionCount);
            }

            Batch = null;
            Job = null;
            Actors = null;
            Count = 0;
            ActivePartition = false;
            Skipped = false;
            ActionCount = 0;
            RetainedCount = 0;
            Checked = 0;
        }
    }

    private enum PathMovementWorkKind : byte
    {
        Retain,
        Handled,
        RequiresSerial,
        Inactive
    }

    private enum ActorGateKind : byte
    {
        DeadCheck,
        FrozenCheck
    }

    private sealed class ActorGateBatchWork
    {
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal Actor[] SerialActors { get; private set; } =
            Array.Empty<Actor>();
        internal int Count { get; private set; }
        internal int SerialCount { get; private set; }
        internal ActorGateKind Kind { get; private set; }
        internal bool Enabled { get; private set; }
        internal bool Skipped { get; private set; }

        internal void Configure(
            Job<Actor> job,
            Actor[] actors,
            int count,
            ActorGateKind kind,
            bool enabled)
        {
            Job = job;
            Actors = actors;
            Count = count;
            SerialCount = 0;
            Kind = kind;
            Enabled = enabled;
            Skipped = false;
            if (SerialActors.Length < count)
            {
                SerialActors =
                    new Actor[
                        Math.Max(
                            PerformanceSettings
                                .SimulationBatchSize,
                            count)];
            }
        }

        internal void ConfigureSkipped(Job<Actor> job)
        {
            Job = job;
            Actors = null;
            Count = 0;
            SerialCount = 0;
            Kind = default;
            Enabled = false;
            Skipped = true;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                !Enabled ||
                Count == 0)
            {
                return;
            }

            int serialCount = 0;
            Actor[] actors = Actors;
            Actor[] serialActors = SerialActors;
            if (Kind == ActorGateKind.DeadCheck)
            {
                for (int i = 0; i < Count; i++)
                {
                    Actor actor = actors[i];
                    if (!actor._update_done &&
                        (!actor.isAlive() ||
                         actor.isInMagnet() ||
                         actor.under_forces))
                    {
                        serialActors[serialCount++] =
                            actor;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Count; i++)
                {
                    Actor actor = actors[i];
                    if (!actor._update_done &&
                        (actor.is_ai_frozen ||
                         actor.is_unconscious))
                    {
                        serialActors[serialCount++] =
                            actor;
                    }
                }
            }

            SerialCount = serialCount;
        }

        internal void Reset()
        {
            if (SerialCount > 0)
            {
                Array.Clear(
                    SerialActors,
                    0,
                    SerialCount);
            }

            Job = null;
            Actors = null;
            Count = 0;
            SerialCount = 0;
            Kind = default;
            Enabled = false;
            Skipped = false;
        }
    }

    private struct PathMovementWorkEntry
    {
        internal PathMovementWorkKind Kind;
        internal PatchAboutPathfinding.PreparedPathMovement
            Prepared;
    }

    private sealed class UpdateEligibilityBatchWork
    {
        internal BatchActors Batch { get; private set; }
        internal Job<Actor> UpdateTimersJob { get; private set; }
        internal Job<Actor> UnderForceJob { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal Actor[] ActiveActors { get; private set; }
        internal int Count { get; private set; }
        internal int ActiveCount { get; private set; }
        internal int UnderForceChecked { get; private set; }
        internal float Elapsed { get; private set; }
        internal bool Skipped { get; private set; }

        internal void Configure(
            BatchActors batch,
            Job<Actor> updateTimersJob,
            Job<Actor> underForceJob,
            Actor[] actors,
            int count,
            Actor[] activeActors,
            float elapsed)
        {
            Batch = batch;
            UpdateTimersJob = updateTimersJob;
            UnderForceJob = underForceJob;
            Actors = actors;
            ActiveActors = activeActors;
            Count = count;
            ActiveCount = 0;
            UnderForceChecked = 0;
            Elapsed = elapsed;
            Skipped = false;
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> updateTimersJob,
            Job<Actor> underForceJob)
        {
            Batch = batch;
            UpdateTimersJob = updateTimersJob;
            UnderForceJob = underForceJob;
            Actors = null;
            ActiveActors = null;
            Count = 0;
            ActiveCount = 0;
            UnderForceChecked = 0;
            Elapsed = 0f;
            Skipped = true;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Count == 0)
            {
                return;
            }

            Actor[] actors = Actors;
            Actor[] activeActors = ActiveActors;
            float elapsed = Elapsed;
            int activeCount = 0;
            int underForceChecked = 0;
            for (int i = 0; i < Count; i++)
            {
                Actor actor = actors[i];
                actor.u8_checkUpdateTimers(elapsed);
                if (actor._update_done)
                {
                    continue;
                }

                underForceChecked++;
                actor.b1_checkUnderForce(elapsed);
                if (!actor._beh_skip)
                {
                    activeActors[activeCount++] = actor;
                }
            }

            ActiveCount = activeCount;
            UnderForceChecked = underForceChecked;
        }

        internal void Reset()
        {
            Batch = null;
            UpdateTimersJob = null;
            UnderForceJob = null;
            Actors = null;
            ActiveActors = null;
            Count = 0;
            ActiveCount = 0;
            UnderForceChecked = 0;
            Elapsed = 0f;
            Skipped = false;
        }
    }

    private sealed class TileActionBatchWork
    {
        internal BatchActors Batch { get; private set; }
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal int Count { get; private set; }
        internal int Checked { get; private set; }
        internal int SerialCount { get; private set; }
        internal bool Skipped { get; private set; }
        internal bool[] Fires { get; private set; }
        internal Actor[] SerialActors { get; private set; } =
            Array.Empty<Actor>();

        internal void Configure(
            BatchActors batch,
            Job<Actor> job,
            Actor[] actors,
            int count,
            bool[] fires)
        {
            Batch = batch;
            Job = job;
            Actors = actors;
            Count = count;
            Checked = 0;
            SerialCount = 0;
            Skipped = false;
            Fires = fires;
            if (SerialActors.Length < count)
            {
                SerialActors =
                    new Actor[
                        Math.Max(
                            PerformanceSettings.SimulationBatchSize,
                            count)];
            }
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> job)
        {
            Batch = batch;
            Job = job;
            Actors = null;
            Count = 0;
            Checked = 0;
            SerialCount = 0;
            Skipped = true;
            Fires = null;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Count == 0)
            {
                return;
            }

            int serialCount = 0;
            Actor[] serialActors =
                SerialActors;
            for (int i = 0; i < Count; i++)
            {
                Actor actor = Actors[i];
                if (Fires == null ||
                    !CanSkipSafeGroundTileAction(
                        actor,
                        Fires))
                {
                    serialActors[serialCount++] =
                        actor;
                }
            }

            Checked = Count;
            SerialCount = serialCount;
        }

        internal void Reset()
        {
            if (SerialCount > 0)
            {
                Array.Clear(
                    SerialActors,
                    0,
                    SerialCount);
            }

            Batch = null;
            Job = null;
            Actors = null;
            Count = 0;
            Checked = 0;
            SerialCount = 0;
            Skipped = false;
            Fires = null;
        }
    }

    private sealed class PathMovementBatchWork
    {
        internal BatchActors Batch { get; private set; }
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal int Count { get; private set; }
        internal int Checked { get; private set; }
        internal bool Fallback { get; private set; }
        internal bool Skipped { get; private set; }
        internal PathMovementWorkEntry[] Entries { get; private set; } =
            Array.Empty<PathMovementWorkEntry>();

        internal void ConfigureParallel(
            BatchActors batch,
            Job<Actor> job,
            Actor[] actors,
            int count)
        {
            Batch = batch;
            Job = job;
            Actors = actors;
            Count = count;
            Checked = 0;
            Fallback = false;
            Skipped = false;
            if (Entries.Length < count)
            {
                Entries =
                    new PathMovementWorkEntry[
                        Math.Max(
                            PerformanceSettings.SimulationBatchSize,
                            count)];
            }
        }

        internal void ConfigureFallback(
            BatchActors batch,
            Job<Actor> job)
        {
            Batch = batch;
            Job = job;
            Actors = null;
            Count = 0;
            Checked = 0;
            Fallback = true;
            Skipped = false;
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> job)
        {
            Batch = batch;
            Job = job;
            Actors = null;
            Count = 0;
            Checked = 0;
            Fallback = false;
            Skipped = true;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Fallback ||
                Count == 0)
            {
                return;
            }

            int checkedActors = 0;
            for (int i = 0; i < Count; i++)
            {
                Actor actor = Actors[i];
                ref PathMovementWorkEntry entry =
                    ref Entries[i];
                entry.Prepared = default;
                if (actor._update_done ||
                    actor._beh_skip)
                {
                    entry.Kind =
                        PathMovementWorkKind.Inactive;
                    continue;
                }

                checkedActors++;
                PatchAboutPathfinding
                    .ParallelPathMovementResult result =
                    PatchAboutPathfinding
                        .TryRunParallelSafePathMovement(
                            actor,
                            out PatchAboutPathfinding
                                .PreparedPathMovement prepared);
                switch (result)
                {
                    case PatchAboutPathfinding
                        .ParallelPathMovementResult.NoPath:
                        entry.Kind =
                            PathMovementWorkKind.Retain;
                        break;
                    case PatchAboutPathfinding
                        .ParallelPathMovementResult.Handled:
                        actor.skipBehaviour();
                        entry.Kind =
                            PathMovementWorkKind.Handled;
                        break;
                    case PatchAboutPathfinding
                        .ParallelPathMovementResult.RequiresSerial:
                        entry.Prepared = prepared;
                        entry.Kind =
                            PathMovementWorkKind.RequiresSerial;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            Checked = checkedActors;
        }

        internal void Reset()
        {
            if (Count > 0)
            {
                Array.Clear(
                    Entries,
                    0,
                    Count);
            }

            Batch = null;
            Job = null;
            Actors = null;
            Count = 0;
            Checked = 0;
            Fallback = false;
            Skipped = false;
        }
    }

    private sealed class SmoothMovementBatchWork
    {
        internal Job<Actor> Job { get; private set; }
        internal Actor[] Actors { get; private set; }
        internal Actor[] SerialActors { get; private set; } =
            Array.Empty<Actor>();
        internal int Count { get; private set; }
        internal int Checked { get; private set; }
        internal int SerialCount { get; private set; }
        internal bool Skipped { get; private set; }
        internal float Elapsed { get; private set; }
        internal PatchAboutPathfinding
            .PreparedSmoothMovement[] Entries
        {
            get;
            private set;
        } = Array.Empty<PatchAboutPathfinding
            .PreparedSmoothMovement>();

        internal void Configure(
            BatchActors batch,
            Job<Actor> job,
            Actor[] actors,
            int count,
            float elapsed)
        {
            Job = job;
            Actors = actors;
            Count = count;
            Checked = 0;
            SerialCount = 0;
            Skipped = false;
            Elapsed = elapsed;
            if (Entries.Length < count)
            {
                int capacity = Math.Max(
                    PerformanceSettings.SimulationBatchSize,
                    count);
                SerialActors =
                    new Actor[capacity];
                Entries =
                    new PatchAboutPathfinding
                        .PreparedSmoothMovement[
                        capacity];
            }
        }

        internal void ConfigureSkipped(
            BatchActors batch,
            Job<Actor> job)
        {
            Job = job;
            Actors = null;
            Count = 0;
            Checked = 0;
            SerialCount = 0;
            Skipped = true;
            Elapsed = 0f;
        }

        internal void RunParallel()
        {
            if (Skipped ||
                Count == 0)
            {
                return;
            }

            int serialCount = 0;
            for (int i = 0; i < Count; i++)
            {
                PatchAboutPathfinding
                    .ParallelSmoothMovementResult result =
                    PatchAboutPathfinding
                        .TryRunParallelSafeSmoothMovement(
                            Actors[i],
                            Elapsed,
                            out PatchAboutPathfinding
                                .PreparedSmoothMovement
                                prepared);
                if (result ==
                    PatchAboutPathfinding
                        .ParallelSmoothMovementResult
                    .RequiresSerial)
                {
                    SerialActors[serialCount] =
                        Actors[i];
                    Entries[serialCount] =
                        prepared;
                    serialCount++;
                }
            }

            Checked = Count;
            SerialCount = serialCount;
        }

        internal void Reset()
        {
            if (SerialCount > 0)
            {
                Array.Clear(
                    SerialActors,
                    0,
                    SerialCount);
                Array.Clear(
                    Entries,
                    0,
                    SerialCount);
            }

            Job = null;
            Actors = null;
            Count = 0;
            Checked = 0;
            SerialCount = 0;
            Skipped = false;
            Elapsed = 0f;
        }
    }

    private sealed class SearchWorkItem
    {
        private readonly CandidateView candidateView = new();
        private readonly List<BaseSimObject>
            aggressionCandidates = new();
        private Actor actor;
        private List<BaseSimObject> primaryCandidates;
        private bool findClosest;
        private int randomOffset;
        private int originalAggressionCount;
        private bool hadAggressionTargets;
        private bool clearAggressionTargets;
        private BaseSimObject result;

        internal void Configure(
            Actor sourceActor,
            List<BaseSimObject> sourcePrimaryCandidates,
            bool sourceFindClosest,
            int sourceRandomOffset,
            int sourceOriginalAggressionCount)
        {
            actor = sourceActor;
            primaryCandidates = sourcePrimaryCandidates;
            findClosest = sourceFindClosest;
            randomOffset = sourceRandomOffset;
            originalAggressionCount = sourceOriginalAggressionCount;
            hadAggressionTargets = sourceOriginalAggressionCount > 0;
            clearAggressionTargets = false;
            result = null;
        }

        internal void Search()
        {
            if (primaryCandidates.Count > 0)
            {
                IEnumerable<BaseSimObject> candidates = primaryCandidates;
                if (!findClosest)
                {
                    candidateView.Configure(
                        primaryCandidates,
                        0,
                        primaryCandidates.Count,
                        randomOffset);
                    candidates = candidateView;
                }

                result = actor.checkObjectList(
                    candidates,
                    actor.asset.can_attack_buildings,
                    findClosest,
                    pIgnoreStunned: false,
                    int.MaxValue);
            }

            if (result != null || !hadAggressionTargets)
            {
                return;
            }

            aggressionCandidates.Clear();
            foreach (long targetId in
                     actor._aggression_targets)
            {
                Actor target =
                    World.world.units.get(targetId);
                if (!target.isRekt())
                {
                    aggressionCandidates.Add(target);
                }
            }

            if (aggressionCandidates.Count == 0)
            {
                clearAggressionTargets = true;
                return;
            }

            candidateView.Configure(
                aggressionCandidates,
                0,
                aggressionCandidates.Count,
                0);
            result = actor.checkObjectList(
                candidateView,
                actor.asset.can_attack_buildings,
                pFindClosest: true,
                pIgnoreStunned: true,
                30);
        }

        internal void Commit()
        {
            // 搜索可能跨过渲染帧，提交前不能用旧结果覆盖期间产生的新战斗状态。
            if (actor.isRekt() || actor.has_attack_target)
            {
                return;
            }

            if (result != null &&
                (result.isRekt() ||
                 !actor.canAttackTarget(
                     result,
                     pCheckForFactions: true,
                     pAttackBuildings: actor.asset.can_attack_buildings)))
            {
                result = null;
            }

            if (result == null)
            {
                if (clearAggressionTargets &&
                    actor._aggression_targets.Count == originalAggressionCount)
                {
                    actor._aggression_targets.Clear();
                }

                return;
            }

            actor.startFightingWith(result);
            actor.stopMovement();
            actor.skipBehaviour();
        }

        internal void Reset()
        {
            actor = null;
            primaryCandidates = null;
            aggressionCandidates.Clear();
            result = null;
            candidateView.ResetSource();
        }
    }

    private sealed class CandidateView :
        IEnumerable<BaseSimObject>,
        IEnumerator<BaseSimObject>
    {
        private List<BaseSimObject> source;
        private int start;
        private int count;
        private int offset;
        private int index;

        public BaseSimObject Current =>
            source[start + (index + offset) % count];

        object IEnumerator.Current => Current;

        internal void Configure(
            List<BaseSimObject> sourceList,
            int sourceStart,
            int sourceCount,
            int sourceOffset)
        {
            source = sourceList;
            start = sourceStart;
            count = sourceCount;
            offset = sourceOffset;
            index = -1;
        }

        public IEnumerator<BaseSimObject> GetEnumerator()
        {
            index = -1;
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool MoveNext()
        {
            return ++index < count;
        }

        public void Reset()
        {
            index = -1;
        }

        public void Dispose()
        {
        }

        internal void ResetSource()
        {
            source = null;
            start = 0;
            count = 0;
            offset = 0;
            index = -1;
        }
    }
}
