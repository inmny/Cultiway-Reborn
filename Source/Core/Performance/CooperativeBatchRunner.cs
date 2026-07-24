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
    private JobManagerBase<TBatch, TObject> manager;
    private RunnerStage stage;
    private float elapsed;
    private int batchIndex;
    private bool parallelEnabled;
    private int parallelGroupSize;
    private ParallelOptions parallelOptions;

    public CooperativeBatchRunner(string phasePrefix)
    {
        this.phasePrefix = phasePrefix;
    }

    public bool Active => stage != RunnerStage.Idle;

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
        parallelGroupSize = parallelEnabled ? PerformanceSettings.ForegroundParallelism : 1;
        parallelOptions = cycleParallelOptions;
        if (parallelEnabled && parallelOptions == null)
        {
            throw new InvalidOperationException("并行批处理缺少 ParallelOptions");
        }

        batches.Clear();
        batches.AddRange(activeBatches);
        if (comparison != null)
        {
            batches.Sort(comparison);
        }

        manager.clearJobBenchmarks();
        batchIndex = 0;
        stage = RunnerStage.Pre;
    }

    public string GetNextPhaseName()
    {
        if (!Active)
        {
            return phasePrefix + ".idle";
        }

        if (stage == RunnerStage.Parallel && batchIndex < batches.Count)
        {
            return phasePrefix + ".parallel.batch_group." + batchIndex;
        }

        if (stage is RunnerStage.Pre or RunnerStage.Post)
        {
            return phasePrefix + "." + stage.ToString().ToLowerInvariant() + ".batch." + batchIndex;
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
                    continue;
                case RunnerStage.ClearParallelResults:
                    manager.clearParallelResults();
                    stage = RunnerStage.Parallel;
                    return false;
                case RunnerStage.Parallel:
                    if (TryRunNextParallelBatchGroup())
                    {
                        return false;
                    }

                    stage = RunnerStage.ApplyParallelResults;
                    batchIndex = 0;
                    continue;
                case RunnerStage.ApplyParallelResults:
                    manager.applyParallelResults();
                    stage = RunnerStage.Post;
                    return false;
                case RunnerStage.Post:
                    if (TryRunNextMainThreadBatch(RunnerStage.Post))
                    {
                        return false;
                    }

                    stage = RunnerStage.Finish;
                    continue;
                case RunnerStage.Finish:
                    manager.saveJobBenchmarks();
                    batches.Clear();
                    manager = null;
                    parallelOptions = null;
                    parallelEnabled = false;
                    parallelGroupSize = 0;
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
        manager = null;
        parallelOptions = null;
        parallelEnabled = false;
        parallelGroupSize = 0;
        stage = RunnerStage.Idle;
        batchIndex = 0;
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

    private bool TryRunNextParallelBatchGroup()
    {
        if (batchIndex >= batches.Count)
        {
            return false;
        }

        int startIndex = batchIndex;
        int groupSize = Math.Min(parallelGroupSize, batches.Count - startIndex);
        int endIndex = startIndex + groupSize;

        if (parallelEnabled && groupSize > 1)
        {
            Parallel.For(startIndex, endIndex, parallelOptions, RunParallelBatch);
        }
        else
        {
            RunParallelBatch(startIndex);
        }

        batchIndex = endIndex;
        return true;
    }

    private void RunParallelBatch(int index)
    {
        TBatch batch = batches[index];
        batch._elapsed = elapsed;
        batch.updateJobsParallel(elapsed);
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
}
