using System;
using System.Diagnostics;
using System.Globalization;
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

    private readonly ManualResetEventSlim operationCompleted = new(true);
    private readonly object operationLock = new();
    private readonly Thread[] workers;
    private readonly AutoResetEvent[] workerSignals;

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
    private int assistantJoined;
    private long operationStartedAt;
    private long operationCompletedAt;
    private long participantBusyTicks;
    private long mainWaitTicks;
    private bool operationActive;
    private bool operationAsynchronous;

    private long completedOperations;
    private long completedAsynchronousOperations;
    private long completedItems;
    private long completedWallTicks;
    private long completedParticipantBusyTicks;
    private long completedMainWaitTicks;
    private long completedParticipantSlots;
    private long completedParticipantCapacityTicks;
    private long completedAssistedOperations;

    private SimulationWorkerPool()
    {
        int workerCount = Math.Max(0, PerformanceSettings.ForegroundParallelism - 1);
        workers = new Thread[workerCount];
        workerSignals = new AutoResetEvent[workerCount];
        for (int i = 0; i < workers.Length; i++)
        {
            workerSignals[i] = new AutoResetEvent(false);
            var worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Cultiway Simulation Worker " + (i + 1),
                Priority = ThreadPriority.Normal
            };
            workers[i] = worker;
            worker.Start(i);
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

    /// <summary>
    /// 等待协调线程时，主线程可以作为额外参与者领取尚未开始的 work item。
    /// 参与者计数先用 CAS 加一，确保最后一个固定 worker 不会在协助者
    /// 仍访问 operationAction 时提前发布完成屏障。
    /// </summary>
    internal bool TryAssistActiveOperation()
    {
        int generation = Volatile.Read(ref activeGeneration);
        if (generation == 0 ||
            Volatile.Read(ref completionMarked) != 0 ||
            Volatile.Read(ref nextIndex) >=
            Volatile.Read(ref endIndex) - 1)
        {
            return false;
        }

        while (true)
        {
            int participants =
                Volatile.Read(ref remainingParticipants);
            if (participants <= 0 ||
                generation != Volatile.Read(ref activeGeneration) ||
                Volatile.Read(ref completionMarked) != 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(
                    ref remainingParticipants,
                    participants + 1,
                    participants) == participants)
            {
                break;
            }
        }

        Interlocked.Exchange(ref assistantJoined, 1);
        try
        {
            ExecuteItems(generation);
        }
        finally
        {
            SignalParticipantCompleted(generation);
        }

        return true;
    }

    internal void Wait(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        if (operationCompleted.IsSet)
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        operationCompleted.Wait();
        Interlocked.Add(ref mainWaitTicks, Stopwatch.GetTimestamp() - startedAt);
    }

    internal bool TryWait(WorkTicket ticket, double maximumMilliseconds)
    {
        ValidateActiveTicket(ticket);
        if (operationCompleted.IsSet)
        {
            return true;
        }

        if (maximumMilliseconds <= 0.0)
        {
            return false;
        }

        long startedAt = Stopwatch.GetTimestamp();
        long maximumTicks = Math.Max(
            1L,
            (long)(maximumMilliseconds * Stopwatch.Frequency / 1000.0));
        long deadline = startedAt + maximumTicks;
        while (!operationCompleted.IsSet)
        {
            if (Stopwatch.GetTimestamp() >= deadline)
            {
                Interlocked.Add(ref mainWaitTicks, Stopwatch.GetTimestamp() - startedAt);
                return false;
            }

            Thread.SpinWait(64);
        }

        Interlocked.Add(ref mainWaitTicks, Stopwatch.GetTimestamp() - startedAt);
        return true;
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
                Math.Max(0L, Interlocked.Read(ref mainWaitTicks)),
                workerSlots,
                operationAsynchronous,
                Volatile.Read(ref assistantJoined) != 0);
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
            assistantJoined = 0;
        }

        RecordCompletedOperation(result);
        exception?.Throw();
        return result;
    }

    internal string GetDiagnostics()
    {
        long operations = Interlocked.Read(ref completedOperations);
        long asynchronousOperations = Interlocked.Read(ref completedAsynchronousOperations);
        long items = Interlocked.Read(ref completedItems);
        long wallTicks = Interlocked.Read(ref completedWallTicks);
        long busyTicks = Interlocked.Read(ref completedParticipantBusyTicks);
        long waitTicks = Interlocked.Read(ref completedMainWaitTicks);
        long participantSlots = Interlocked.Read(ref completedParticipantSlots);
        long participantCapacityTicks = Interlocked.Read(ref completedParticipantCapacityTicks);
        long assistedOperations =
            Interlocked.Read(ref completedAssistedOperations);
        double wallSeconds = wallTicks / (double)Stopwatch.Frequency;
        double busySeconds = busyTicks / (double)Stopwatch.Frequency;
        double waitSeconds = waitTicks / (double)Stopwatch.Frequency;
        double parallelism = wallTicks > 0L
            ? busyTicks / (double)wallTicks
            : 0.0;
        double averageSlots = operations > 0L
            ? participantSlots / (double)operations
            : 0.0;
        double utilization = participantCapacityTicks > 0L
            ? busyTicks / (double)participantCapacityTicks * 100.0
            : 0.0;
        double blockedShare = wallTicks > 0L
            ? waitTicks / (double)wallTicks * 100.0
            : 0.0;
        bool active;
        lock (operationLock)
        {
            active = operationActive;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "ops={0}(async={1},assist={11}) items={2} wall={3:0.0}ms busy={4:0.0}ms wait={5:0.0}ms parallel={6:0.00}x slots={7:0.00} util={8:0.0}% blocked={9:0.0}% active={10}",
            operations,
            asynchronousOperations,
            items,
            wallSeconds * 1000.0,
            busySeconds * 1000.0,
            waitSeconds * 1000.0,
            parallelism,
            averageSlots,
            utilization,
            blockedShare,
            active,
            assistedOperations);
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
            mainWaitTicks = 0L;
            assistantJoined = 0;
            operationStartedAt = Stopwatch.GetTimestamp();
            operationCompletedAt = 0L;
            operationCompleted.Reset();
            ticket = new WorkTicket(activeGeneration);
        }

        for (int i = 0; i < backgroundWorkers; i++)
        {
            workerSignals[i].Set();
        }

        return ticket;
    }

    private void WorkerLoop(object state)
    {
        int workerIndex = (int)state;
        AutoResetEvent signal = workerSignals[workerIndex];
        while (true)
        {
            signal.WaitOne();
            int generation =
                Volatile.Read(ref activeGeneration);
            if (generation == 0)
            {
                continue;
            }

            ExecuteItems(generation);
            SignalParticipantCompleted(generation);
        }
    }

    private void RecordCompletedOperation(WorkResult result)
    {
        Interlocked.Increment(ref completedOperations);
        if (result.RanAsynchronously)
        {
            Interlocked.Increment(ref completedAsynchronousOperations);
        }
        if (result.Assisted)
        {
            Interlocked.Increment(ref completedAssistedOperations);
        }

        Interlocked.Add(ref completedItems, result.ExecutedItems);
        Interlocked.Add(ref completedWallTicks, result.WallTicks);
        Interlocked.Add(ref completedParticipantBusyTicks, result.ParticipantBusyTicks);
        Interlocked.Add(ref completedMainWaitTicks, result.MainWaitTicks);
        Interlocked.Add(ref completedParticipantSlots, result.ParticipantSlots);
        Interlocked.Add(
            ref completedParticipantCapacityTicks,
            result.WallTicks * result.ParticipantSlots);
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
            long mainWaitTicks,
            int workerSlots,
            bool ranAsynchronously,
            bool assisted)
        {
            ScheduledItems = scheduledItems;
            ExecutedItems = executedItems;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            WallTicks = Math.Max(0L, completedAt - startedAt);
            WallSeconds = WallTicks / (double)Stopwatch.Frequency;
            ParticipantBusyTicks = participantBusyTicks;
            ParticipantBusySeconds = ParticipantBusyTicks / (double)Stopwatch.Frequency;
            MainWaitTicks = mainWaitTicks;
            MainWaitSeconds = MainWaitTicks / (double)Stopwatch.Frequency;
            WorkerSlots = workerSlots;
            RanAsynchronously = ranAsynchronously;
            Assisted = assisted;
            ParticipantSlots =
                workerSlots +
                (ranAsynchronously ? 0 : 1) +
                (assisted ? 1 : 0);
        }

        internal int ScheduledItems { get; }
        internal int ExecutedItems { get; }
        internal long StartedAt { get; }
        internal long CompletedAt { get; }
        internal long WallTicks { get; }
        internal double WallSeconds { get; }
        internal long ParticipantBusyTicks { get; }
        internal double ParticipantBusySeconds { get; }
        internal long MainWaitTicks { get; }
        internal double MainWaitSeconds { get; }
        internal int WorkerSlots { get; }
        internal bool RanAsynchronously { get; }
        internal bool Assisted { get; }
        internal int ParticipantSlots { get; }
    }
}
