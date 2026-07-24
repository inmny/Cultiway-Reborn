using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Patch;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeActorPostRunner : ICooperativeBatchPostRunner<BatchActors, Actor>
{
    private const string EnemySearchJobId = "b3_findEnemyTarget";

    private readonly ActorTileActionProfiler tileActionProfiler = new();
    private readonly Action<int> searchWorkItemAction;

    private enum PostStage
    {
        Idle,
        BeforeEnemySearch,
        PrepareEnemySearch,
        SearchEnemies,
        CommitEnemySearch,
        AfterEnemySearch,
        Finish
    }

    private readonly List<BaseSimObject> aggressionCandidates = new();
    private SearchWorkItem[] workItems = Array.Empty<SearchWorkItem>();
    private List<BatchActors> batches;
    private ParallelOptions parallelOptions;
    private PostStage stage;
    private float elapsed;
    private int enemySearchJobIndex;
    private int batchIndex;
    private int workIndex;
    private int workCount;
    private int workGroupSize;

    internal CooperativeActorPostRunner()
    {
        searchWorkItemAction = SearchWorkItemAt;
    }

    public void Start(
        List<BatchActors> activeBatches,
        float cycleElapsed,
        ParallelOptions cycleParallelOptions)
    {
        batches = activeBatches;
        elapsed = cycleElapsed;
        parallelOptions = cycleParallelOptions;
        workGroupSize = Math.Max(1, PerformanceSettings.ForegroundParallelism * 4);
        batchIndex = 0;
        workIndex = 0;
        workCount = 0;
        aggressionCandidates.Clear();
        tileActionProfiler.Start(batches.Count);

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
                ? phasePrefix + ".post.b3.search.batch_group.0"
                : phasePrefix + ".post.after_b3.batch.0";
        }

        if (stage == PostStage.SearchEnemies && workIndex >= workCount)
        {
            return workCount > 0
                ? phasePrefix + ".post.b3.commit.batch_group.0"
                : phasePrefix + ".post.after_b3.batch.0";
        }

        if (stage == PostStage.CommitEnemySearch && workIndex >= workCount)
        {
            return phasePrefix + ".post.after_b3.batch.0";
        }

        if (stage == PostStage.AfterEnemySearch &&
            batchIndex >= batches.Count)
        {
            return phasePrefix + ".post.finish";
        }

        return stage switch
        {
            PostStage.BeforeEnemySearch =>
                phasePrefix + ".post.before_b3.batch." + batchIndex,
            PostStage.PrepareEnemySearch =>
                phasePrefix + ".post.b3.prepare.batch." + batchIndex,
            PostStage.SearchEnemies =>
                phasePrefix + ".post.b3.search.batch_group." + workIndex,
            PostStage.CommitEnemySearch =>
                phasePrefix + ".post.b3.commit.batch_group." + workIndex,
            PostStage.AfterEnemySearch =>
                phasePrefix + ".post.after_b3.batch." + batchIndex,
            PostStage.Finish => phasePrefix + ".post.finish",
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
                    stage = PostStage.PrepareEnemySearch;
                    continue;
                case PostStage.PrepareEnemySearch:
                    if (TryPrepareNextBatch())
                    {
                        return false;
                    }

                    workIndex = 0;
                    stage = PostStage.SearchEnemies;
                    continue;
                case PostStage.SearchEnemies:
                    if (TrySearchNextGroup())
                    {
                        return false;
                    }

                    workIndex = 0;
                    stage = PostStage.CommitEnemySearch;
                    continue;
                case PostStage.CommitEnemySearch:
                    if (TryCommitNextGroup())
                    {
                        return false;
                    }

                    batchIndex = 0;
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
        tileActionProfiler.Abort();
        ResetCycleReferences();
        stage = PostStage.Idle;
    }

    private bool TryRunNextPostRange(int startJobIndex, int endJobIndex)
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        int currentBatchIndex = batchIndex;
        BatchActors batch = batches[batchIndex++];
        List<Job<Actor>> jobs = batch.jobs_post;
        int end = Math.Min(endJobIndex, jobs.Count);
        for (int i = startJobIndex; i < end; i++)
        {
            RunPostJob(batch, jobs[i], currentBatchIndex);
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

        double startedAt = Time.realtimeSinceStartupAsDouble;
        bool profileTileAction =
            tileActionProfiler.Active &&
            ReferenceEquals(job.container, batch.c_main_tile_action);
        if (!profileTileAction ||
            !tileActionProfiler.TryRunSampledJob(batch, job, currentBatchIndex))
        {
            job.job_updater();
        }

        if (profileTileAction)
        {
            tileActionProfiler.RecordFullCalls(job.container);
        }

        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(0, job.random_tick_skips);
        }

        job.time_benchmark += Time.realtimeSinceStartupAsDouble - startedAt;
        job.counter += batch._cur_container.Count;
    }

    private bool TryPrepareNextBatch()
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        BatchActors batch = batches[batchIndex++];
        Job<Actor> job = batch.jobs_post[enemySearchJobIndex];
        batch._elapsed = elapsed;
        batch._cur_container = job.container;
        if (job.current_skips > 0)
        {
            job.current_skips--;
            return true;
        }

        long startedAt = StartBenchmarkMeasurement();
        PrepareEnemySearchBatch(batch, job.container);
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
        ObjectContainer<Actor> container)
    {
        if (container.Count == 0 && !container.isDirtyContainer())
        {
            return;
        }

        container.checkAddRemove();
        Actor[] array = container.getFastSimpleArray();
        int count = container.Count;
        batch._array = array;
        batch._count = count;
        if (World.world.isPaused())
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            PrepareEnemySearch(array[i]);
        }
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
            aggressionSourceCount > 0,
            applyBackoff);
    }

    private bool TrySearchNextGroup()
    {
        if (workIndex >= workCount)
        {
            return false;
        }

        int startIndex = workIndex;
        int endIndex = Math.Min(workCount, startIndex + workGroupSize);
        long startedAt = StartBenchmarkMeasurement();
        if (endIndex - startIndex > 1)
        {
            Parallel.For(startIndex, endIndex, parallelOptions, searchWorkItemAction);
        }
        else
        {
            SearchWorkItemAt(startIndex);
        }

        workIndex = endIndex;
        RecordBenchmarkMeasurement(
            "b3_findEnemyTarget.search_parallel",
            startedAt,
            endIndex - startIndex);
        return true;
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

    private void ResetCycleReferences()
    {
        for (int i = 0; i < workCount; i++)
        {
            workItems[i].Reset();
        }

        workCount = 0;
        workIndex = 0;
        batchIndex = 0;
        aggressionCandidates.Clear();
        batches = null;
        parallelOptions = null;
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

    private sealed class SearchWorkItem
    {
        private readonly CandidateView candidateView = new();
        private Actor actor;
        private List<BaseSimObject> primaryCandidates;
        private bool findClosest;
        private int randomOffset;
        private int aggressionStart;
        private int aggressionCount;
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
            bool sourceHadAggressionTargets,
            bool sourceApplyBackoff)
        {
            actor = sourceActor;
            primaryCandidates = sourcePrimaryCandidates;
            findClosest = sourceFindClosest;
            randomOffset = sourceRandomOffset;
            aggressionStart = sourceAggressionStart;
            aggressionCount = sourceAggressionCount;
            hadAggressionTargets = sourceHadAggressionTargets;
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
            if (result == null)
            {
                if (clearAggressionTargets)
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
