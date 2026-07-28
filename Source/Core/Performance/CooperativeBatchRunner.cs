using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeBatchRunner<TBatch, TObject> where TBatch : Batch<TObject>, new()
{
    private enum RunnerStage
    {
        Idle,
        Pre,
        ClearParallelResults,
        Parallel,
        ApplyParallelResults,
        Post,
        Finish
    }

    private readonly List<TBatch> batches = new();
    private readonly string phasePrefix;
    private readonly ICooperativeBatchPostRunner<TBatch, TObject> postRunner;
    private readonly Action<int> runCurrentParallelJob;
    private int[] activeParallelBatchIndices = Array.Empty<int>();
    private JobManagerBase<TBatch, TObject> manager;
    private RunnerStage stage;
    private float elapsed;
    private int batchIndex;
    private int parallelJobIndex;
    private int activeParallelBatchCount;
    private bool parallelEnabled;
    private int parallelGroupSize;
    private bool collectJobBenchmarks;
    private bool useCustomPostRunner;

    public CooperativeBatchRunner(
        string phasePrefix,
        ICooperativeBatchPostRunner<TBatch, TObject> postRunner = null)
    {
        this.phasePrefix = phasePrefix;
        this.postRunner = postRunner;
        runCurrentParallelJob = RunCurrentParallelJob;
    }

    public bool Active => stage != RunnerStage.Idle;
    public bool WaitingForBackgroundWork =>
        stage == RunnerStage.Post &&
        useCustomPostRunner &&
        postRunner.WaitingForBackgroundWork;
    public bool IsBackgroundWorkCompleted =>
        WaitingForBackgroundWork &&
        postRunner.IsBackgroundWorkCompleted;

    public bool TryJoinBackgroundWork(double maximumMilliseconds)
    {
        return !WaitingForBackgroundWork ||
               postRunner.TryJoinBackgroundWork(maximumMilliseconds);
    }

    public void Start(
        JobManagerBase<TBatch, TObject> jobManager,
        IEnumerable<TBatch> activeBatches,
        float cycleElapsed,
        ParallelOptions cycleParallelOptions,
        Comparison<TBatch> comparison = null)
    {
        manager = jobManager;
        elapsed = cycleElapsed;
        parallelEnabled = Config.parallel_jobs_updater;
        parallelGroupSize = parallelEnabled
            ? Math.Max(1, PerformanceSettings.ForegroundParallelism * 4)
            : 1;
        useCustomPostRunner = parallelEnabled && postRunner != null;
        if (parallelEnabled && cycleParallelOptions == null)
        {
            throw new InvalidOperationException("并行批处理缺少 ParallelOptions");
        }

        batches.Clear();
        batches.AddRange(activeBatches);
        if (comparison != null)
        {
            batches.Sort(comparison);
        }

        collectJobBenchmarks = SimulationTickBenchmark.IsCapturing;
        if (collectJobBenchmarks)
        {
            manager.clearJobBenchmarks();
        }

        batchIndex = 0;
        parallelJobIndex = 0;
        activeParallelBatchCount = 0;
        stage = RunnerStage.Pre;
    }

    public string GetNextPhaseName()
    {
        if (!Active)
        {
            return phasePrefix + ".idle";
        }

        if (stage == RunnerStage.Parallel)
        {
            if (parallelEnabled)
            {
                int nextJobIndex = parallelJobIndex;
                int nextBatchIndex = batchIndex;
                int jobCount = batches.Count == 0
                    ? 0
                    : batches[0].jobs_parallel.Count;
                while (nextJobIndex < jobCount &&
                       nextBatchIndex >= batches.Count)
                {
                    nextJobIndex++;
                    nextBatchIndex = 0;
                }

                if (nextJobIndex < jobCount)
                {
                    Job<TObject> job = batches[0].jobs_parallel[nextJobIndex];
                    return phasePrefix +
                           ".parallel." +
                           job.id +
                           ".batch_group." +
                           nextBatchIndex;
                }
            }
            else if (batchIndex < batches.Count)
            {
                return phasePrefix + ".parallel.batch." + batchIndex;
            }

            return phasePrefix + ".applyparallelresults";
        }

        if (stage is RunnerStage.Pre or RunnerStage.Post)
        {
            if (stage == RunnerStage.Post && useCustomPostRunner)
            {
                return postRunner.GetNextPhaseName(phasePrefix);
            }

            int nextBatchIndex = FindNextMainThreadBatchIndex(stage);
            if (nextBatchIndex >= 0)
            {
                return phasePrefix +
                       "." +
                       stage.ToString().ToLowerInvariant() +
                       ".batch." +
                       nextBatchIndex;
            }

            return stage == RunnerStage.Pre
                ? phasePrefix + ".clearparallelresults"
                : phasePrefix + ".finish";
        }

        return phasePrefix + "." + stage.ToString().ToLowerInvariant();
    }

    public bool Step()
    {
        while (true)
        {
            switch (stage)
            {
                case RunnerStage.Idle:
                    return true;
                case RunnerStage.Pre:
                    if (TryRunNextMainThreadBatch(RunnerStage.Pre))
                    {
                        return false;
                    }

                    stage = RunnerStage.ClearParallelResults;
                    batchIndex = 0;
                    parallelJobIndex = 0;
                    continue;
                case RunnerStage.ClearParallelResults:
                    manager.clearParallelResults();
                    stage = RunnerStage.Parallel;
                    return false;
                case RunnerStage.Parallel:
                    if (parallelEnabled
                            ? TryRunNextParallelJobGroup()
                            : TryRunNextParallelBatch())
                    {
                        return false;
                    }

                    stage = RunnerStage.ApplyParallelResults;
                    batchIndex = 0;
                    continue;
                case RunnerStage.ApplyParallelResults:
                    manager.applyParallelResults();
                    stage = RunnerStage.Post;
                    if (useCustomPostRunner)
                    {
                        postRunner.Start(batches, elapsed);
                    }

                    return false;
                case RunnerStage.Post:
                    if (useCustomPostRunner
                            ? !postRunner.Step()
                            : TryRunNextMainThreadBatch(RunnerStage.Post))
                    {
                        return false;
                    }

                    stage = RunnerStage.Finish;
                    continue;
                case RunnerStage.Finish:
                    if (collectJobBenchmarks)
                    {
                        SimulationTickBenchmark.RecordBatchJobs<TBatch, TObject>(
                            manager.benchmark_id,
                            batches);
                    }

                    batches.Clear();
                    manager = null;
                    parallelEnabled = false;
                    parallelGroupSize = 0;
                    activeParallelBatchCount = 0;
                    collectJobBenchmarks = false;
                    useCustomPostRunner = false;
                    stage = RunnerStage.Idle;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public void Abort()
    {
        batches.Clear();
        postRunner?.Abort();
        manager = null;
        parallelEnabled = false;
        parallelGroupSize = 0;
        activeParallelBatchCount = 0;
        collectJobBenchmarks = false;
        useCustomPostRunner = false;
        stage = RunnerStage.Idle;
        batchIndex = 0;
        parallelJobIndex = 0;
    }

    public void WaitForBackgroundWork()
    {
        if (WaitingForBackgroundWork)
        {
            postRunner.WaitForBackgroundWork();
        }
    }

    private bool TryRunNextMainThreadBatch(RunnerStage jobStage)
    {
        while (batchIndex < batches.Count)
        {
            TBatch batch = batches[batchIndex++];
            List<Job<TObject>> jobs = GetJobs(batch, jobStage);
            if (jobs.Count == 0)
            {
                continue;
            }

            // 原版按 batch 顺序完整执行全部主线程 job；batch 本身已经是可跨帧的安全边界。
            if (jobStage == RunnerStage.Pre)
            {
                batch.updateJobsPre(elapsed);
            }
            else
            {
                batch.updateJobsPost(elapsed);
            }

            return true;
        }

        return false;
    }

    private bool TryRunNextParallelJobGroup()
    {
        int jobCount = batches.Count == 0 ? 0 : batches[0].jobs_parallel.Count;
        while (parallelJobIndex < jobCount)
        {
            if (batchIndex >= batches.Count)
            {
                parallelJobIndex++;
                batchIndex = 0;
                continue;
            }

            int scannedCount = Math.Min(parallelGroupSize, batches.Count - batchIndex);
            EnsureActiveParallelBatchCapacity(scannedCount);
            activeParallelBatchCount = 0;
            int endIndex = batchIndex + scannedCount;
            for (; batchIndex < endIndex; batchIndex++)
            {
                if (HasParallelJobWork(batchIndex, parallelJobIndex))
                {
                    activeParallelBatchIndices[activeParallelBatchCount++] = batchIndex;
                }
            }

            if (activeParallelBatchCount > 1)
            {
                // 同一 job 的 batch 由长驻 worker 动态领取；返回后才进入下一 job，
                // 因而保留原版 job 顺序与跨 job 屏障。
                SimulationWorkerPool.Instance.RunIndexed(
                    0,
                    activeParallelBatchCount,
                    runCurrentParallelJob);
            }
            else if (activeParallelBatchCount == 1)
            {
                RunParallelJob(activeParallelBatchIndices[0], parallelJobIndex);
            }

            return true;
        }

        return false;
    }

    private bool HasParallelJobWork(int batchListIndex, int jobListIndex)
    {
        Job<TObject> job = batches[batchListIndex].jobs_parallel[jobListIndex];
        ObjectContainer<TObject> container = job.container;
        return container == null ||
               container.Count > 0 ||
               container.isDirtyContainer();
    }

    private void EnsureActiveParallelBatchCapacity(int capacity)
    {
        if (activeParallelBatchIndices.Length < capacity)
        {
            Array.Resize(ref activeParallelBatchIndices, capacity);
        }
    }

    private bool TryRunNextParallelBatch()
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        TBatch batch = batches[batchIndex++];
        batch._elapsed = elapsed;
        batch.updateJobsParallel(elapsed);
        return true;
    }

    private void RunParallelJob(int batchListIndex, int jobListIndex)
    {
        TBatch batch = batches[batchListIndex];
        Job<TObject> job = batch.jobs_parallel[jobListIndex];
        batch._elapsed = elapsed;
        batch._cur_container = job.container;
        job.job_updater();
    }

    private void RunCurrentParallelJob(int activeBatchIndex)
    {
        RunParallelJob(
            activeParallelBatchIndices[activeBatchIndex],
            parallelJobIndex);
    }

    private static List<Job<TObject>> GetJobs(TBatch batch, RunnerStage jobStage)
    {
        return jobStage switch
        {
            RunnerStage.Pre => batch.jobs_pre,
            RunnerStage.Parallel => batch.jobs_parallel,
            RunnerStage.Post => batch.jobs_post,
            _ => throw new ArgumentOutOfRangeException(nameof(jobStage))
        };
    }

    private int FindNextMainThreadBatchIndex(RunnerStage jobStage)
    {
        for (int i = batchIndex; i < batches.Count; i++)
        {
            if (GetJobs(batches[i], jobStage).Count > 0)
            {
                return i;
            }
        }

        return -1;
    }
}
