using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 按原版 chunk 成员表汇总角色状态，用于快速排除附近不可能存在目标状态的查询。
/// 索引只给出“可能存在”，真正存在时仍由原版行为完成选择，避免改变匹配语义。
/// </summary>
internal static class NearbyStatusTargetIndex
{
    private static readonly Dictionary<MapChunk, HashSet<string>>
        StatusIdsByChunk = new();
    private static readonly Stack<HashSet<string>> StatusSetPool = new();
    private static readonly HashSet<string> TrackedStatusIds =
        new(StringComparer.Ordinal);
    private static readonly HashSet<string> SpatiallyUncertainStatusIds =
        new(StringComparer.Ordinal);

    private static int indexedGeneration = -1;
    private static long indexedSimulationTimeBits;
    private static bool indexAvailable;

    private static long queries;
    private static long fastNegativeQueries;
    private static long possibleQueries;
    private static long rebuilds;
    private static long totalBuildTicks;
    private static long maximumBuildTicks;
    private static long lastBuildTicks;
    private static long statusChecks;
    private static int indexedChunkCount;
    private static int indexedStatusEntryCount;
    private static int uncertainStatusCount;

    internal static bool MayContainNearby(
        Actor actor,
        string[] statusIds)
    {
        Interlocked.Increment(ref queries);
        if (actor?.current_tile?.chunk == null ||
            statusIds == null ||
            statusIds.Length == 0 ||
            World.world?.map_chunk_manager == null)
        {
            Interlocked.Increment(ref possibleQueries);
            return true;
        }

        RegisterTrackedStatusIds(statusIds);
        EnsureBuilt();
        for (int i = 0; i < statusIds.Length; i++)
        {
            if (SpatiallyUncertainStatusIds.Contains(statusIds[i]))
            {
                Interlocked.Increment(ref possibleQueries);
                return true;
            }
        }

        MapChunk origin = actor.current_tile.chunk;
        MapChunkManager manager = World.world.map_chunk_manager;
        for (int x = origin.x - 1; x <= origin.x + 1; x++)
        {
            for (int y = origin.y - 1; y <= origin.y + 1; y++)
            {
                MapChunk chunk = manager.get(x, y);
                if (chunk == null ||
                    !StatusIdsByChunk.TryGetValue(
                        chunk,
                        out HashSet<string> chunkStatusIds))
                {
                    continue;
                }

                for (int i = 0; i < statusIds.Length; i++)
                {
                    if (chunkStatusIds.Contains(statusIds[i]))
                    {
                        Interlocked.Increment(ref possibleQueries);
                        return true;
                    }
                }
            }
        }

        Interlocked.Increment(ref fastNegativeQueries);
        return false;
    }

    /// <summary>
    /// 索引构建后新增的状态没有可靠的原版 chunk 成员位置。
    /// 本 tick 对该状态关闭空间排除，下一 tick 重建后自动恢复。
    /// </summary>
    internal static void NotifyStatusAdded(
        BaseSimObject simObject,
        StatusAsset statusAsset)
    {
        if (!indexAvailable ||
            simObject is not Actor ||
            statusAsset == null ||
            !IsCurrentIndex())
        {
            return;
        }

        SpatiallyUncertainStatusIds.Add(statusAsset.id);
        Interlocked.Exchange(
            ref uncertainStatusCount,
            SpatiallyUncertainStatusIds.Count);
    }

    internal static string GetDiagnostics()
    {
        long buildCount = Interlocked.Read(ref rebuilds);
        return string.Format(
            CultureInfo.InvariantCulture,
            "queries={0} fast_negative={1} possible={2} " +
            "rebuilds={3} chunks={4} status_entries={5} uncertain={6} " +
            "status_checks={7} build={8:0.000}ms(avg={9:0.000},max={10:0.000})",
            Interlocked.Read(ref queries),
            Interlocked.Read(ref fastNegativeQueries),
            Interlocked.Read(ref possibleQueries),
            buildCount,
            Volatile.Read(ref indexedChunkCount),
            Volatile.Read(ref indexedStatusEntryCount),
            Volatile.Read(ref uncertainStatusCount),
            Interlocked.Read(ref statusChecks),
            TicksToMilliseconds(Interlocked.Read(ref lastBuildTicks)),
            buildCount == 0L
                ? 0.0
                : TicksToMilliseconds(
                    Interlocked.Read(ref totalBuildTicks)) / buildCount,
            TicksToMilliseconds(
                Interlocked.Read(ref maximumBuildTicks)));
    }

    internal static void Reset()
    {
        RecycleStatusSets();
        TrackedStatusIds.Clear();
        SpatiallyUncertainStatusIds.Clear();
        indexedGeneration = -1;
        indexedSimulationTimeBits = 0L;
        indexAvailable = false;
        Volatile.Write(ref indexedChunkCount, 0);
        Volatile.Write(ref indexedStatusEntryCount, 0);
        Volatile.Write(ref uncertainStatusCount, 0);
    }

    private static void EnsureBuilt()
    {
        if (IsCurrentIndex())
        {
            return;
        }

        long startedAt = Stopwatch.GetTimestamp();
        RecycleStatusSets();
        SpatiallyUncertainStatusIds.Clear();

        MapBox world = World.world;
        long checkedStatuses = 0L;
        int statusEntries = 0;
        if (world?.statuses?.Count > 0 &&
            world.map_chunk_manager != null)
        {
            foreach (Status status in world.statuses)
            {
                checkedStatuses++;
                string statusId = status?.asset?.id;
                if (statusId == null ||
                    !TrackedStatusIds.Contains(statusId) ||
                    status.sim_object is not Actor actor ||
                    !actor.isAlive() ||
                    actor.current_tile?.chunk == null ||
                    !actor.hasStatus(statusId))
                {
                    continue;
                }

                // 原版 chunk 成员表按 0.1 模拟秒重建，而角色位置每 tick
                // 都会变化。向相邻 chunk 保守扩张只会增加原版回退扫描，
                // 不会让索引直接返回一个原版找不到的目标。
                statusEntries += AddStatusMarker(
                    actor.current_tile.chunk,
                    statusId);
                MapChunk[] neighbours =
                    actor.current_tile.chunk.neighbours_all;
                for (int i = 0; i < neighbours.Length; i++)
                {
                    statusEntries += AddStatusMarker(
                        neighbours[i],
                        statusId);
                }
            }
        }

        indexedGeneration = SimulationTime.Generation;
        indexedSimulationTimeBits =
            BitConverter.DoubleToInt64Bits(
                SimulationTime.DiagnosticTime);
        indexAvailable = true;
        Interlocked.Add(ref statusChecks, checkedStatuses);
        Volatile.Write(
            ref indexedChunkCount,
            StatusIdsByChunk.Count);
        Volatile.Write(
            ref indexedStatusEntryCount,
            statusEntries);
        Volatile.Write(ref uncertainStatusCount, 0);
        RecordBuildDuration(
            Stopwatch.GetTimestamp() - startedAt);
    }

    private static bool IsCurrentIndex()
    {
        return indexAvailable &&
               indexedGeneration == SimulationTime.Generation &&
               indexedSimulationTimeBits ==
               BitConverter.DoubleToInt64Bits(
                   SimulationTime.DiagnosticTime);
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
                added = true;
            }
        }

        if (added)
        {
            indexAvailable = false;
        }
    }

    private static int AddStatusMarker(
        MapChunk chunk,
        string statusId)
    {
        if (chunk == null)
        {
            return 0;
        }

        if (!StatusIdsByChunk.TryGetValue(
                chunk,
                out HashSet<string> statusIds))
        {
            statusIds = RentStatusSet();
            StatusIdsByChunk.Add(chunk, statusIds);
        }

        return statusIds.Add(statusId) ? 1 : 0;
    }

    private static HashSet<string> RentStatusSet()
    {
        return StatusSetPool.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : StatusSetPool.Pop();
    }

    private static void RecycleStatusSets()
    {
        foreach (HashSet<string> statusIds in
                 StatusIdsByChunk.Values)
        {
            statusIds.Clear();
            StatusSetPool.Push(statusIds);
        }

        StatusIdsByChunk.Clear();
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
}
