using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 并行重建原版 SimObjectsZones 的 tile/chunk 角色成员表。
/// 每个 chunk 只由一个 worker 写入，城市征服与危险区仍按原角色顺序提交。
/// </summary>
internal static class ParallelSimObjectZoneUnits
{
    private const int ParallelThreshold = 1024;

    private static readonly Action<int> rebuildChunkAction =
        RebuildChunk;

    private static List<Actor>[] actorsByChunk =
        Array.Empty<List<Actor>>();
    private static int[] tileMarks = Array.Empty<int>();
    private static MapChunk[] activeChunks;
    private static int preparedGeneration = -1;
    private static int tileMarkGeneration;
    private static int pendingIslandGeneration = -1;
    private static int unitMembershipVersion;
    private static bool statusIndexRebuildPrepared;

    /// <summary>
    /// 原版 chunk.objects.units_all 成员表的提交版本。
    /// 只有 checkUnits 完整结束后才推进，因此读方不会观察到半提交状态。
    /// </summary>
    internal static int UnitMembershipVersion =>
        Volatile.Read(ref unitMembershipVersion);

    internal static void NotifyUnitMembershipRebuilt()
    {
        int version =
            Interlocked.Increment(
                ref unitMembershipVersion);
        NearbyStatusTargetIndex
            .NotifyUnitMembershipRebuilt(
                version,
                statusIndexRebuildPrepared);
        statusIndexRebuildPrepared = false;
    }

    internal static bool TryDeferIslandRebuild(
        IslandsCalculator calculator)
    {
        MapBox world = World.world;
        List<Actor> source =
            world?.units?.getSimpleList();
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            source == null ||
            source.Count < ParallelThreshold)
        {
            return false;
        }

        ListPool<TileIsland> islands = calculator.islands;
        for (int i = 0; i < islands.Count; i++)
        {
            islands[i].actors.Clear();
        }

        pendingIslandGeneration = SimulationTime.Generation;
        return true;
    }

    internal static bool TryRebuild(
        List<WorldTile> tilesToClear)
    {
        statusIndexRebuildPrepared = false;
        MapBox world = World.world;
        List<Actor> source =
            world?.units?.getSimpleList();
        MapChunk[] chunks =
            world?.map_chunk_manager?.chunks;
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            source == null ||
            chunks == null ||
            source.Count < ParallelThreshold)
        {
            return false;
        }

        EnsureChunkLists(chunks, source.Count);
        for (int i = 0; i < actorsByChunk.Length; i++)
        {
            actorsByChunk[i].Clear();
        }

        NearbyStatusTargetIndex
            .BeginUnitMembershipRebuild();
        statusIndexRebuildPrepared = true;
        try
        {
            int tileMark =
                NextTileMark(world.tiles_list.Length);
            bool rebuildIslands =
                pendingIslandGeneration ==
                SimulationTime.Generation;
            int aliveCount = 0;
            bool benchmark = Bench.bench_enabled;
            if (benchmark)
            {
                Bench.bench(
                    "checkUnits.parallel_prepare",
                    "sim_zones");
            }

            for (int i = 0; i < source.Count; i++)
            {
                Actor actor = source[i];
                if (!actor.isAlive())
                {
                    continue;
                }

                WorldTile tile = actor.current_tile;
                aliveCount++;
                if (rebuildIslands)
                {
                    tile.region.island.actors.Add(actor);
                }

                List<Actor> chunkActors =
                    actorsByChunk[tile.chunk.id];
                int unitIndex = chunkActors.Count;
                chunkActors.Add(actor);
                NearbyStatusTargetIndex
                    .AddUnitMembership(
                        actor,
                        tile.chunk,
                        unitIndex);
                int tileId = tile.tile_id;
                if (tileMarks[tileId] != tileMark)
                {
                    tileMarks[tileId] = tileMark;
                    tilesToClear.Add(tile);
                }
            }

            pendingIslandGeneration = -1;
            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.parallel_prepare",
                    "sim_zones",
                    pSaveCounter: true,
                    aliveCount);
                Bench.bench(
                    "checkUnits.parallel_commit",
                    "sim_zones");
            }

            activeChunks = chunks;
            try
            {
                SimulationWorkerPool.Instance
                    .RunIndexed(
                        0,
                        chunks.Length,
                        rebuildChunkAction);
            }
            finally
            {
                activeChunks = null;
            }

            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.parallel_commit",
                    "sim_zones",
                    pSaveCounter: true,
                    aliveCount);
                Bench.bench(
                    "checkUnits.city_membership",
                    "sim_zones");
            }

            for (int i = 0;
                 i < source.Count;
                 i++)
            {
                Actor actor = source[i];
                if (actor.isAlive())
                {
                    UpdateCityMembership(actor);
                }
            }

            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.city_membership",
                    "sim_zones",
                    pSaveCounter: true,
                    aliveCount);
            }

            return true;
        }
        catch
        {
            NearbyStatusTargetIndex
                .AbortUnitMembershipRebuild();
            statusIndexRebuildPrepared = false;
            throw;
        }
    }

    private static void EnsureChunkLists(
        MapChunk[] chunks,
        int actorCount)
    {
        int generation = SimulationTime.Generation;
        if (preparedGeneration == generation &&
            actorsByChunk.Length == chunks.Length)
        {
            return;
        }

        preparedGeneration = generation;
        tileMarkGeneration = 0;
        int capacity = Math.Max(
            16,
            actorCount / Math.Max(1, chunks.Length));
        actorsByChunk = new List<Actor>[chunks.Length];
        for (int i = 0; i < actorsByChunk.Length; i++)
        {
            actorsByChunk[i] =
                new List<Actor>(capacity);
        }
    }

    private static int NextTileMark(int tileCount)
    {
        if (tileMarks.Length != tileCount)
        {
            tileMarks = new int[tileCount];
            tileMarkGeneration = 0;
        }

        int next = unchecked(++tileMarkGeneration);
        if (next != 0)
        {
            return next;
        }

        Array.Clear(tileMarks, 0, tileMarks.Length);
        tileMarkGeneration = 1;
        return tileMarkGeneration;
    }

    private static void RebuildChunk(int chunkIndex)
    {
        MapChunk chunk = activeChunks[chunkIndex];
        List<Actor> actors = actorsByChunk[chunkIndex];
        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            actor.current_tile.addUnit(actor);
            chunk.objects.addActor(actor);
        }
    }

    private static void UpdateCityMembership(Actor actor)
    {
        WorldTile tile = actor.current_tile;
        City city = tile.zone_city;
        if (city == null || actor.isInsideSomething())
        {
            return;
        }

        Kingdom kingdom = actor.kingdom;
        if (actor.profession_asset.can_capture)
        {
            city.updateConquest(actor);
        }
        else if (kingdom.isCiv())
        {
            return;
        }

        TileZone zone = tile.zone;
        if (!city.danger_zones.Contains(zone) &&
            (!kingdom.isMobs() ||
             !WorldLawLibrary.world_law_peaceful_monsters
                 .isEnabled()) &&
            kingdom != city.kingdom &&
            kingdom.asset.count_as_danger &&
            kingdom.isEnemy(city.kingdom))
        {
            city.danger_zones.Add(zone);
        }
    }
}
