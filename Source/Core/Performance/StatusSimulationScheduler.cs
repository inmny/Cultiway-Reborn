using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 固定步模式下按下一次动作或到期 tick 调度状态。
/// 状态动画由表现快照推进，因此没有事件的状态无需每 tick 扫描。
/// </summary>
internal static class StatusSimulationScheduler
{
    private enum MutationKind : byte
    {
        Added,
        DurationChanged,
        Finished,
        Removed
    }

    private sealed class Entry
    {
        internal Status Status;
        internal long Order;
        internal long Version;
        internal long NextActionTick;
        internal long NextExpiryTick;
        internal long LastProcessedTick;
    }

    private readonly struct Mutation
    {
        internal Mutation(
            Status status,
            MutationKind kind)
        {
            Status = status;
            Kind = kind;
        }

        internal Status Status { get; }
        internal MutationKind Kind { get; }
    }

    private readonly struct HeapNode
    {
        internal HeapNode(
            Entry entry,
            long dueTick)
        {
            Entry = entry;
            DueTick = dueTick;
            Version = entry.Version;
            Order = entry.Order;
        }

        internal Entry Entry { get; }
        internal long DueTick { get; }
        internal long Version { get; }
        internal long Order { get; }
    }

    private const long Never = long.MaxValue;

    private static readonly ConcurrentQueue<Mutation> mutations = new();
    private static readonly Dictionary<Status, Entry> entries = new();
    private static readonly List<HeapNode> heap = new();
    private static readonly List<Entry> removals = new();
    private static readonly Comparison<Entry> reverseOrderComparison =
        CompareEntryOrderDescending;

    private static StatusManager manager;
    private static int worldGeneration = -1;
    private static int lastListSyncFrame = -1;
    private static long updateTick;
    private static long nextOrder;
    private static long updates;
    private static long dueChecks;
    private static long actionCalls;
    private static long expirationCalls;
    private static long removedStatuses;
    private static long staleHeapNodes;
    private static long rebuilds;
    private static long listSyncs;
    private static long listSyncSkips;

    internal static bool Enabled =>
        PerformanceSettings.EnableFramePriorityScheduler &&
        !PerformanceSettings.EnableVanillaLargeSimulationStep &&
        Config.game_loaded &&
        !SmoothLoader.isLoading() &&
        World.world != null;

    internal static bool TryUpdate(
        StatusManager statusManager,
        float elapsed)
    {
        if (!Enabled ||
            statusManager == null ||
            World.world.isPaused() ||
            Math.Abs(
                elapsed -
                PerformanceSettings
                    .FixedSimulationStepSeconds) >
            0.000001f)
        {
            Disable();
            return false;
        }

        EnsureWorld(statusManager);
        long currentTick = ++updateTick;
        float worldTime =
            (float)World.world.getCurWorldTime();
        DrainMutations(
            currentTick,
            long.MinValue,
            worldTime);

        removals.Clear();
        long currentOrder = long.MinValue;
        while (heap.Count > 0 &&
               heap[0].DueTick <= currentTick)
        {
            HeapNode node = Pop();
            Entry entry = node.Entry;
            if (entry == null ||
                node.Version != entry.Version ||
                !entries.TryGetValue(
                    entry.Status,
                    out Entry currentEntry) ||
                !ReferenceEquals(entry, currentEntry))
            {
                staleHeapNodes++;
                continue;
            }

            Status status = entry.Status;
            currentOrder = Math.Max(
                currentOrder,
                entry.Order);
            entry.LastProcessedTick = currentTick;
            dueChecks++;

            if (status == null ||
                status.is_finished)
            {
                QueueRemoval(entry);
                DrainMutations(
                    currentTick,
                    currentOrder,
                    worldTime);
                continue;
            }

            bool actionDue =
                entry.NextActionTick <= currentTick;
            bool expiryDue =
                entry.NextExpiryTick <= currentTick;
            if (actionDue)
            {
                status._action_timer = 0f;
            }

            if (actionDue || expiryDue)
            {
                bool wasFinished = status.is_finished;
                status.update(0f, worldTime);
                if (actionDue)
                {
                    actionCalls++;
                }

                if (!wasFinished &&
                    status.is_finished)
                {
                    expirationCalls++;
                }
            }

            if (status.is_finished)
            {
                QueueRemoval(entry);
            }
            else
            {
                if (actionDue)
                {
                    entry.NextActionTick =
                        ComputeNextActionTickAfterCall(
                            currentTick,
                            status);
                }

                if (expiryDue)
                {
                    entry.NextExpiryTick =
                        ComputeNextExpiryTick(
                            currentTick,
                            worldTime,
                            status);
                }

                Schedule(entry);
            }

            DrainMutations(
                currentTick,
                currentOrder,
                worldTime);
        }

        DrainMutations(
            currentTick,
            long.MaxValue,
            worldTime);
        RemoveFinished(statusManager);
        updates++;
        return true;
    }

    internal static void NotifyAdded(Status status)
    {
        Enqueue(status, MutationKind.Added);
    }

    internal static void NotifyDurationChanged(
        Status status)
    {
        Enqueue(
            status,
            MutationKind.DurationChanged);
    }

    internal static void NotifyFinished(Status status)
    {
        Enqueue(status, MutationKind.Finished);
    }

    internal static void NotifyRemoved(Status status)
    {
        Enqueue(status, MutationKind.Removed);
    }

    internal static bool ShouldRunListSync()
    {
        if (!Enabled ||
            !SimulationStepContext.IsActive)
        {
            listSyncs++;
            return true;
        }

        int frame = Time.frameCount;
        if (lastListSyncFrame != frame)
        {
            lastListSyncFrame = frame;
            listSyncs++;
            return true;
        }

        listSyncSkips++;
        return false;
    }

    internal static void EnsureListCurrent(
        StatusManager statusManager)
    {
        if (!Enabled || statusManager == null)
        {
            return;
        }

        statusManager.checkLists();
        listSyncs++;
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "active={0} statuses={1} heap={2} mutations={3} " +
            "updates={4} due={5} actions={6} expirations={7} removed={8} " +
            "stale={9} rebuilds={10} list_sync={11}/{12}(run/skip)",
            manager != null,
            entries.Count,
            heap.Count,
            mutations.Count,
            Interlocked.Read(ref updates),
            Interlocked.Read(ref dueChecks),
            Interlocked.Read(ref actionCalls),
            Interlocked.Read(ref expirationCalls),
            Interlocked.Read(ref removedStatuses),
            Interlocked.Read(ref staleHeapNodes),
            Interlocked.Read(ref rebuilds),
            Interlocked.Read(ref listSyncs),
            Interlocked.Read(ref listSyncSkips));
    }

    private static void Enqueue(
        Status status,
        MutationKind kind)
    {
        if (status == null || !Enabled)
        {
            return;
        }

        mutations.Enqueue(
            new Mutation(status, kind));
    }

    private static void EnsureWorld(
        StatusManager statusManager)
    {
        int generation = SimulationTime.Generation;
        if (ReferenceEquals(manager, statusManager) &&
            worldGeneration == generation)
        {
            return;
        }

        ClearState(restoreTimers: false);
        manager = statusManager;
        worldGeneration = generation;
        updateTick = 0L;
        nextOrder = 0L;
        lastListSyncFrame = -1;
        float worldTime =
            (float)World.world.getCurWorldTime();
        List<Status> statuses = statusManager.list;
        long firstUpdateTick = 1L;
        for (int i = 0; i < statuses.Count; i++)
        {
            Register(
                statuses[i],
                firstUpdateTick,
                worldTime);
        }

        rebuilds++;
    }

    private static void Disable()
    {
        if (manager == null)
        {
            return;
        }

        ClearState(restoreTimers: true);
    }

    private static void ClearState(bool restoreTimers)
    {
        if (restoreTimers)
        {
            foreach (Entry entry in entries.Values)
            {
                RestoreActionTimer(entry);
            }
        }

        entries.Clear();
        heap.Clear();
        removals.Clear();
        while (mutations.TryDequeue(out _))
        {
        }

        manager = null;
        worldGeneration = -1;
        updateTick = 0L;
        nextOrder = 0L;
        lastListSyncFrame = -1;
    }

    private static void Register(
        Status status,
        long firstUpdateTick,
        float worldTime)
    {
        if (status == null)
        {
            return;
        }

        if (entries.TryGetValue(
                status,
                out Entry existing))
        {
            existing.Version++;
            existing.NextExpiryTick =
                ComputeNextExpiryTick(
                    firstUpdateTick,
                    worldTime,
                    status);
            Schedule(existing);
            return;
        }

        var entry = new Entry
        {
            Status = status,
            Order = nextOrder++,
            Version = 1L,
            NextActionTick =
                ComputeFirstActionTick(
                    firstUpdateTick,
                    status),
            NextExpiryTick =
                status.is_finished
                    ? firstUpdateTick
                    : ComputeNextExpiryTick(
                        firstUpdateTick,
                        worldTime,
                        status)
        };
        entries.Add(status, entry);
        Schedule(entry);
    }

    private static void DrainMutations(
        long currentTick,
        long currentOrder,
        float worldTime)
    {
        while (mutations.TryDequeue(
                   out Mutation mutation))
        {
            Status status = mutation.Status;
            switch (mutation.Kind)
            {
                case MutationKind.Added:
                    long firstUpdateTick =
                        currentOrder == long.MinValue
                            ? currentTick
                            : SafeAdd(currentTick, 1L);
                    Register(
                        status,
                        firstUpdateTick,
                        worldTime);
                    break;
                case MutationKind.DurationChanged:
                    if (entries.TryGetValue(
                            status,
                            out Entry durationEntry))
                    {
                        long earliestEligibleTick =
                            GetEarliestEligibleTick(
                                durationEntry,
                                currentTick,
                                currentOrder);
                        durationEntry.Version++;
                        durationEntry.NextExpiryTick =
                            Math.Max(
                                earliestEligibleTick,
                                ComputeNextExpiryTick(
                                    currentTick,
                                    worldTime,
                                    status));
                        Schedule(durationEntry);
                    }

                    break;
                case MutationKind.Finished:
                    if (entries.TryGetValue(
                            status,
                            out Entry finishedEntry))
                    {
                        finishedEntry.Version++;
                        finishedEntry.NextExpiryTick =
                            GetEarliestEligibleTick(
                                finishedEntry,
                                currentTick,
                                currentOrder);
                        Schedule(finishedEntry);
                    }

                    break;
                case MutationKind.Removed:
                    if (entries.TryGetValue(
                            status,
                            out Entry removedEntry))
                    {
                        removedEntry.Version++;
                        entries.Remove(status);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static long GetEarliestEligibleTick(
        Entry entry,
        long currentTick,
        long currentOrder)
    {
        return entry.LastProcessedTick == currentTick ||
               entry.Order <= currentOrder
            ? SafeAdd(currentTick, 1L)
            : currentTick;
    }

    private static long ComputeFirstActionTick(
        long firstUpdateTick,
        Status status)
    {
        if (status.asset?.action == null)
        {
            return Never;
        }

        return SafeAdd(
            firstUpdateTick,
            CountTimerDecrementTicks(
                status._action_timer));
    }

    private static long ComputeNextActionTickAfterCall(
        long currentTick,
        Status status)
    {
        if (status.asset?.action == null)
        {
            return Never;
        }

        long firstDecrementTick =
            SafeAdd(currentTick, 1L);
        return SafeAdd(
            firstDecrementTick,
            CountTimerDecrementTicks(
                status._action_timer));
    }

    private static long CountTimerDecrementTicks(
        float timer)
    {
        if (float.IsNaN(timer) ||
            timer <= 0f)
        {
            return 0L;
        }

        if (float.IsPositiveInfinity(timer))
        {
            return Never;
        }

        long ticks = 0L;
        float remaining = timer;
        float step =
            PerformanceSettings
                .FixedSimulationStepSeconds;
        while (remaining > 0f)
        {
            remaining -= step;
            if (ticks == long.MaxValue - 1L)
            {
                return Never;
            }

            ticks++;
        }

        return ticks;
    }

    private static long ComputeNextExpiryTick(
        long earliestTick,
        float worldTime,
        Status status)
    {
        if (status == null ||
            status.is_finished)
        {
            return earliestTick;
        }

        double endTime = status._end_time;
        if (double.IsNaN(endTime) ||
            double.IsPositiveInfinity(endTime))
        {
            return Never;
        }

        double remaining =
            endTime - worldTime;
        if (remaining <= 0.0)
        {
            return earliestTick;
        }

        double estimatedTicks =
            Math.Floor(
                remaining /
                PerformanceSettings
                    .FixedSimulationStepSeconds);
        long delay = estimatedTicks >= long.MaxValue
            ? Never
            : Math.Max(1L, (long)estimatedTicks);
        return SafeAdd(earliestTick, delay);
    }

    private static void Schedule(Entry entry)
    {
        entry.Version++;
        long dueTick = Math.Min(
            entry.NextActionTick,
            entry.NextExpiryTick);
        if (dueTick == Never)
        {
            return;
        }

        Push(new HeapNode(entry, dueTick));
    }

    private static void QueueRemoval(Entry entry)
    {
        if (!removals.Contains(entry))
        {
            removals.Add(entry);
        }
    }

    private static void RemoveFinished(
        StatusManager statusManager)
    {
        if (removals.Count == 0)
        {
            return;
        }

        removals.Sort(reverseOrderComparison);
        for (int i = 0; i < removals.Count; i++)
        {
            Entry entry = removals[i];
            Status status = entry.Status;
            if (status == null ||
                !status.is_finished ||
                !entries.TryGetValue(
                    status,
                    out Entry current) ||
                !ReferenceEquals(entry, current))
            {
                continue;
            }

            entry.Version++;
            entries.Remove(status);
            statusManager.removeObject(status);
            removedStatuses++;
        }

        removals.Clear();
    }

    private static void RestoreActionTimer(
        Entry entry)
    {
        Status status = entry.Status;
        if (status == null ||
            status.asset?.action == null ||
            entry.NextActionTick == Never)
        {
            return;
        }

        long ticksUntilAction =
            entry.NextActionTick - updateTick;
        if (ticksUntilAction <= 0L)
        {
            status._action_timer = 0f;
            return;
        }

        long decrementTicks =
            Math.Max(0L, ticksUntilAction - 1L);
        status._action_timer =
            decrementTicks *
            PerformanceSettings
                .FixedSimulationStepSeconds;
    }

    private static int CompareEntryOrderDescending(
        Entry left,
        Entry right)
    {
        return right.Order.CompareTo(left.Order);
    }

    private static long SafeAdd(long left, long right)
    {
        if (left == Never ||
            right == Never ||
            right > 0L &&
            left > long.MaxValue - right)
        {
            return Never;
        }

        return left + right;
    }

    private static void Push(HeapNode node)
    {
        int index = heap.Count;
        heap.Add(node);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (Compare(heap[parent], node) <= 0)
            {
                break;
            }

            heap[index] = heap[parent];
            index = parent;
        }

        heap[index] = node;
    }

    private static HeapNode Pop()
    {
        HeapNode root = heap[0];
        int lastIndex = heap.Count - 1;
        HeapNode last = heap[lastIndex];
        heap.RemoveAt(lastIndex);
        if (lastIndex == 0)
        {
            return root;
        }

        int index = 0;
        int half = lastIndex >> 1;
        while (index < half)
        {
            int left = (index << 1) + 1;
            int right = left + 1;
            int child =
                right < lastIndex &&
                Compare(heap[right], heap[left]) < 0
                    ? right
                    : left;
            if (Compare(last, heap[child]) <= 0)
            {
                break;
            }

            heap[index] = heap[child];
            index = child;
        }

        heap[index] = last;
        return root;
    }

    private static int Compare(
        HeapNode left,
        HeapNode right)
    {
        int due = left.DueTick.CompareTo(
            right.DueTick);
        return due != 0
            ? due
            : left.Order.CompareTo(right.Order);
    }
}
