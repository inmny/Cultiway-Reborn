using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Core.Libraries;

namespace Cultiway.Core;

/// <summary>
/// 宗门运行期岗位名额和占用计数，结构参考原版 CitizenJobs。
/// </summary>
[Serializable]
public class SectJobsContainer
{
    public readonly Dictionary<SectJobAsset, int> jobs = new();
    public readonly Dictionary<SectJobAsset, int> occupied = new();

    private int _totalTasks;

    public void Clear()
    {
        jobs.Clear();
        occupied.Clear();
        _totalTasks = 0;
    }

    public void ClearJobs()
    {
        jobs.Clear();
        _totalTasks = 0;
    }

    public void ClearOccupied()
    {
        occupied.Clear();
    }

    public int GetTotalTasks()
    {
        return _totalTasks;
    }

    public bool HasAnyTask()
    {
        return _totalTasks > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddToJob(SectJobAsset job, int value)
    {
        if (job == null || value <= 0) return;

        _totalTasks += value;
        if (jobs.TryGetValue(job, out int current))
        {
            jobs[job] = current + value;
        }
        else
        {
            jobs.Add(job, value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountCurrentJobs(SectJobAsset job)
    {
        return job != null && jobs.TryGetValue(job, out int value) ? value : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountOccupied(SectJobAsset job)
    {
        return job != null && occupied.TryGetValue(job, out int value) ? value : 0;
    }

    public bool HasJob(SectJobAsset job)
    {
        if (job == null) return false;
        if (!jobs.TryGetValue(job, out int value) || value <= 0) return false;
        return CountOccupied(job) < value;
    }

    public void TakeJob(SectJobAsset job)
    {
        if (job == null) return;
        occupied[job] = CountOccupied(job) + 1;
    }

    public void FreeJob(SectJobAsset job)
    {
        if (job == null) return;
        if (!occupied.TryGetValue(job, out int value)) return;
        occupied[job] = Math.Max(0, value - 1);
    }
}
