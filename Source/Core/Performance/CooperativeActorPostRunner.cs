using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Cultiway.Const;
using Cultiway.Patch;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeActorPostRunner : ICooperativeBatchPostRunner<BatchActors, Actor>
{
    private const string EnemySearchJobId = "b3_findEnemyTarget";
    private const string TileActionJobId = "u5_curTileAction";
    private const string UpdateTimersJobId = "u8_checkUpdateTimers";
    private const string UnderForceJobId = "b1_checkUnderForce";
    private const string TaskVerifierJobId = "b4_checkTaskVerifier";
    private const string PathMovementJobId = "b5_checkPathMovement";
    private const string NaturalDeathJobId = "b55_update_natural_death";
    private const string UpdateAiJobId = "b6_update_ai";

    private readonly ActorTileActionProfiler tileActionProfiler = new();
    private readonly Action<int> searchWorkItemAction;

    private enum PostStage
    {
        Idle,
        BeforeEnemySearch,
        PrepareEnemySearch,
        ScheduleEnemySearch,
        AwaitEnemySearch,
        CommitEnemySearch,
        AfterEnemySearch,
        Finish
    }

    private readonly List<BaseSimObject> aggressionCandidates = new();
    private SearchWorkItem[] workItems = Array.Empty<SearchWorkItem>();
    private Actor[][] activeBehaviorActorsByBatch =
        Array.Empty<Actor[]>();
    private int[] activeBehaviorActorCounts =
        Array.Empty<int>();
    private bool[] activeBehaviorPartitionsValid =
        Array.Empty<bool>();
    private List<BatchActors> batches;
    private PostStage stage;
    private float elapsed;
    private int enemySearchJobIndex;
    private int batchIndex;
    private int postJobIndex;
    private int workIndex;
    private int workCount;
    private int workGroupSize;
    private bool splitPostJobs;
    private SimulationWorkerPool.WorkTicket searchTicket;
    private long searchScheduleStartedAt;
    private long searchScheduleCompletedAt;

    internal CooperativeActorPostRunner()
    {
        searchWorkItemAction = SearchWorkItemAt;
    }

    public void Start(
        List<BatchActors> activeBatches,
        float cycleElapsed)
    {
        batches = activeBatches;
        elapsed = cycleElapsed;
        workGroupSize = Math.Max(1, PerformanceSettings.ForegroundParallelism * 4);
        batchIndex = 0;
        postJobIndex = 0;
        workIndex = 0;
        workCount = 0;
        splitPostJobs = SimulationTickBenchmark.IsCapturing;
        searchTicket = default;
        searchScheduleStartedAt = 0L;
        searchScheduleCompletedAt = 0L;
        aggressionCandidates.Clear();
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

        stage = PostStage.BeforeEnemySearch;
    }

    public bool WaitingForBackgroundWork =>
        stage == PostStage.AwaitEnemySearch &&
        searchTicket.IsValid;

    public bool IsBackgroundWorkCompleted =>
        WaitingForBackgroundWork &&
        SimulationWorkerPool.Instance.IsCompleted(searchTicket);

    public bool TryJoinBackgroundWork(double maximumMilliseconds)
    {
        return !WaitingForBackgroundWork ||
               SimulationWorkerPool.Instance.TryWait(searchTicket, maximumMilliseconds);
    }

    public void WaitForBackgroundWork()
    {
        if (WaitingForBackgroundWork)
        {
            SimulationWorkerPool.Instance.Wait(searchTicket);
        }
    }

    public string GetNextPhaseName(string phasePrefix)
    {
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
                    int.MaxValue,
                    "after_b3",
                    restartRange: true);
        }

        if (stage == PostStage.CommitEnemySearch && workIndex >= workCount)
        {
            return GetNextPostRangePhaseName(
                phasePrefix,
                enemySearchJobIndex + 1,
                int.MaxValue,
                "after_b3",
                restartRange: true);
        }

        if (stage == PostStage.AfterEnemySearch &&
            batchIndex >= batches.Count)
        {
            return DeferredPathRequestBatch.HasPendingRequests
                ? phasePrefix + ".post.path_requests.flush"
                : phasePrefix + ".post.finish";
        }

        return stage switch
        {
            PostStage.BeforeEnemySearch =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    0,
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
            PostStage.AfterEnemySearch =>
                GetNextPostRangePhaseName(
                    phasePrefix,
                    enemySearchJobIndex + 1,
                    int.MaxValue,
                    "after_b3"),
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
                case PostStage.BeforeEnemySearch:
                    if (TryRunNextPostRange(0, enemySearchJobIndex))
                    {
                        return false;
                    }

                    batchIndex = 0;
                    postJobIndex = 0;
                    stage = PostStage.PrepareEnemySearch;
                    continue;
                case PostStage.PrepareEnemySearch:
                    if (TryPrepareNextBatch())
                    {
                        return false;
                    }

                    workIndex = 0;
                    if (workCount == 0)
                    {
                        batchIndex = 0;
                        postJobIndex = enemySearchJobIndex + 1;
                        stage = PostStage.AfterEnemySearch;
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
                    stage = PostStage.AfterEnemySearch;
                    continue;
                case PostStage.AfterEnemySearch:
                    if (TryRunNextPostRange(enemySearchJobIndex + 1, int.MaxValue))
                    {
                        return false;
                    }

                    stage = PostStage.Finish;
                    continue;
                case PostStage.Finish:
                    DeferredPathRequestBatch.CompleteCycle();
                    tileActionProfiler.Finish();
                    ResetCycleReferences();
                    stage = PostStage.Idle;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Abort()
    {
        if (searchTicket.IsValid)
        {
            SimulationWorkerPool.Instance.WaitAndDiscard(searchTicket);
            searchTicket = default;
        }

        DeferredPathRequestBatch.AbortCycle();
        tileActionProfiler.Abort();
        ClearActiveBehaviorPartitions();
        ResetCycleReferences();
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

        int aggregateBatchIndex = batchIndex;
        BatchActors aggregateBatch = batches[batchIndex++];
        List<Job<Actor>> aggregateJobs = aggregateBatch.jobs_post;
        int aggregateEnd = Math.Min(endJobIndex, aggregateJobs.Count);
        for (int i = startJobIndex; i < aggregateEnd; i++)
        {
            RunPostJob(aggregateBatch, aggregateJobs[i], aggregateBatchIndex);
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

        bool completed = false;
        try
        {
            if (job.id.Equals(
                    UpdateTimersJobId,
                    StringComparison.Ordinal))
            {
                RunUpdateTimersJob(
                    job.container,
                    currentBatchIndex);
            }
            else if (job.id.Equals(
                    PathMovementJobId,
                    StringComparison.Ordinal))
            {
                if (!TryRunActiveBehaviorJob(
                        job,
                        currentBatchIndex))
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
                         currentBatchIndex))
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
            job.counter += batch._cur_container.Count;
        }
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
                ref activeBehaviorPartitionsValid,
                batchCount);
        }

        Array.Clear(
            activeBehaviorActorCounts,
            0,
            batchCount);
        Array.Clear(
            activeBehaviorPartitionsValid,
            0,
            batchCount);
    }

    private void RunUpdateTimersJob(
        ObjectContainer<Actor> container,
        int currentBatchIndex)
    {
        activeBehaviorActorCounts[currentBatchIndex] = 0;
        activeBehaviorPartitionsValid[currentBatchIndex] = false;
        if (container.Count == 0 &&
            !container.isDirtyContainer())
        {
            activeBehaviorPartitionsValid[currentBatchIndex] = true;
            return;
        }

        container.checkAddRemove();
        if (World.world.isPaused())
        {
            return;
        }

        Actor[] actors = container.getFastSimpleArray();
        int count = container.Count;
        Actor[] activeActors =
            activeBehaviorActorsByBatch[currentBatchIndex];
        if (activeActors == null ||
            activeActors.Length < count)
        {
            int capacity = Math.Max(
                PerformanceSettings.SimulationBatchSize,
                count);
            activeActors = new Actor[capacity];
            activeBehaviorActorsByBatch[currentBatchIndex] =
                activeActors;
        }

        int activeCount = 0;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.u8_checkUpdateTimers(elapsed);
            if (!actor._update_done)
            {
                activeActors[activeCount++] = actor;
            }
        }

        activeBehaviorActorCounts[currentBatchIndex] =
            activeCount;
        activeBehaviorPartitionsValid[currentBatchIndex] =
            true;
    }

    private bool TryRunActiveBehaviorJob(
        Job<Actor> job,
        int currentBatchIndex)
    {
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
                for (int i = 0; i < count; i++)
                {
                    actors[i].b1_checkUnderForce(elapsed);
                }

                return true;
            case TaskVerifierJobId:
                for (int i = 0; i < count; i++)
                {
                    actors[i].b4_checkTaskVerifier(elapsed);
                }

                return true;
            case PathMovementJobId:
                RunPathMovementJob(actors, count);
                return true;
            case NaturalDeathJobId:
                for (int i = 0; i < count; i++)
                {
                    actors[i].b55_updateNaturalDeaths(elapsed);
                }

                return true;
            case UpdateAiJobId:
                for (int i = 0; i < count; i++)
                {
                    actors[i].b6_updateAI(elapsed);
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
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (CanSkipSafeGroundTileAction(actor))
            {
                continue;
            }

            actor.u5_curTileAction();
        }
    }

    private static bool CanSkipSafeGroundTileAction(Actor actor)
    {
        if (actor._update_done ||
            actor.position_height > 0f ||
            actor.isFlying())
        {
            return true;
        }

        WorldTile tile = actor.current_tile;
        TileTypeBase type = tile.Type;
        if (type.block ||
            !type.ground ||
            tile.isOnFire() ||
            actor.asset.is_boat ||
            type.damage_units)
        {
            return false;
        }

        if (actor.isWaterCreature() &&
            !actor.asset.force_land_creature)
        {
            return false;
        }

        Building building = tile.building;
        return building == null ||
               !building.asset.has_step_action;
    }

    private void RunPathMovementJob(
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

        RunPathMovementJob(
            container.getFastSimpleArray(),
            container.Count);
    }

    private static void RunPathMovementJob(
        Actor[] actors,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (actor._update_done ||
                actor._beh_skip ||
                !PatchAboutPathfinding
                    .TryUpdateActivePathMovement(actor))
            {
                continue;
            }

            actor.skipBehaviour();
        }
    }

    private bool TryPrepareNextBatch()
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        int currentBatchIndex = batchIndex++;
        BatchActors batch = batches[currentBatchIndex];
        Job<Actor> job = batch.jobs_post[enemySearchJobIndex];
        batch._elapsed = elapsed;
        batch._cur_container = job.container;
        if (job.current_skips > 0)
        {
            job.current_skips--;
            return true;
        }

        long startedAt = StartBenchmarkMeasurement();
        PrepareEnemySearchBatch(
            batch,
            job.container,
            currentBatchIndex);
        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(0, job.random_tick_skips);
        }

        int actorsChecked = job.container.Count;
        job.counter += actorsChecked;
        RecordBenchmarkMeasurement(
            "b3_findEnemyTarget.prepare",
            startedAt,
            actorsChecked);
        return true;
    }

    private void PrepareEnemySearchBatch(
        BatchActors batch,
        ObjectContainer<Actor> container,
        int currentBatchIndex)
    {
        if (container.Count == 0 && !container.isDirtyContainer())
        {
            return;
        }

        if (container.isDirtyContainer())
        {
            activeBehaviorPartitionsValid[currentBatchIndex] =
                false;
        }

        container.checkAddRemove();
        Actor[] containerActors =
            container.getFastSimpleArray();
        int containerCount = container.Count;
        batch._array = containerActors;
        batch._count = containerCount;
        if (World.world.isPaused())
        {
            return;
        }

        Actor[] array = containerActors;
        int count = containerCount;
        if (activeBehaviorPartitionsValid[currentBatchIndex])
        {
            array =
                activeBehaviorActorsByBatch[currentBatchIndex];
            count =
                activeBehaviorActorCounts[currentBatchIndex];
        }

        for (int i = 0; i < count; i++)
        {
            PrepareEnemySearch(array[i]);
        }
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
            activeBehaviorPartitionsValid,
            0,
            activeBehaviorPartitionsValid.Length);
    }

    private void PrepareEnemySearch(Actor actor)
    {
        bool applyBackoff = PatchActor.ShouldBackoffEmptyEnemySearch(actor);
        if (actor._update_done || actor._beh_skip)
        {
            return;
        }

        if (!actor.isAllowedToLookForEnemies() ||
            actor.isInWaterAndCantAttack() ||
            actor._has_status_strange_urge)
        {
            return;
        }

        if (actor.has_attack_target)
        {
            if (!actor.hasTask() || !actor.ai.task.in_combat)
            {
                actor.setTask("fighting", pClean: true, pCleanJob: true);
            }

            return;
        }

        if (actor._timeout_targets > 0f)
        {
            return;
        }

        actor._timeout_targets = 0.1f + Randy.randomFloat(0f, 1f);
        EnemyFinderData enemyData = EnemiesFinder.findEnemiesFrom(actor.current_tile, actor.kingdom);
        List<BaseSimObject> primaryCandidates = enemyData.list;
        bool findClosest = true;
        int randomOffset = 0;
        if (primaryCandidates.Count > 50)
        {
            findClosest = Randy.randomChance(0.6f);
            if (!findClosest)
            {
                randomOffset = Randy.randomInt(0, primaryCandidates.Count);
            }
        }

        int aggressionStart = aggressionCandidates.Count;
        int aggressionSourceCount = actor._aggression_targets.Count;
        if (aggressionSourceCount > 0)
        {
            foreach (long targetId in actor._aggression_targets)
            {
                Actor target = World.world.units.get(targetId);
                if (!target.isRekt())
                {
                    aggressionCandidates.Add(target);
                }
            }
        }

        SearchWorkItem item = RentWorkItem();
        item.Configure(
            actor,
            primaryCandidates,
            findClosest,
            randomOffset,
            aggressionStart,
            aggressionCandidates.Count - aggressionStart,
            aggressionSourceCount,
            applyBackoff);
    }

    private void SearchWorkItemAt(int index)
    {
        workItems[index].Search(aggressionCandidates);
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

    private void ResetCycleReferences()
    {
        for (int i = 0; i < workCount; i++)
        {
            workItems[i].Reset();
        }

        workCount = 0;
        workIndex = 0;
        batchIndex = 0;
        postJobIndex = 0;
        aggressionCandidates.Clear();
        batches = null;
        splitPostJobs = false;
        searchTicket = default;
        searchScheduleStartedAt = 0L;
        searchScheduleCompletedAt = 0L;
    }

    private static int FindEnemySearchJobIndex(List<Job<Actor>> jobs)
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            if (jobs[i].id.Equals(EnemySearchJobId, StringComparison.Ordinal))
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

    private sealed class SearchWorkItem
    {
        private readonly CandidateView candidateView = new();
        private Actor actor;
        private List<BaseSimObject> primaryCandidates;
        private bool findClosest;
        private int randomOffset;
        private int aggressionStart;
        private int aggressionCount;
        private int originalAggressionCount;
        private bool hadAggressionTargets;
        private bool applyBackoff;
        private bool clearAggressionTargets;
        private BaseSimObject result;

        internal void Configure(
            Actor sourceActor,
            List<BaseSimObject> sourcePrimaryCandidates,
            bool sourceFindClosest,
            int sourceRandomOffset,
            int sourceAggressionStart,
            int sourceAggressionCount,
            int sourceOriginalAggressionCount,
            bool sourceApplyBackoff)
        {
            actor = sourceActor;
            primaryCandidates = sourcePrimaryCandidates;
            findClosest = sourceFindClosest;
            randomOffset = sourceRandomOffset;
            aggressionStart = sourceAggressionStart;
            aggressionCount = sourceAggressionCount;
            originalAggressionCount = sourceOriginalAggressionCount;
            hadAggressionTargets = sourceOriginalAggressionCount > 0;
            applyBackoff = sourceApplyBackoff;
            clearAggressionTargets = false;
            result = null;
        }

        internal void Search(List<BaseSimObject> allAggressionCandidates)
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

            if (aggressionCount == 0)
            {
                clearAggressionTargets = true;
                return;
            }

            candidateView.Configure(
                allAggressionCandidates,
                aggressionStart,
                aggressionCount,
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

                if (applyBackoff)
                {
                    PatchActor.ApplyEnemySearchBackoff(actor);
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
