using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 长驻模拟协调线程。它负责串起需要保留阶段屏障的后台工作，
/// 具体数据并行仍交给 <see cref="SimulationWorkerPool"/>。
/// </summary>
internal sealed class SimulationCoordinatorThread
{
    internal static SimulationCoordinatorThread Instance { get; } = new();

    private readonly object gate = new();
    private readonly AutoResetEvent workReady = new(false);
    private readonly ManualResetEventSlim workCompleted = new(true);
    private readonly Thread thread;

    private Action operation;
    private ExceptionDispatchInfo operationException;
    private string operationName;
    private int activeGeneration;
    private int nextGeneration;
    private bool operationActive;
    private long operationStartedAt;
    private long operationCompletedAt;
    private long operationWaitTicks;

    private long completedOperations;
    private long completedWallTicks;
    private long completedWaitTicks;

    private SimulationCoordinatorThread()
    {
        thread = new Thread(CoordinatorLoop)
        {
            IsBackground = true,
            Name = "Cultiway Simulation Coordinator",
            Priority = ThreadPriority.Normal
        };
        thread.Start();
    }

    internal WorkTicket Begin(string name, Action action)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("后台模拟工作必须提供诊断名称", nameof(name));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        WorkTicket ticket;
        lock (gate)
        {
            if (operationActive)
            {
                throw new InvalidOperationException(
                    "模拟协调线程仍有未提交的工作: " + operationName);
            }

            activeGeneration = unchecked(++nextGeneration);
            if (activeGeneration == 0)
            {
                activeGeneration = unchecked(++nextGeneration);
            }

            operation = action;
            operationException = null;
            operationName = name;
            operationActive = true;
            operationStartedAt = Stopwatch.GetTimestamp();
            operationCompletedAt = 0L;
            operationWaitTicks = 0L;
            workCompleted.Reset();
            ticket = new WorkTicket(activeGeneration);
        }

        workReady.Set();
        return ticket;
    }

    internal bool IsCompleted(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        return workCompleted.IsSet;
    }

    internal void Wait(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        if (workCompleted.IsSet)
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        int idleSpins = 0;
        while (!workCompleted.IsSet)
        {
            if (SimulationWorkerPool.Instance
                .TryAssistActiveOperation())
            {
                idleSpins = 0;
                continue;
            }

            if (idleSpins++ < 64)
            {
                Thread.SpinWait(64);
            }
            else
            {
                Thread.Yield();
                idleSpins = 0;
            }
        }

        Interlocked.Add(
            ref operationWaitTicks,
            Stopwatch.GetTimestamp() - startedAt);
    }

    internal bool TryWait(WorkTicket ticket, double maximumMilliseconds)
    {
        ValidateActiveTicket(ticket);
        if (workCompleted.IsSet)
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
        int idleSpins = 0;
        while (!workCompleted.IsSet)
        {
            if (Stopwatch.GetTimestamp() >= deadline)
            {
                Interlocked.Add(
                    ref operationWaitTicks,
                    Stopwatch.GetTimestamp() - startedAt);
                return false;
            }

            if (SimulationWorkerPool.Instance
                .TryAssistActiveOperation())
            {
                idleSpins = 0;
            }
            else if (idleSpins++ < 64)
            {
                Thread.SpinWait(64);
            }
            else
            {
                Thread.Yield();
                idleSpins = 0;
            }
        }

        Interlocked.Add(
            ref operationWaitTicks,
            Stopwatch.GetTimestamp() - startedAt);
        return true;
    }

    internal WorkResult Complete(WorkTicket ticket)
    {
        ValidateActiveTicket(ticket);
        if (!workCompleted.IsSet)
        {
            throw new InvalidOperationException("后台模拟协调工作尚未完成");
        }

        WorkResult result;
        ExceptionDispatchInfo exception;
        lock (gate)
        {
            ValidateActiveTicketLocked(ticket);
            result = new WorkResult(
                operationName,
                operationStartedAt,
                operationCompletedAt,
                Math.Max(0L, Interlocked.Read(ref operationWaitTicks)));
            exception = operationException;
            operation = null;
            operationException = null;
            operationName = null;
            operationActive = false;
            activeGeneration = 0;
            operationStartedAt = 0L;
            operationCompletedAt = 0L;
            operationWaitTicks = 0L;
        }

        Interlocked.Increment(ref completedOperations);
        Interlocked.Add(ref completedWallTicks, result.WallTicks);
        Interlocked.Add(ref completedWaitTicks, result.WaitTicks);
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
            // Abort 只负责确保后台不再接触本轮世界数据。
        }
    }

    internal string GetDiagnostics()
    {
        long operations = Interlocked.Read(ref completedOperations);
        long wallTicks = Interlocked.Read(ref completedWallTicks);
        long waitTicks = Interlocked.Read(ref completedWaitTicks);
        bool active;
        string activeName;
        lock (gate)
        {
            active = operationActive;
            activeName = operationName;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "ops={0} wall={1:0.0}ms wait={2:0.0}ms active={3} name={4}",
            operations,
            TicksToMilliseconds(wallTicks),
            TicksToMilliseconds(waitTicks),
            active,
            activeName ?? "none");
    }

    private void CoordinatorLoop()
    {
        while (true)
        {
            workReady.WaitOne();
            Action action;
            int generation;
            lock (gate)
            {
                action = operation;
                generation = activeGeneration;
            }

            try
            {
                action();
            }
            catch (Exception exception)
            {
                lock (gate)
                {
                    if (operationActive &&
                        generation == activeGeneration &&
                        operationException == null)
                    {
                        operationException =
                            ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }
            finally
            {
                lock (gate)
                {
                    if (operationActive && generation == activeGeneration)
                    {
                        operationCompletedAt = Stopwatch.GetTimestamp();
                        workCompleted.Set();
                    }
                }
            }
        }
    }

    private void ValidateActiveTicket(WorkTicket ticket)
    {
        lock (gate)
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
            throw new InvalidOperationException("模拟协调线程 ticket 已失效");
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
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
            string name,
            long startedAt,
            long completedAt,
            long waitTicks)
        {
            Name = name;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            WallTicks = Math.Max(0L, completedAt - startedAt);
            WaitTicks = waitTicks;
        }

        internal string Name { get; }
        internal long StartedAt { get; }
        internal long CompletedAt { get; }
        internal long WallTicks { get; }
        internal long WaitTicks { get; }
        internal double WallMilliseconds =>
            TicksToMilliseconds(WallTicks);
        internal double WaitMilliseconds =>
            TicksToMilliseconds(WaitTicks);
    }
}
