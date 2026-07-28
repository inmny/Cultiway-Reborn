using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 缓存 chunk 内按岛屿划分的空闲地面。原版每次查询都会随机遍历
/// 周围 chunk、region 和 tile；大量角色同时执行情绪行为时会重复扫描
/// 完全相同的地图数据。
/// </summary>
internal static class FreeTileSearchIndex
{
    private static readonly Dictionary<
        MapChunk,
        Dictionary<TileIsland, List<WorldTile>>> TilesByChunk = new();
    private static readonly Stack<Dictionary<TileIsland, List<WorldTile>>>
        IslandMapPool = new();
    private static readonly Stack<List<WorldTile>> TileListPool = new();

    [ThreadStatic]
    private static MapChunk[] queryChunkBuffer;

    private static int indexedGeneration = -1;
    private static long indexedSimulationTimeBits;
    private static bool indexAvailable;

    private static long queries;
    private static long hits;
    private static long fallbacks;
    private static long chunkBuilds;
    private static long tilesScanned;
    private static long candidatesBuilt;
    private static long totalSearchTicks;
    private static long maximumSearchTicks;
    private static long lastSearchTicks;

    internal static bool TryFind(
        WorldTile origin,
        out WorldTile result)
    {
        long startedAt = Bench.bench_enabled
            ? Stopwatch.GetTimestamp()
            : 0L;
        Interlocked.Increment(ref queries);
        result = null;
        if (origin?.chunk == null ||
            origin.region?.island == null)
        {
            Interlocked.Increment(ref fallbacks);
            RecordSearchDuration(startedAt);
            return false;
        }

        EnsureCurrentTick();
        MapChunk[] chunks =
            queryChunkBuffer ??= new MapChunk[9];
        chunks[0] = origin.chunk;
        int chunkCount = 1;
        MapChunk[] neighbours = origin.chunk.neighbours_all;
        for (int i = 0;
             i < neighbours.Length &&
             chunkCount < chunks.Length;
             i++)
        {
            chunks[chunkCount++] = neighbours[i];
        }

        int chunkOffset = Randy.randomInt(0, chunkCount);
        TileIsland island = origin.region.island;
        for (int i = 0; i < chunkCount; i++)
        {
            MapChunk chunk =
                chunks[(i + chunkOffset) % chunkCount];
            List<WorldTile> candidates =
                GetCandidates(chunk, island);
            if (candidates.Count == 0)
            {
                continue;
            }

            int tileOffset =
                Randy.randomInt(0, candidates.Count);
            for (int j = 0; j < candidates.Count; j++)
            {
                WorldTile tile =
                    candidates[(j + tileOffset) %
                               candidates.Count];
                if (!IsFreeFor(tile, origin))
                {
                    continue;
                }

                result = tile;
                Interlocked.Increment(ref hits);
                RecordSearchDuration(startedAt);
                return true;
            }
        }

        // 缓存没有可用项时交回原版扫描，以覆盖本 tick 内刚变为空闲
        // 或岛屿/chunk 结构刚发生变化的极端情况。
        Interlocked.Increment(ref fallbacks);
        RecordSearchDuration(startedAt);
        return false;
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "queries={0} hits={1} fallbacks={2} chunk_builds={3} " +
            "tiles_scanned={4} candidates={5} " +
            "search={6:0.000}ms(avg={7:0.000},max={8:0.000})",
            Interlocked.Read(ref queries),
            Interlocked.Read(ref hits),
            Interlocked.Read(ref fallbacks),
            Interlocked.Read(ref chunkBuilds),
            Interlocked.Read(ref tilesScanned),
            Interlocked.Read(ref candidatesBuilt),
            TicksToMilliseconds(
                Interlocked.Read(ref lastSearchTicks)),
            TicksToMilliseconds(
                Interlocked.Read(ref totalSearchTicks)) /
            Math.Max(1L, Interlocked.Read(ref queries)),
            TicksToMilliseconds(
                Interlocked.Read(ref maximumSearchTicks)));
    }

    internal static void Reset()
    {
        RecycleCache();
        indexedGeneration = -1;
        indexedSimulationTimeBits = 0L;
        indexAvailable = false;
    }

    private static void EnsureCurrentTick()
    {
        long simulationTimeBits =
            BitConverter.DoubleToInt64Bits(
                SimulationTime.DiagnosticTime);
        if (indexAvailable &&
            indexedGeneration == SimulationTime.Generation &&
            indexedSimulationTimeBits == simulationTimeBits)
        {
            return;
        }

        RecycleCache();
        indexedGeneration = SimulationTime.Generation;
        indexedSimulationTimeBits = simulationTimeBits;
        indexAvailable = true;
    }

    private static List<WorldTile> GetCandidates(
        MapChunk chunk,
        TileIsland island)
    {
        if (!TilesByChunk.TryGetValue(
                chunk,
                out Dictionary<TileIsland, List<WorldTile>>
                    byIsland))
        {
            byIsland = IslandMapPool.Count == 0
                ? new Dictionary<TileIsland, List<WorldTile>>()
                : IslandMapPool.Pop();
            TilesByChunk.Add(chunk, byIsland);
        }

        if (byIsland.TryGetValue(
                island,
                out List<WorldTile> candidates))
        {
            return candidates;
        }

        candidates = TileListPool.Count == 0
            ? new List<WorldTile>(128)
            : TileListPool.Pop();
        WorldTile[] tiles = chunk.tiles;
        Interlocked.Add(ref tilesScanned, tiles.Length);
        for (int i = 0; i < tiles.Length; i++)
        {
            WorldTile tile = tiles[i];
            if (tile?.region?.island == island &&
                tile.Type.ground &&
                !tile.hasBuilding())
            {
                candidates.Add(tile);
            }
        }

        byIsland.Add(island, candidates);
        Interlocked.Increment(ref chunkBuilds);
        Interlocked.Add(
            ref candidatesBuilt,
            candidates.Count);
        return candidates;
    }

    private static bool IsFreeFor(
        WorldTile tile,
        WorldTile origin)
    {
        return tile != null &&
               tile.Type.ground &&
               !tile.hasBuilding() &&
               tile.isSameIsland(origin);
    }

    private static void RecycleCache()
    {
        foreach (Dictionary<TileIsland, List<WorldTile>>
                 byIsland in TilesByChunk.Values)
        {
            foreach (List<WorldTile> candidates in
                     byIsland.Values)
            {
                candidates.Clear();
                TileListPool.Push(candidates);
            }

            byIsland.Clear();
            IslandMapPool.Push(byIsland);
        }

        TilesByChunk.Clear();
    }

    private static void RecordSearchDuration(long startedAt)
    {
        if (startedAt <= 0L)
        {
            return;
        }

        long elapsedTicks =
            Stopwatch.GetTimestamp() - startedAt;
        Interlocked.Exchange(ref lastSearchTicks, elapsedTicks);
        Interlocked.Add(ref totalSearchTicks, elapsedTicks);
        long maximum = Interlocked.Read(ref maximumSearchTicks);
        while (elapsedTicks > maximum)
        {
            long observed = Interlocked.CompareExchange(
                ref maximumSearchTicks,
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
