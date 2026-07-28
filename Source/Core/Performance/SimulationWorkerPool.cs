using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 模拟专用的长驻 worker 池。同一时刻只接受一个 job，以保留原版 job 之间的屏障。
/// </summary>
internal sealed class SimulationWorkerPool
{
    internal static SimulationWorkerPool Instance { get; } = new();

    private readonly BlockingCollection<int> workQueue = new();
    private readonly ManualResetEventSlim operationCompleted = new(true);
    private readonly object operationLock = new();
    private readonly Thread[] workers;

    private Action<int> operationAction;
    private ExceptionDispatchInfo operationException;
    private int activeGeneration;
    private int nextGeneration;
    private int nextIndex;
    private int endIndex;
    private int remainingParticipants;
    private int completionMarked;
    private int stopRequested;
    private int executedItems;
    private int itemCount;
    private int workerSlots;
    private long operationStartedAt;
    private long operationCompletedAt;
    private long participantBusyTicks;
    private bool operationActive;
    private bool operationAsynchronous;

    private SimulationWorkerPool()
    {
        int workerCount = Math.Max(0, PerformanceSettings.ForegroundParallelism - 1);
        workers = new Thread[workerCount];
        for (int i = 0; i < workers.Length; i++)
        {
            var worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Cultiway Simulation Worker " + (i + 1),
                Priority = ThreadPriority.Normal
            };
            workers[i] = worker;
            worker.Start();
        }
    }

    internal WorkResult RunIndexed(int startIndex, int exclusiveEndIndex, Action<int> action)
    {
        ValidateRange(startIndex, exclusiveEndIndex, action);
        int count = exclusiveEndIndex - startIndex;
        int backgroundWorkers = Math.Min(workers.Length, Math.Max(0, count - 1));
        WorkTicket ticket = StartOperation(
            startIndex,
            exclusiveEndIndex,
            action,
            backgroundWorkers,
            asynchronous: false);

        if (count > 0)
        {
            ExecuteItems(ticket.Generation);
        }

        SignalParticipantCompleted(ticket.Generation);

        Wait(ticket);
        return Complete(ticket);
    }

    internal WorkTicket BeginIndexed(int startIndex, int exclusiveEndIndex, Action<int> action)
    {
        ValidateRange(startIndex, exclusiveEndIndex, action);
        int count = exclusiveEndIndex - startIndex;
        int backgroundWorkers = Math.Min(workers.Length, count);
        WorkTicket ticket = StartOperation(
            startIndex,
            exclusiveEndIndex,
            action,
            backgroundWorkers,
            asynchronous: backgroundWorkers > 0);

        // 极小核心数机器没有可用后台 worker 时，同步完成，语义仍然一致。
        if (count > 0 && backgroundWorkers == 0)
        {
            ExecuteItems(ticket.Generation);
        }

        if (backgroundWorkers == 0)
        {
            SignalParticipantCompleted(ticket.Generation);
        }

        return ticket;
    }

    internal bool IsCompleted(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        return operationCompleted.IsSet;
    }

    internal void Wait(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        operationCompleted.Wait();
    }

    internal WorkResult Complete(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        if (!operationCompleted.IsSet)
        {
            throw new InvalidOperationException("模拟 worker 工作尚未完成");
        }

        ExceptionDispatchInfo exception;
        WorkResult result;
        lock (operationLock)
        {
            ValidateActiveTicketLocked(ticket);
            long completedAt = Volatile.Read(ref operationCompletedAt);
            result = new WorkResult(
                itemCount,
                Volatile.Read(ref executedItems),
                operationStartedAt,
                completedAt,
                Math.Max(0L, Interlocked.Read(ref participantBusyTicks)),
                workerSlots,
                operationAsynchronous);
            exception = operationException;
            if (exception == null && result.ExecutedItems != result.ScheduledItems)
            {
                exception = ExceptionDispatchInfo.Capture(
                    new InvalidOperationException(
                        "模拟 worker 未完整执行已调度工作: " +
                        result.ExecutedItems +
                        "/" +
                        result.ScheduledItems));
            }

            operationAction = null;
            operationException = null;
            operationActive = false;
            operationAsynchronous = false;
            activeGeneration = 0;
            nextIndex = 0;
            endIndex = 0;
            remainingParticipants = 0;
            itemCount = 0;
            workerSlots = 0;
        }

        exception?.Throw();
        return result;
    }

    internal void WaitAndDiscard(WorkTicket ticket)
    {
        if (!ticket.IsValid)
        {
            return;
        }

        Wait(ticket);
        try
        {
            Complete(ticket);
        }
        catch
        {
            // Abort 路径只负责确保后台不再访问本轮世界数据。
        }
    }

    private WorkTicket StartOperation(
        int startIndex,
        int exclusiveEndIndex,
        Action<int> action,
        int backgroundWorkers,
        bool asynchronous)
    {
        WorkTicket ticket;
        lock (operationLock)
        {
            if (operationActive)
            {
                throw new InvalidOperationException("模拟 worker 池仍有未提交的工作");
            }

            operationActive = true;
            operationAsynchronous = asynchronous;
            activeGeneration = unchecked(++nextGeneration);
            if (activeGeneration == 0)
            {
                activeGeneration = unchecked(++nextGeneration);
            }

            operationAction = action;
            operationException = null;
            nextIndex = startIndex - 1;
            endIndex = exclusiveEndIndex;
            itemCount = exclusiveEndIndex - startIndex;
            workerSlots = backgroundWorkers;
            remainingParticipants = backgroundWorkers + (asynchronous ? 0 : 1);
            completionMarked = 0;
            stopRequested = 0;
            executedItems = 0;
            participantBusyTicks = 0L;
            operationStartedAt = Stopwatch.GetTimestamp();
            operationCompletedAt = 0L;
            operationCompleted.Reset();
            ticket = new WorkTicket(activeGeneration);
        }

        for (int i = 0; i < backgroundWorkers; i++)
        {
            workQueue.Add(ticket.Generation);
        }

        return ticket;
    }

    private void WorkerLoop()
    {
        foreach (int generation in workQueue.GetConsumingEnumerable())
        {
            ExecuteItems(generation);
            SignalParticipantCompleted(generation);
        }
    }

    private void ExecuteItems(int generation)
    {
        if (generation != Volatile.Read(ref activeGeneration))
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            while (Volatile.Read(ref stopRequested) == 0)
            {
                int index = Interlocked.Increment(ref nextIndex);
                if (index >= endIndex)
                {
                    break;
                }

                try
                {
                    operationAction(index);
                    Interlocked.Increment(ref executedItems);
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(
                        ref operationException,
                        ExceptionDispatchInfo.Capture(exception),
                        null);
                    Volatile.Write(ref stopRequested, 1);
                    break;
                }
            }
        }
        finally
        {
            Interlocked.Add(
                ref participantBusyTicks,
                Stopwatch.GetTimestamp() - startedAt);
        }
    }

    private void MarkOperationCompleted(int generation)
    {
        if (generation != Volatile.Read(ref activeGeneration) ||
            Interlocked.CompareExchange(ref completionMarked, 1, 0) != 0)
        {
            return;
        }

        Volatile.Write(ref operationCompletedAt, Stopwatch.GetTimestamp());
        operationCompleted.Set();
    }

    private void SignalParticipantCompleted(int generation)
    {
        if (generation == Volatile.Read(ref activeGeneration) &&
            Interlocked.Decrement(ref remainingParticipants) == 0)
        {
            MarkOperationCompleted(generation);
        }
    }

    private void ValidateActiveTicket(WorkTicket ticket)
    {
        lock (operationLock)
        {
            ValidateActiveTicketLocked(ticket);
        }
    }

    private void ValidateActiveTicketLocked(WorkTicket ticket)
    {
        if (!ticket.IsValid ||
            !operationActive ||
            ticket.Generation != activeGeneration)
        {
            throw new InvalidOperationException("模拟 worker ticket 已失效");
        }
    }

    private static void ValidateRange(
        int startIndex,
        int exclusiveEndIndex,
        Action<int> action)
    {
        if (startIndex < 0 || exclusiveEndIndex < startIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
    }

    internal readonly struct WorkTicket
    {
        internal WorkTicket(int generation)
        {
            Generation = generation;
        }

        internal int Generation { get; }
        internal bool IsValid => Generation != 0;
    }

    internal readonly struct WorkResult
    {
        internal WorkResult(
            int scheduledItems,
            int executedItems,
            long startedAt,
            long completedAt,
            long participantBusyTicks,
            int workerSlots,
            bool ranAsynchronously)
        {
            ScheduledItems = scheduledItems;
            ExecutedItems = executedItems;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            WallTicks = Math.Max(0L, completedAt - startedAt);
            WallSeconds = WallTicks / (double)Stopwatch.Frequency;
            ParticipantBusySeconds = participantBusyTicks / (double)Stopwatch.Frequency;
            WorkerSlots = workerSlots;
            RanAsynchronously = ranAsynchronously;
        }

        internal int ScheduledItems { get; }
        internal int ExecutedItems { get; }
        internal long StartedAt { get; }
        internal long CompletedAt { get; }
        internal long WallTicks { get; }
        internal double WallSeconds { get; }
        internal double ParticipantBusySeconds { get; }
        internal int WorkerSlots { get; }
        internal bool RanAsynchronously { get; }
    }
}
