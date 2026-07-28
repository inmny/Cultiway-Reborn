using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 按原版 chunk 成员顺序索引拥有目标状态的角色。
/// 搜索仍消费原版相同的随机数并遵循相同候选顺序，但不再为每次查询
/// 扫描附近 chunk 中的全部角色。
/// </summary>
internal static class NearbyStatusTargetIndex
{
    private const int SparseMembershipScanThreshold = 64;

    private static readonly Dictionary<MapChunk, List<IndexedActor>>
        ActorsByChunk = new();
    private static readonly Stack<List<IndexedActor>>
        ActorListPool = new();
    private static readonly HashSet<Actor> IndexedActors = new();
    private static readonly Dictionary<Actor, MapChunk>
        IndexedActorChunks = new();
    private static readonly HashSet<string> TrackedStatusIds =
        new(StringComparer.Ordinal);
    private static readonly HashSet<string> GlobalStatusIds =
        new(StringComparer.Ordinal);

    private static int indexedGeneration = -1;
    private static int indexedUnitMembershipVersion = -1;
    private static bool indexAvailable;

    private static long queries;
    private static long handledQueries;
    private static long fastNegativeQueries;
    private static long foundQueries;
    private static long fallbackQueries;
    private static long rebuilds;
    private static long totalBuildTicks;
    private static long maximumBuildTicks;
    private static long lastBuildTicks;
    private static long statusChecks;
    private static long unitChecks;
    private static long incrementalAdds;
    private static long incrementalRemoves;
    private static int indexedChunkCount;
    private static int indexedActorEntryCount;

    /// <summary>
    /// 返回 true 表示已完整执行原版搜索语义，result 可以为空；
    /// 返回 false 表示索引无法证明空间信息稳定，调用方必须执行原版搜索。
    /// </summary>
    internal static bool TryFindClosest(
        Actor actor,
        string[] statusIds,
        out Actor result)
    {
        result = null;
        Interlocked.Increment(ref queries);
        if (actor?.current_tile?.chunk == null ||
            statusIds == null ||
            statusIds.Length == 0 ||
            World.world?.map_chunk_manager == null)
        {
            Interlocked.Increment(ref fallbackQueries);
            return false;
        }

        RegisterTrackedStatusIds(statusIds);
        EnsureBuilt();

        bool existsGlobally = false;
        for (int i = 0; i < statusIds.Length; i++)
        {
            if (GlobalStatusIds.Contains(statusIds[i]))
            {
                existsGlobally = true;
                break;
            }
        }

        result = FindClosest(
            actor,
            statusIds,
            existsGlobally);
        Interlocked.Increment(ref handledQueries);
        if (result == null)
        {
            Interlocked.Increment(ref fastNegativeQueries);
        }
        else
        {
            Interlocked.Increment(ref foundQueries);
        }

        return true;
    }

    /// <summary>
    /// 索引构建后新增的目标状态立即并入当前 tick 的稀疏索引。
    /// 角色可能仍挂在旧 chunk 成员表中，因此按原版实际成员表定位，
    /// 而不是直接相信 current_tile.chunk。
    /// </summary>
    internal static void NotifyStatusAdded(
        BaseSimObject simObject,
        StatusAsset statusAsset)
    {
        if (simObject is not Actor actor ||
            statusAsset == null ||
            !TrackedStatusIds.Contains(statusAsset.id) ||
            !actor.isAlive())
        {
            return;
        }

        bool newlyIndexed = IndexedActors.Add(actor);
        GlobalStatusIds.Add(statusAsset.id);
        if (!newlyIndexed ||
            !IsCurrentIndex())
        {
            return;
        }

        long checkedUnits = 0L;
        if (TryAddCurrentMembership(
                World.world,
                actor,
                ref checkedUnits))
        {
            Interlocked.Increment(ref incrementalAdds);
            UpdateIndexedEntryDiagnostics();
        }

        Interlocked.Add(ref unitChecks, checkedUnits);
    }

    internal static void NotifyStatusRemoved(
        BaseSimObject simObject,
        StatusAsset statusAsset)
    {
        if (simObject is not Actor actor ||
            statusAsset == null ||
            !TrackedStatusIds.Contains(statusAsset.id))
        {
            return;
        }

        if (!HasAnyTrackedStatus(actor))
        {
            if (IndexedActors.Remove(actor) &&
                IsCurrentIndex() &&
                RemoveActorMembership(actor))
            {
                Interlocked.Increment(
                    ref incrementalRemoves);
            }
        }
    }

    internal static void NotifyAllStatusesRemoved(
        BaseSimObject simObject)
    {
        if (simObject is not Actor actor ||
            !IndexedActors.Remove(actor))
        {
            return;
        }

        if (IsCurrentIndex() &&
            RemoveActorMembership(actor))
        {
            Interlocked.Increment(ref incrementalRemoves);
        }
    }

    internal static string GetDiagnostics()
    {
        long buildCount = Interlocked.Read(ref rebuilds);
        return string.Format(
            CultureInfo.InvariantCulture,
            "queries={0} handled={1} fast_negative={2} found={3} " +
            "fallback={4} rebuilds={5} chunks={6} " +
            "actor_entries={7} tracked_actors={13} " +
            "status_checks={8} unit_checks={9} " +
            "incremental={14}/{15}(add/remove) " +
            "build={10:0.000}ms(avg={11:0.000},max={12:0.000})",
            Interlocked.Read(ref queries),
            Interlocked.Read(ref handledQueries),
            Interlocked.Read(ref fastNegativeQueries),
            Interlocked.Read(ref foundQueries),
            Interlocked.Read(ref fallbackQueries),
            buildCount,
            Volatile.Read(ref indexedChunkCount),
            Volatile.Read(ref indexedActorEntryCount),
            Interlocked.Read(ref statusChecks),
            Interlocked.Read(ref unitChecks),
            TicksToMilliseconds(Interlocked.Read(ref lastBuildTicks)),
            buildCount == 0L
                ? 0.0
                : TicksToMilliseconds(
                    Interlocked.Read(ref totalBuildTicks)) / buildCount,
            TicksToMilliseconds(
                Interlocked.Read(ref maximumBuildTicks)),
            IndexedActors.Count,
            Interlocked.Read(ref incrementalAdds),
            Interlocked.Read(ref incrementalRemoves));
    }

    internal static void Reset()
    {
        RecycleActorLists();
        IndexedActors.Clear();
        TrackedStatusIds.Clear();
        GlobalStatusIds.Clear();
        indexedGeneration = -1;
        indexedUnitMembershipVersion = -1;
        indexAvailable = false;
        Volatile.Write(ref indexedChunkCount, 0);
        Volatile.Write(ref indexedActorEntryCount, 0);
    }

    private static Actor FindClosest(
        Actor self,
        string[] statusIds,
        bool existsGlobally)
    {
        bool randomizeUnits = Randy.randomBool();
        MapChunk[] chunks =
            ChunkWindowIndex.Get(self.current_tile.chunk, 1);
        int chunkCount = chunks.Length;
        int chunkOffset = Randy.randomInt(0, chunkCount);
        int closestDistanceSquared = int.MaxValue;
        Actor closest = null;

        for (int i = 0; i < chunkCount; i++)
        {
            MapChunk chunk =
                chunks[(i + chunkOffset) % chunkCount];
            List<Actor> units = chunk.objects.units_all;
            int unitOffset = randomizeUnits
                ? Randy.randomInt(0, units.Count)
                : 0;
            if (!existsGlobally ||
                !ActorsByChunk.TryGetValue(
                    chunk,
                    out List<IndexedActor> candidates))
            {
                continue;
            }

            int candidateStart = randomizeUnits
                ? LowerBound(candidates, unitOffset)
                : 0;
            int candidateCount = candidates.Count;
            if (candidateStart == candidateCount)
            {
                candidateStart = 0;
            }

            for (int j = 0; j < candidateCount; j++)
            {
                int candidateIndex = randomizeUnits
                    ? (candidateStart + j) % candidateCount
                    : j;
                Actor target = candidates[candidateIndex].Actor;
                if (!target.isAlive() ||
                    target == self)
                {
                    continue;
                }

                int distanceSquared = Toolbox.SquaredDistTile(
                    target.current_tile,
                    self.current_tile);
                if (distanceSquared >= closestDistanceSquared ||
                    !self.isSameIslandAs(target) ||
                    !target.hasAnyStatusEffect() ||
                    !HasRequestedStatus(target, statusIds))
                {
                    continue;
                }

                closestDistanceSquared = distanceSquared;
                closest = target;
                if (randomizeUnits || Randy.randomBool())
                {
                    return closest;
                }
            }
        }

        return closest;
    }

    private static bool HasRequestedStatus(
        Actor actor,
        string[] statusIds)
    {
        for (int i = 0; i < statusIds.Length; i++)
        {
            if (actor.hasStatus(statusIds[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int LowerBound(
        List<IndexedActor> candidates,
        int unitOffset)
    {
        int low = 0;
        int high = candidates.Count;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (candidates[middle].UnitIndex < unitOffset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static void EnsureBuilt()
    {
        if (IsCurrentIndex())
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        RecycleActorLists();
        GlobalStatusIds.Clear();

        MapBox world = World.world;
        long checkedUnits = 0L;
        int actorEntries = 0;
        RemoveInvalidTrackedActors();
        RefreshGlobalStatusIds();
        if (IndexedActors.Count > 0 &&
            world.map_chunk_manager != null)
        {
            if (IndexedActors.Count <=
                SparseMembershipScanThreshold)
            {
                actorEntries = BuildSparseMembership(
                    world,
                    ref checkedUnits);
            }
            else
            {
                actorEntries = BuildDenseMembership(
                    world,
                    ref checkedUnits);
            }
        }

        indexedGeneration = SimulationTime.Generation;
        indexedUnitMembershipVersion =
            ParallelSimObjectZoneUnits
                .UnitMembershipVersion;
        indexAvailable = true;
        Interlocked.Add(ref unitChecks, checkedUnits);
        Volatile.Write(
            ref indexedChunkCount,
            ActorsByChunk.Count);
        Volatile.Write(
            ref indexedActorEntryCount,
            actorEntries);
        RecordBuildDuration(
            Stopwatch.GetTimestamp() - startedAt);
    }

    private static int BuildSparseMembership(
        MapBox world,
        ref long checkedUnits)
    {
        int actorEntries = 0;
        foreach (Actor actor in IndexedActors)
        {
            if (TryAddCurrentMembership(
                    world,
                    actor,
                    ref checkedUnits))
            {
                actorEntries++;
            }
        }

        return actorEntries;
    }

    private static int BuildDenseMembership(
        MapBox world,
        ref long checkedUnits)
    {
        int actorEntries = 0;
        MapChunk[] chunks =
            world.map_chunk_manager.chunks;
        for (int chunkIndex = 0;
             chunkIndex < chunks.Length;
             chunkIndex++)
        {
            MapChunk chunk = chunks[chunkIndex];
            List<Actor> units = chunk.objects.units_all;
            int count = units.Count;
            checkedUnits += count;
            List<IndexedActor> candidates = null;
            for (int unitIndex = 0;
                 unitIndex < count;
                 unitIndex++)
            {
                Actor actor = units[unitIndex];
                if (!IndexedActors.Contains(actor))
                {
                    continue;
                }

                candidates ??= RentActorList();
                candidates.Add(
                    new IndexedActor(actor, unitIndex));
                IndexedActorChunks.Add(actor, chunk);
                actorEntries++;
            }

            if (candidates != null)
            {
                ActorsByChunk.Add(chunk, candidates);
            }
        }

        return actorEntries;
    }

    private static bool TryAddCurrentMembership(
        MapBox world,
        Actor actor,
        ref long checkedUnits)
    {
        MapChunk origin = actor.current_tile?.chunk;
        if (origin != null)
        {
            if (TryAddActorMembership(
                    actor,
                    origin,
                    ref checkedUnits))
            {
                return true;
            }

            MapChunk[] nearby =
                ChunkWindowIndex.Get(origin, 1);
            for (int i = 0; i < nearby.Length; i++)
            {
                if (ReferenceEquals(nearby[i], origin))
                {
                    continue;
                }

                if (TryAddActorMembership(
                        actor,
                        nearby[i],
                        ref checkedUnits))
                {
                    return true;
                }
            }
        }

        // 传送或 chunk 成员表延迟过久时，邻域可能找不到角色。
        // 极端路径才全图兜底，以保持与原版成员表完全一致。
        MapChunk[] allChunks =
            world.map_chunk_manager.chunks;
        for (int i = 0; i < allChunks.Length; i++)
        {
            if (TryAddActorMembership(
                    actor,
                    allChunks[i],
                    ref checkedUnits))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAddActorMembership(
        Actor actor,
        MapChunk chunk,
        ref long checkedUnits)
    {
        List<Actor> units = chunk.objects.units_all;
        int count = units.Count;
        checkedUnits += count;
        for (int unitIndex = 0;
             unitIndex < count;
             unitIndex++)
        {
            if (!ReferenceEquals(units[unitIndex], actor))
            {
                continue;
            }

            if (!ActorsByChunk.TryGetValue(
                    chunk,
                    out List<IndexedActor> candidates))
            {
                candidates = RentActorList();
                ActorsByChunk.Add(chunk, candidates);
            }

            int insertIndex =
                LowerBound(candidates, unitIndex);
            candidates.Insert(
                insertIndex,
                new IndexedActor(actor, unitIndex));
            IndexedActorChunks.Add(actor, chunk);
            return true;
        }

        return false;
    }

    private static bool RemoveActorMembership(Actor actor)
    {
        if (!IndexedActorChunks.TryGetValue(
                actor,
                out MapChunk chunk) ||
            !ActorsByChunk.TryGetValue(
                chunk,
                out List<IndexedActor> candidates))
        {
            return false;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (!ReferenceEquals(
                    candidates[i].Actor,
                    actor))
            {
                continue;
            }

            candidates.RemoveAt(i);
            IndexedActorChunks.Remove(actor);
            if (candidates.Count == 0)
            {
                ActorsByChunk.Remove(chunk);
                candidates.Clear();
                ActorListPool.Push(candidates);
            }

            UpdateIndexedEntryDiagnostics();
            return true;
        }

        return false;
    }

    private static void UpdateIndexedEntryDiagnostics()
    {
        Volatile.Write(
            ref indexedChunkCount,
            ActorsByChunk.Count);
        Volatile.Write(
            ref indexedActorEntryCount,
            IndexedActorChunks.Count);
    }

    private static void RemoveInvalidTrackedActors()
    {
        IndexedActors.RemoveWhere(
            actor =>
                actor == null ||
                !actor.isAlive() ||
                !HasAnyTrackedStatus(actor));
    }

    private static void RefreshGlobalStatusIds()
    {
        foreach (Actor actor in IndexedActors)
        {
            foreach (string statusId in TrackedStatusIds)
            {
                Interlocked.Increment(ref statusChecks);
                if (actor.hasStatus(statusId))
                {
                    GlobalStatusIds.Add(statusId);
                }
            }
        }
    }

    private static bool HasAnyTrackedStatus(Actor actor)
    {
        foreach (string statusId in TrackedStatusIds)
        {
            if (actor.hasStatus(statusId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCurrentIndex()
    {
        return indexAvailable &&
               indexedGeneration == SimulationTime.Generation &&
               indexedUnitMembershipVersion ==
               ParallelSimObjectZoneUnits
                   .UnitMembershipVersion;
    }

    private static void RegisterTrackedStatusIds(
        string[] statusIds)
    {
        bool added = false;
        for (int i = 0; i < statusIds.Length; i++)
        {
            string statusId = statusIds[i];
            if (statusId != null &&
                TrackedStatusIds.Add(statusId))
            {
                InitializeTrackedStatus(statusId);
                added = true;
            }
        }

        if (added)
        {
            indexAvailable = false;
        }
    }

    private static void InitializeTrackedStatus(string statusId)
    {
        StatusManager manager = World.world?.statuses;
        if (manager == null)
        {
            return;
        }

        foreach (Status status in manager)
        {
            Interlocked.Increment(ref statusChecks);
            if (status?.asset?.id == statusId &&
                status.sim_object is Actor actor &&
                actor.isAlive() &&
                actor.hasStatus(statusId))
            {
                IndexedActors.Add(actor);
            }
        }
    }

    private static List<IndexedActor> RentActorList()
    {
        return ActorListPool.Count == 0
            ? new List<IndexedActor>(4)
            : ActorListPool.Pop();
    }

    private static void RecycleActorLists()
    {
        foreach (List<IndexedActor> actors in
                 ActorsByChunk.Values)
        {
            actors.Clear();
            ActorListPool.Push(actors);
        }

        ActorsByChunk.Clear();
        IndexedActorChunks.Clear();
    }

    private static void RecordBuildDuration(long elapsedTicks)
    {
        Interlocked.Exchange(ref lastBuildTicks, elapsedTicks);
        Interlocked.Add(ref totalBuildTicks, elapsedTicks);
        Interlocked.Increment(ref rebuilds);
        long maximum = Interlocked.Read(ref maximumBuildTicks);
        while (elapsedTicks > maximum)
        {
            long observed = Interlocked.CompareExchange(
                ref maximumBuildTicks,
                elapsedTicks,
                maximum);
            if (observed == maximum)
            {
                break;
            }

            maximum = observed;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private readonly struct IndexedActor
    {
        internal IndexedActor(
            Actor actor,
            int unitIndex)
        {
            Actor = actor;
            UnitIndex = unitIndex;
        }

        internal Actor Actor { get; }
        internal int UnitIndex { get; }
    }
}
