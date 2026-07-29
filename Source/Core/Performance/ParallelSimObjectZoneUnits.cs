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
    private const int ParallelTileClearThreshold = 256;
    private const int TilesPerChunk = 256;

    private static readonly Action<int> rebuildChunkAction =
        RebuildChunk;
    private static readonly Action<int> classifyUnitRangeAction =
        ClassifyUnitRange;
    private static readonly Action<int> scatterUnitRangeAction =
        ScatterUnitRange;
    private static readonly Action<int> clearTileAction =
        ClearTile;
    private static readonly Action<int> clearChunkAction =
        ClearChunk;

    private static Actor[][] actorsByChunk =
        Array.Empty<Actor[]>();
    private static int[] actorCountsByChunk =
        Array.Empty<int>();
    private static Actor[][] statusActorsByChunk =
        Array.Empty<Actor[]>();
    private static int[][] statusUnitIndicesByChunk =
        Array.Empty<int[]>();
    private static int[] statusActorCountsByChunk =
        Array.Empty<int>();
    private static List<WorldTile>[] occupiedTilesByChunk =
        Array.Empty<List<WorldTile>>();
    private static int[] unitChunkIndices =
        Array.Empty<int>();
    private static byte[] cityMembershipFlags =
        Array.Empty<byte>();
    private static Actor[] cityMembershipActors =
        Array.Empty<Actor>();
    private static int cityMembershipActorCount;
    private static int[] workChunkCounts =
        Array.Empty<int>();
    private static int[] workChunkOffsets =
        Array.Empty<int>();
    private static int[] workCityCounts =
        Array.Empty<int>();
    private static int[] workCityOffsets =
        Array.Empty<int>();
    private static int[] tileMarks = Array.Empty<int>();
    private static List<Actor> activeSource;
    private static MapChunk[] activeChunks;
    private static int activeChunkCount;
    private static int activeUnitWorkCount;
    private static int activeTileMark;
    private static int preparedGeneration = -1;
    private static int tileMarkGeneration;
    private static int pendingIslandGeneration = -1;
    private static int unitMembershipVersion;
    private static bool statusIndexRebuildPrepared;
    private static List<WorldTile> activeTilesToClear;
    private static MapChunk[] activeChunksToClear;
    private static bool forceClearBuildings;
    private static readonly List<ActorZoneDirtyEntry>
        dirtySpatialActors = new();

    /// <summary>
    /// 原版 chunk.objects.units_all 成员表的提交版本。
    /// 只有 checkUnits 完整结束后才推进，因此读方不会观察到半提交状态。
    /// </summary>
    internal static int UnitMembershipVersion =>
        Volatile.Read(ref unitMembershipVersion);

    internal static bool TryClearTileUnits(
        List<WorldTile> tilesToClear)
    {
        if (!ShouldUseParallelClear() ||
            tilesToClear == null ||
            tilesToClear.Count <
            ParallelTileClearThreshold)
        {
            return false;
        }

        activeTilesToClear = tilesToClear;
        try
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                tilesToClear.Count,
                clearTileAction);
        }
        finally
        {
            activeTilesToClear = null;
        }

        tilesToClear.Clear();
        return true;
    }

    internal static bool TryClearChunkObjects(
        bool clearBuildings)
    {
        MapChunk[] chunks =
            World.world?.map_chunk_manager?.chunks;
        if (!ShouldUseParallelClear() ||
            chunks == null ||
            chunks.Length < 2)
        {
            return false;
        }

        activeChunksToClear = chunks;
        forceClearBuildings = clearBuildings;
        try
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                chunks.Length,
                clearChunkAction);
        }
        finally
        {
            activeChunksToClear = null;
            forceClearBuildings = false;
        }

        return true;
    }

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

    internal static void
        NotifyUnitMembershipIncrementallyRebuilt(
            IReadOnlyList<int> dirtyChunks,
            MapChunk[] chunks)
    {
        int previousVersion =
            Volatile.Read(
                ref unitMembershipVersion);
        int version =
            Interlocked.Increment(
                ref unitMembershipVersion);
        bool applied =
            NearbyStatusTargetIndex
                .TryApplyChunkMembershipChanges(
                    previousVersion,
                    version,
                    dirtyChunks,
                    chunks);
        if (!applied)
        {
            NearbyStatusTargetIndex
                .NotifyUnitMembershipRebuilt(
                    version,
                    fusedIndexPrepared: false);
        }

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
            IncrementalSimObjectZoneUnits.Invalidate();
            return false;
        }

        PrepareUnitPartitionStorage(
            chunks,
            source.Count);
        bool benchmark = Bench.bench_enabled;
        if (benchmark)
        {
            Bench.bench(
                "checkUnits.dirty_spatial",
                "sim_zones");
        }

        int dirtySpatialActorCount =
            ActorZoneMembershipDirtyIndex
                .Consume(dirtySpatialActors);
        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.dirty_spatial",
                "sim_zones",
                pSaveCounter: true,
                dirtySpatialActorCount);
        }

        NearbyStatusTargetIndex
            .BeginUnitMembershipRebuild();
        statusIndexRebuildPrepared = true;
        activeSource = source;
        activeChunks = chunks;
        activeChunkCount = chunks.Length;
        try
        {
            int tileMark =
                NextTileMark(world.tiles_list.Length);
            bool rebuildIslands =
                pendingIslandGeneration ==
                SimulationTime.Generation;
            if (benchmark)
            {
                Bench.bench(
                    "checkUnits.parallel_prepare",
                    "sim_zones");
            }

            RunUnitClassification();
            PrepareStableChunkOffsets();
            RunUnitScatter();

            int aliveCount = 0;
            for (int i = 0;
                 i < actorCountsByChunk.Length;
                 i++)
            {
                aliveCount += actorCountsByChunk[i];
            }

            if (rebuildIslands)
            {
                RebuildIslandMembership(source);
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

            activeTileMark = tileMark;
            SimulationWorkerPool.Instance
                .RunIndexed(
                    0,
                    chunks.Length,
                    rebuildChunkAction);
            MergeOccupiedTiles(tilesToClear);

            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.parallel_commit",
                    "sim_zones",
                    pSaveCounter: true,
                    aliveCount);
                Bench.bench(
                    "checkUnits.status_membership",
                    "sim_zones");
            }

            RebuildNearbyStatusMembership();

            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.status_membership",
                    "sim_zones",
                    pSaveCounter: true,
                    aliveCount);
                Bench.bench(
                    "checkUnits.city_membership",
                    "sim_zones");
            }

            for (int i = 0;
                 i < cityMembershipActorCount;
                 i++)
            {
                UpdateCityMembership(
                    cityMembershipActors[i]);
            }

            if (benchmark)
            {
                Bench.benchEnd(
                    "checkUnits.city_membership",
                    "sim_zones",
                    pSaveCounter: true,
                    cityMembershipActorCount);
            }

            IncrementalSimObjectZoneUnits
                .CompleteFullRebuild(
                    source,
                    chunks,
                    tilesToClear);
            return true;
        }
        catch
        {
            NearbyStatusTargetIndex
                .AbortUnitMembershipRebuild();
            statusIndexRebuildPrepared = false;
            throw;
        }
        finally
        {
            activeSource = null;
            activeChunks = null;
            activeChunkCount = 0;
            activeUnitWorkCount = 0;
            activeTileMark = 0;
            dirtySpatialActors.Clear();
        }
    }

    private static void PrepareUnitPartitionStorage(
        MapChunk[] chunks,
        int actorCount)
    {
        int generation = SimulationTime.Generation;
        int chunkCount = chunks.Length;
        if (preparedGeneration != generation ||
            actorsByChunk.Length != chunkCount)
        {
            preparedGeneration = generation;
            tileMarkGeneration = 0;
            actorsByChunk =
                new Actor[chunkCount][];
            actorCountsByChunk =
                new int[chunkCount];
            statusActorsByChunk =
                new Actor[chunkCount][];
            statusUnitIndicesByChunk =
                new int[chunkCount][];
            statusActorCountsByChunk =
                new int[chunkCount];
            occupiedTilesByChunk =
                new List<WorldTile>[chunkCount];
            int initialCapacity = Math.Max(
                16,
                actorCount /
                Math.Max(1, chunkCount));
            for (int i = 0; i < chunkCount; i++)
            {
                actorsByChunk[i] =
                    new Actor[initialCapacity];
                statusActorsByChunk[i] =
                    new Actor[initialCapacity];
                statusUnitIndicesByChunk[i] =
                    new int[initialCapacity];
                occupiedTilesByChunk[i] =
                    new List<WorldTile>(
                        Math.Min(
                            TilesPerChunk,
                            initialCapacity));
            }
        }

        if (unitChunkIndices.Length < actorCount)
        {
            int capacity = Math.Max(
                PerformanceSettings.SimulationBatchSize,
                actorCount);
            unitChunkIndices = new int[capacity];
            cityMembershipFlags =
                new byte[capacity];
            cityMembershipActors =
                new Actor[capacity];
        }

        activeUnitWorkCount =
            (actorCount +
             PerformanceSettings.SimulationBatchSize -
             1) /
            PerformanceSettings.SimulationBatchSize;
        int workCellCount =
            activeUnitWorkCount *
            chunkCount;
        if (workChunkCounts.Length < workCellCount)
        {
            workChunkCounts =
                new int[workCellCount];
            workChunkOffsets =
                new int[workCellCount];
        }
        else
        {
            Array.Clear(
                workChunkCounts,
                0,
                workCellCount);
        }

        if (workCityCounts.Length <
            activeUnitWorkCount)
        {
            workCityCounts =
                new int[activeUnitWorkCount];
            workCityOffsets =
                new int[activeUnitWorkCount];
        }
        else
        {
            Array.Clear(
                workCityCounts,
                0,
                activeUnitWorkCount);
        }
    }

    private static void RunUnitClassification()
    {
        if (activeUnitWorkCount > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                activeUnitWorkCount,
                classifyUnitRangeAction);
        }
        else if (activeUnitWorkCount == 1)
        {
            ClassifyUnitRange(0);
        }
    }

    private static void ClassifyUnitRange(int workIndex)
    {
        int start =
            workIndex *
            PerformanceSettings.SimulationBatchSize;
        int end = Math.Min(
            activeSource.Count,
            start +
            PerformanceSettings.SimulationBatchSize);
        int countOffset =
            workIndex *
            activeChunkCount;
        for (int i = start; i < end; i++)
        {
            Actor actor = activeSource[i];
            if (!actor.isAlive())
            {
                unitChunkIndices[i] = -1;
                cityMembershipFlags[i] = 0;
                continue;
            }

            WorldTile tile = actor.current_tile;
            int chunkIndex = tile.chunk.id;
            unitChunkIndices[i] = chunkIndex;
            workChunkCounts[
                countOffset +
                chunkIndex]++;
            City city = tile.zone_city;
            cityMembershipFlags[i] =
                city != null &&
                ShouldQueueCityMembership(actor)
                    ? (byte)1
                    : (byte)0;
            if (cityMembershipFlags[i] != 0)
            {
                workCityCounts[workIndex]++;
            }
        }
    }

    /// <summary>
    /// 按连续 World.units 分区计算每个 worker 在各 chunk 中的写入区间。
    /// worker 只写自己的区间，因此无需锁；分区前缀按 workIndex 递增，
    /// chunk 内角色顺序仍与原版 World.units 完全一致。
    /// </summary>
    private static void PrepareStableChunkOffsets()
    {
        for (int chunkIndex = 0;
             chunkIndex < activeChunkCount;
             chunkIndex++)
        {
            int total = 0;
            for (int workIndex = 0;
                 workIndex < activeUnitWorkCount;
                 workIndex++)
            {
                int cell =
                    workIndex *
                    activeChunkCount +
                    chunkIndex;
                workChunkOffsets[cell] = total;
                total += workChunkCounts[cell];
            }

            Actor[] actors =
                actorsByChunk[chunkIndex];
            int previousCount =
                actorCountsByChunk[chunkIndex];
            if (actors.Length < total)
            {
                int capacity = Math.Max(
                    total,
                    Math.Max(
                        16,
                        actors.Length * 2));
                actorsByChunk[chunkIndex] =
                    new Actor[capacity];
            }
            else if (previousCount > total)
            {
                Array.Clear(
                    actors,
                    total,
                    previousCount - total);
            }

            actorCountsByChunk[chunkIndex] =
                total;
            if (statusActorsByChunk[
                    chunkIndex].Length < total)
            {
                int capacity =
                    actorsByChunk[
                        chunkIndex].Length;
                statusActorsByChunk[
                    chunkIndex] =
                    new Actor[capacity];
                statusUnitIndicesByChunk[
                    chunkIndex] =
                    new int[capacity];
            }
        }

        int previousCityCount =
            cityMembershipActorCount;
        int cityCount = 0;
        for (int workIndex = 0;
             workIndex < activeUnitWorkCount;
             workIndex++)
        {
            workCityOffsets[workIndex] =
                cityCount;
            cityCount +=
                workCityCounts[workIndex];
        }

        if (previousCityCount > cityCount)
        {
            Array.Clear(
                cityMembershipActors,
                cityCount,
                previousCityCount - cityCount);
        }

        cityMembershipActorCount = cityCount;
    }

    private static void RunUnitScatter()
    {
        if (activeUnitWorkCount > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                activeUnitWorkCount,
                scatterUnitRangeAction);
        }
        else if (activeUnitWorkCount == 1)
        {
            ScatterUnitRange(0);
        }
    }

    private static void ScatterUnitRange(int workIndex)
    {
        int start =
            workIndex *
            PerformanceSettings.SimulationBatchSize;
        int end = Math.Min(
            activeSource.Count,
            start +
            PerformanceSettings.SimulationBatchSize);
        int offsetBase =
            workIndex *
            activeChunkCount;
        for (int i = start; i < end; i++)
        {
            int chunkIndex =
                unitChunkIndices[i];
            if (chunkIndex < 0)
            {
                continue;
            }

            int offsetCell =
                offsetBase +
                chunkIndex;
            int targetIndex =
                workChunkOffsets[offsetCell]++;
            actorsByChunk[chunkIndex][
                targetIndex] = activeSource[i];
            if (cityMembershipFlags[i] != 0)
            {
                int cityTargetIndex =
                    workCityOffsets[workIndex]++;
                cityMembershipActors[
                    cityTargetIndex] =
                    activeSource[i];
            }
        }
    }

    private static void RebuildIslandMembership(
        List<Actor> source)
    {
        ParallelIslandActorMembership
            .Rebuild(source);
    }

    private static void MergeOccupiedTiles(
        List<WorldTile> tilesToClear)
    {
        for (int i = 0;
             i < occupiedTilesByChunk.Length;
             i++)
        {
            tilesToClear.AddRange(
                occupiedTilesByChunk[i]);
        }
    }

    private static void RebuildNearbyStatusMembership()
    {
        for (int chunkIndex = 0;
             chunkIndex < activeChunkCount;
             chunkIndex++)
        {
            Actor[] actors =
                statusActorsByChunk[
                    chunkIndex];
            int[] unitIndices =
                statusUnitIndicesByChunk[
                    chunkIndex];
            int count =
                statusActorCountsByChunk[
                    chunkIndex];
            MapChunk chunk =
                activeChunks[chunkIndex];
            for (int unitIndex = 0;
                 unitIndex < count;
                 unitIndex++)
            {
                NearbyStatusTargetIndex
                    .AddUnitMembership(
                        actors[unitIndex],
                        chunk,
                        unitIndices[unitIndex]);
            }
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
        Actor[] actors = actorsByChunk[chunkIndex];
        int count = actorCountsByChunk[chunkIndex];
        List<WorldTile> occupiedTiles =
            occupiedTilesByChunk[chunkIndex];
        Actor[] statusActors =
            statusActorsByChunk[chunkIndex];
        int[] statusUnitIndices =
            statusUnitIndicesByChunk[
                chunkIndex];
        int previousStatusCount =
            statusActorCountsByChunk[
                chunkIndex];
        int statusCount = 0;
        occupiedTiles.Clear();
        int tileMark = activeTileMark;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            WorldTile tile = actor.current_tile;
            int tileId = tile.tile_id;
            if (tileMarks[tileId] != tileMark)
            {
                tileMarks[tileId] = tileMark;
                occupiedTiles.Add(tile);
            }

            tile.addUnit(actor);
            chunk.objects.addActor(actor);
            if (NearbyStatusTargetIndex
                .ShouldAddUnitMembership(actor))
            {
                statusActors[statusCount] =
                    actor;
                statusUnitIndices[statusCount] =
                    i;
                statusCount++;
            }
        }

        if (previousStatusCount > statusCount)
        {
            Array.Clear(
                statusActors,
                statusCount,
                previousStatusCount -
                statusCount);
        }

        statusActorCountsByChunk[
            chunkIndex] = statusCount;
    }

    private static bool ShouldUseParallelClear()
    {
        return PerformanceSettings
                   .EnableFramePriorityScheduler &&
               World.world?.units?.Count >=
               ParallelThreshold;
    }

    private static void ClearTile(int index)
    {
        activeTilesToClear[index].clearUnits();
    }

    private static void ClearChunk(int index)
    {
        MapChunk chunk =
            activeChunksToClear[index];
        if (!chunk.objects.isEmpty())
        {
            chunk.clearObjects(
                forceClearBuildings);
        }
    }

    internal static bool ShouldQueueCityMembership(
        Actor actor)
    {
        return actor.isAlive() &&
               !actor.isInsideSomething() &&
               (actor.profession_asset.can_capture ||
                !actor.kingdom.isCiv());
    }

    internal static void UpdateCityMembership(Actor actor)
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
