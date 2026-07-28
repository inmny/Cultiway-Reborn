using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 按原版 actor batch 增量维护船内角色，避免 u1_checkInside 每 tick 扫描全部角色。
/// </summary>
internal static class InsideBoatActorIndex
{
    private sealed class Partition
    {
        internal readonly HashSet<Actor> Actors =
            new(ActorReferenceComparer.Instance);
        internal Actor[] Snapshot = Array.Empty<Actor>();
        internal int SnapshotCount;
        internal bool Dirty = true;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<BatchActors, Partition>
        Partitions = new();
    private static readonly Dictionary<Actor, BatchActors>
        ActorPartitions =
            new(ActorReferenceComparer.Instance);

    private static long additions;
    private static long removals;
    private static long snapshotReads;
    private static long processedActors;

    internal static void Notify(Actor actor, bool isInsideBoat)
    {
        if (actor == null)
        {
            return;
        }

        lock (Gate)
        {
            ActorPartitions.TryGetValue(
                actor,
                out BatchActors previousBatch);
            BatchActors nextBatch =
                isInsideBoat
                    ? actor.batch
                    : null;
            if (ReferenceEquals(previousBatch, nextBatch))
            {
                return;
            }

            if (previousBatch != null)
            {
                RemoveFromPartition(actor, previousBatch);
                removals++;
            }

            if (nextBatch == null)
            {
                return;
            }

            if (!Partitions.TryGetValue(
                    nextBatch,
                    out Partition partition))
            {
                partition = new Partition();
                Partitions.Add(nextBatch, partition);
            }

            if (partition.Actors.Add(actor))
            {
                partition.Dirty = true;
                ActorPartitions[actor] = nextBatch;
                additions++;
            }
        }
    }

    internal static bool TryGetSnapshot(
        BatchActors batch,
        out Actor[] actors,
        out int count)
    {
        lock (Gate)
        {
            snapshotReads++;
            if (batch == null ||
                !Partitions.TryGetValue(batch, out Partition partition) ||
                partition.Actors.Count == 0)
            {
                actors = Array.Empty<Actor>();
                count = 0;
                return false;
            }

            if (partition.Dirty)
            {
                int required = partition.Actors.Count;
                if (partition.Snapshot.Length < required)
                {
                    int capacity = Math.Max(
                        PerformanceSettings.SimulationBatchSize,
                        required);
                    partition.Snapshot = new Actor[capacity];
                }

                partition.Actors.CopyTo(partition.Snapshot);
                partition.SnapshotCount = required;
                partition.Dirty = false;
            }

            actors = partition.Snapshot;
            count = partition.SnapshotCount;
            return count > 0;
        }
    }

    internal static void RecordProcessed(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (Gate)
        {
            processedActors += count;
        }
    }

    internal static void Reset()
    {
        lock (Gate)
        {
            Partitions.Clear();
            ActorPartitions.Clear();
            additions = 0;
            removals = 0;
            snapshotReads = 0;
            processedActors = 0;
        }
    }

    internal static string GetDiagnostics()
    {
        lock (Gate)
        {
            return
                $"indexed={ActorPartitions.Count} partitions={Partitions.Count} " +
                $"add={additions} remove={removals} " +
                $"reads={snapshotReads} processed={processedActors}";
        }
    }

    private static void RemoveFromPartition(
        Actor actor,
        BatchActors batch)
    {
        ActorPartitions.Remove(actor);
        if (!Partitions.TryGetValue(batch, out Partition partition))
        {
            return;
        }

        if (partition.Actors.Remove(actor))
        {
            partition.Dirty = true;
        }

        if (partition.Actors.Count == 0)
        {
            Partitions.Remove(batch);
        }
    }

    private sealed class ActorReferenceComparer :
        IEqualityComparer<Actor>
    {
        internal static readonly ActorReferenceComparer Instance =
            new();

        public bool Equals(Actor left, Actor right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(Actor actor)
        {
            return RuntimeHelpers.GetHashCode(actor);
        }
    }
}
