using System;
using System.Collections.Generic;
using UnityEngine;

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
    private int jobIndex;

    public CooperativeBatchRunner(string phasePrefix)
    {
        this.phasePrefix = phasePrefix;
    }

    public bool Active => stage != RunnerStage.Idle;

    public void Start(
        JobManagerBase<TBatch, TObject> jobManager,
        IEnumerable<TBatch> activeBatches,
        float cycleElapsed,
        Comparison<TBatch> comparison = null)
    {
        manager = jobManager;
        elapsed = cycleElapsed;
        batches.Clear();
        batches.AddRange(activeBatches);
        if (comparison != null)
        {
            batches.Sort(comparison);
        }

        manager.clearJobBenchmarks();
        batchIndex = 0;
        jobIndex = 0;
        stage = RunnerStage.Pre;
    }

    public string GetNextPhaseName()
    {
        if (!Active)
        {
            return phasePrefix + ".idle";
        }

        if ((stage == RunnerStage.Pre || stage == RunnerStage.Parallel || stage == RunnerStage.Post) &&
            batchIndex < batches.Count)
        {
            List<Job<TObject>> jobs = GetJobs(batches[batchIndex], stage);
            if (jobIndex < jobs.Count)
            {
                return phasePrefix + "." + stage.ToString().ToLowerInvariant() + "." + jobs[jobIndex].id;
            }
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
                    if (TryRunNextJob(RunnerStage.Pre))
                    {
                        return false;
                    }

                    stage = RunnerStage.ClearParallelResults;
                    batchIndex = 0;
                    jobIndex = 0;
                    continue;
                case RunnerStage.ClearParallelResults:
                    manager.clearParallelResults();
                    stage = RunnerStage.Parallel;
                    return false;
                case RunnerStage.Parallel:
                    if (TryRunNextJob(RunnerStage.Parallel))
                    {
                        return false;
                    }

                    stage = RunnerStage.ApplyParallelResults;
                    batchIndex = 0;
                    jobIndex = 0;
                    continue;
                case RunnerStage.ApplyParallelResults:
                    manager.applyParallelResults();
                    stage = RunnerStage.Post;
                    return false;
                case RunnerStage.Post:
                    if (TryRunNextJob(RunnerStage.Post))
                    {
                        return false;
                    }

                    stage = RunnerStage.Finish;
                    continue;
                case RunnerStage.Finish:
                    manager.saveJobBenchmarks();
                    batches.Clear();
                    manager = null;
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
        stage = RunnerStage.Idle;
        batchIndex = 0;
        jobIndex = 0;
    }

    private bool TryRunNextJob(RunnerStage jobStage)
    {
        while (batchIndex < batches.Count)
        {
            TBatch batch = batches[batchIndex];
            batch._elapsed = elapsed;
            List<Job<TObject>> jobs = GetJobs(batch, jobStage);
            if (jobIndex >= jobs.Count)
            {
                batchIndex++;
                jobIndex = 0;
                continue;
            }

            Job<TObject> job = jobs[jobIndex++];
            batch._cur_container = job.container;
            if (jobStage == RunnerStage.Parallel)
            {
                job.job_updater();
            }
            else
            {
                RunMainThreadJob(batch, job);
            }

            return true;
        }

        return false;
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

    private static void RunMainThreadJob(TBatch batch, Job<TObject> job)
    {
        if (job.current_skips > 0)
        {
            job.current_skips--;
            return;
        }

        double startedAt = Time.realtimeSinceStartupAsDouble;
        job.job_updater();
        if (job.random_tick_skips > 0)
        {
            job.current_skips = Randy.randomInt(0, job.random_tick_skips);
        }

        job.time_benchmark += Time.realtimeSinceStartupAsDouble - startedAt;
        if (batch._cur_container != null)
        {
            job.counter += batch._cur_container.Count;
        }
    }
}
