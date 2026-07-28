using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Core.Performance;

/// <summary>
/// 在一个角色 AI 小批次内收集已经通过校验的寻路请求。
/// 角色字段和原版容器仍在主线程按原顺序修改；独立角色的请求快照、
/// 取消、任务创建与入队集中交给 worker 并行执行。
/// </summary>
internal static class DeferredPathRequestBatch
{
    private const int ParallelThreshold = 2;

    private static readonly List<RequestWorkItem> Requests = new(64);
    private static readonly Action<int> ProcessAction = ProcessAt;

    private static bool accepting;
    private static long capturedRequests;
    private static long completedBatches;
    private static long serialBatches;
    private static long parallelBatches;
    private static long totalProcessingTicks;
    private static long maximumProcessingTicks;
    private static int maximumBatchSize;

    internal static void Begin()
    {
        if (accepting || Requests.Count != 0)
        {
            throw new InvalidOperationException(
                "延迟寻路请求批次发生重入");
        }

        accepting = true;
    }

    internal static bool TryCapture(
        Actor actor,
        WorldTile target,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int regionLimit)
    {
        if (!accepting)
        {
            return false;
        }

        Requests.Add(new RequestWorkItem(
            actor,
            target,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            regionLimit));
        Interlocked.Increment(ref capturedRequests);
        return true;
    }

    internal static void Complete()
    {
        if (!accepting)
        {
            throw new InvalidOperationException(
                "延迟寻路请求批次尚未开始");
        }

        accepting = false;
        int count = Requests.Count;
        if (count == 0)
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            if (count < ParallelThreshold)
            {
                Interlocked.Increment(ref serialBatches);
                for (int i = 0; i < count; i++)
                {
                    ProcessAt(i);
                }
            }
            else
            {
                Interlocked.Increment(ref parallelBatches);
                PathFinder.Instance.EnsureWorkersReady();
                SimulationWorkerPool.Instance.RunIndexed(
                    0,
                    count,
                    ProcessAction);
            }
        }
        finally
        {
            RecordCompletedBatch(
                count,
                Stopwatch.GetTimestamp() - startedAt);
            Requests.Clear();
        }
    }

    internal static void Abort()
    {
        accepting = false;
        Requests.Clear();
    }

    private static void ProcessAt(int index)
    {
        Requests[index].Execute();
    }

    internal static string GetDiagnostics()
    {
        long batches = Interlocked.Read(ref completedBatches);
        return string.Format(
            CultureInfo.InvariantCulture,
            "requests={0} batches={1}(serial={2},parallel={3}) " +
            "batch_avg={4:0.00} batch_max={5} processing={6:0.000}ms(avg={7:0.000},max={8:0.000})",
            Interlocked.Read(ref capturedRequests),
            batches,
            Interlocked.Read(ref serialBatches),
            Interlocked.Read(ref parallelBatches),
            batches == 0
                ? 0.0
                : Interlocked.Read(ref capturedRequests) /
                  (double)batches,
            Volatile.Read(ref maximumBatchSize),
            TicksToMilliseconds(
                Interlocked.Read(ref totalProcessingTicks)),
            batches == 0
                ? 0.0
                : TicksToMilliseconds(
                    Interlocked.Read(ref totalProcessingTicks)) /
                  batches,
            TicksToMilliseconds(
                Interlocked.Read(ref maximumProcessingTicks)));
    }

    private static void RecordCompletedBatch(
        int count,
        long elapsedTicks)
    {
        Interlocked.Increment(ref completedBatches);
        Interlocked.Add(ref totalProcessingTicks, elapsedTicks);
        UpdateMaximum(ref maximumBatchSize, count);
        UpdateMaximum(ref maximumProcessingTicks, elapsedTicks);
    }

    private static void UpdateMaximum(ref int target, int value)
    {
        int current = Volatile.Read(ref target);
        while (value > current)
        {
            int previous = Interlocked.CompareExchange(
                ref target,
                value,
                current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static void UpdateMaximum(ref long target, long value)
    {
        long current = Interlocked.Read(ref target);
        while (value > current)
        {
            long previous = Interlocked.CompareExchange(
                ref target,
                value,
                current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private readonly struct RequestWorkItem
    {
        private readonly Actor actor;
        private readonly WorldTile target;
        private readonly bool pathOnWater;
        private readonly bool walkOnBlocks;
        private readonly bool walkOnLava;
        private readonly int regionLimit;

        internal RequestWorkItem(
            Actor actor,
            WorldTile target,
            bool pathOnWater,
            bool walkOnBlocks,
            bool walkOnLava,
            int regionLimit)
        {
            this.actor = actor;
            this.target = target;
            this.pathOnWater = pathOnWater;
            this.walkOnBlocks = walkOnBlocks;
            this.walkOnLava = walkOnLava;
            this.regionLimit = regionLimit;
        }

        internal void Execute()
        {
            PathFinder.Instance.RequestPathValidated(
                actor,
                target,
                pathOnWater,
                walkOnBlocks,
                walkOnLava,
                regionLimit);
        }
    }
}
