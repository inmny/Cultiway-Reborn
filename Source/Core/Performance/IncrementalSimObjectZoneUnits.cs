using System;
using System.Collections.Generic;
using System.Globalization;
using Cultiway.Const;
using HarmonyLib;

namespace Cultiway.Core.Performance;

/// <summary>
/// 在完整重建得到稳定基线后，仅维护发生变化的角色空间成员关系。
/// 容器结构变化时退回完整重建；建筑按脏 chunk 更新，
/// 岛屿角色表则保持原版每轮完整重建的语义。
/// </summary>
internal static class IncrementalSimObjectZoneUnits
{
    private const int ParallelThreshold = 1024;

    private static readonly AccessTools.FieldRef<
            WorldTile,
            List<Actor>>
        TileUnitsField =
            AccessTools.FieldRefAccess<
                WorldTile,
                List<Actor>>("_units");

    private static readonly Dictionary<Actor, int>
        ActorRanks = new();
    private static readonly SortedSet<int>
        CityMembershipActorRanks = new();
    private static readonly HashSet<WorldTile>
        TrackedTiles = new();
    private static readonly List<ActorZoneDirtyEntry>
        DirtyActors = new();
    private static readonly List<int>
        DirtyChunks = new();

    private static List<Actor>[] actorsByChunk =
        Array.Empty<List<Actor>>();
    private static WorldTile[] committedTiles =
        Array.Empty<WorldTile>();
    private static byte[] committedAlive =
        Array.Empty<byte>();
    private static byte[] cityMembershipFlags =
        Array.Empty<byte>();
    private static int[] dirtyChunkMarks =
        Array.Empty<int>();
    private static List<Actor> preparedSource;
    private static MapChunk[] preparedChunks;
    private static List<WorldTile> preparedTilesToClear;
    private static int preparedGeneration = -1;
    private static int preparedStructuralVersion = -1;
    private static int dirtyChunkMark;
    private static bool ready;
    private static long attempts;
    private static long handled;
    private static long fullRebuilds;
    private static long islandRebuilds;
    private static long rejectedDisabled;
    private static long rejectedNotReady;
    private static long rejectedBuildings;
    private static long rejectedWorld;
    private static long rejectedTiles;
    private static long rejectedAfterDisposed;

    internal static void CompleteFullRebuild(
        List<Actor> source,
        MapChunk[] chunks,
        List<WorldTile> tilesToClear)
    {
        ready = false;
        EnsureStorage(
            source.Count,
            chunks.Length);
        ActorRanks.Clear();
        CityMembershipActorRanks.Clear();
        TrackedTiles.Clear();
        Array.Clear(
            committedTiles,
            0,
            committedTiles.Length);
        Array.Clear(
            committedAlive,
            0,
            committedAlive.Length);
        Array.Clear(
            cityMembershipFlags,
            0,
            cityMembershipFlags.Length);
        for (int i = 0;
             i < actorsByChunk.Length;
             i++)
        {
            actorsByChunk[i].Clear();
        }

        for (int i = 0; i < source.Count; i++)
        {
            Actor actor = source[i];
            ActorRanks.Add(actor, i);
            if (!actor.isAlive())
            {
                continue;
            }

            WorldTile tile = actor.current_tile;
            committedAlive[i] = 1;
            committedTiles[i] = tile;
            actorsByChunk[tile.chunk.id].Add(actor);
            if (ParallelSimObjectZoneUnits
                .ShouldQueueCityMembership(actor))
            {
                cityMembershipFlags[i] = 1;
                CityMembershipActorRanks.Add(i);
            }
        }

        TrackedTiles.UnionWith(tilesToClear);
        preparedSource = source;
        preparedChunks = chunks;
        preparedTilesToClear = tilesToClear;
        preparedGeneration = SimulationTime.Generation;
        preparedStructuralVersion =
            ActorMetaPartitionVersion
                .GetStructuralVersion(
                    World.world.units.version);
        DirtyActors.Clear();
        DirtyChunks.Clear();
        fullRebuilds++;
        ready = true;
    }

    internal static bool TryRecalculate(
        bool buildingsDirty,
        HashSet<MapChunk> dirtyBuildingChunks,
        List<WorldTile> tilesToClear)
    {
        MapBox world = World.world;
        attempts++;
        if (!CanUseIncremental(
                world,
                buildingsDirty,
                dirtyBuildingChunks,
                tilesToClear))
        {
            return false;
        }

        bool benchmark = Bench.bench_enabled;
        if (benchmark)
        {
            Bench.bench(
                "clear_islands_docks",
                "sim_zones");
        }

        if (buildingsDirty)
        {
            ClearIslandDocks();
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "clear_islands_docks",
                "sim_zones",
                pSaveCounter: false,
                0L);
            Bench.bench(
                "clear_capture_and_danger_zones",
                "sim_zones");
        }

        foreach (City city in world.cities)
        {
            city.clearCurrentCaptureAmounts();
            city.clearDangerZones();
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "clear_capture_and_danger_zones",
                "sim_zones",
                pSaveCounter: false,
                0L);
            Bench.bench(
                "clear_all_disposed",
                "sim_zones");
        }

        foreach (BaseSystemManager manager in
                 world.list_all_sim_managers)
        {
            manager.ClearAllDisposed();
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "clear_all_disposed",
                "sim_zones",
                pSaveCounter: false,
                0L);
        }

        // ClearAllDisposed 可能真正移除角色；此时让原流程从头完整重建。
        if (!IsPreparedWorldCurrent(world))
        {
            rejectedAfterDisposed++;
            Invalidate();
            return false;
        }

        if (benchmark)
        {
            Bench.bench(
                "checkUnits",
                "sim_zones");
            Bench.bench(
                "checkUnits.incremental_collect",
                "sim_zones");
        }

        int dirtyCount =
            ActorZoneMembershipDirtyIndex
                .Consume(DirtyActors);
        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.incremental_collect",
                "sim_zones",
                pSaveCounter: true,
                dirtyCount);
        }

        DirtyActors.Sort(CompareDirtyActors);
        ValidateDirtyActors();

        if (benchmark)
        {
            Bench.bench(
                "checkUnits.incremental_islands",
                "sim_zones");
        }

        // 原版每次 recalc 都会完整重建岛屿角色表。
        // 岛屿对象可能在角色没有移动时被原版拓扑流程替换，
        // 因而不能只依赖角色脏标记维护这层成员关系。
        RebuildIslandMembership();
        islandRebuilds++;

        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.incremental_islands",
                "sim_zones",
                pSaveCounter: true,
                dirtyCount);
            Bench.bench(
                "checkUnits.incremental_membership",
                "sim_zones");
        }

        bool chunkMembershipChanged =
            ApplyUnitMembershipChanges(
                tilesToClear);
        if (buildingsDirty)
        {
            MarkDirtyBuildingChunks(
                dirtyBuildingChunks);
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.incremental_membership",
                "sim_zones",
                pSaveCounter: true,
                dirtyCount);
            Bench.bench(
                "checkUnits.incremental_chunks",
                "sim_zones");
        }

        RebuildDirtyChunks();

        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.incremental_chunks",
                "sim_zones",
                pSaveCounter: true,
                DirtyChunks.Count);
            Bench.bench(
                "checkUnits.city_membership",
                "sim_zones");
        }

        foreach (int actorRank in
                 CityMembershipActorRanks)
        {
            ParallelSimObjectZoneUnits
                .UpdateCityMembership(
                    preparedSource[actorRank]);
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.city_membership",
                "sim_zones",
                pSaveCounter: true,
                CityMembershipActorRanks.Count);
            Bench.benchEnd(
                "checkUnits",
                "sim_zones",
                pSaveCounter: false,
                0L);
            Bench.bench(
                "checkBuildings",
                "sim_zones");
        }

        if (buildingsDirty)
        {
            RebuildDirtyBuildings(
                dirtyBuildingChunks);
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "checkBuildings",
                "sim_zones",
                pSaveCounter: false,
                0L);
        }

        if (chunkMembershipChanged)
        {
            ParallelSimObjectZoneUnits
                .NotifyUnitMembershipIncrementallyRebuilt(
                    DirtyChunks,
                    preparedChunks);
        }

        DirtyActors.Clear();
        DirtyChunks.Clear();
        handled++;
        return true;
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "attempts={0} handled={1} full={2} islands={3} " +
            "reject=disabled:{4},not_ready:{5},buildings:{6}," +
            "world:{7},tiles:{8},disposed:{9}",
            attempts,
            handled,
            fullRebuilds,
            islandRebuilds,
            rejectedDisabled,
            rejectedNotReady,
            rejectedBuildings,
            rejectedWorld,
            rejectedTiles,
            rejectedAfterDisposed);
    }

    internal static void Invalidate()
    {
        ready = false;
        preparedSource = null;
        preparedChunks = null;
        preparedTilesToClear = null;
        DirtyActors.Clear();
        DirtyChunks.Clear();
    }

    private static bool CanUseIncremental(
        MapBox world,
        bool buildingsDirty,
        HashSet<MapChunk> dirtyBuildingChunks,
        List<WorldTile> tilesToClear)
    {
        if (!PerformanceSettings
                .EnableFramePriorityScheduler ||
            world?.units?.Count <
            ParallelThreshold)
        {
            rejectedDisabled++;
            return false;
        }

        if (!ready)
        {
            rejectedNotReady++;
            return false;
        }

        if (buildingsDirty &&
            dirtyBuildingChunks == null)
        {
            rejectedBuildings++;
            return false;
        }

        if (!ReferenceEquals(
                preparedTilesToClear,
                tilesToClear))
        {
            rejectedTiles++;
            return false;
        }

        if (!IsPreparedWorldCurrent(world))
        {
            rejectedWorld++;
            return false;
        }

        return true;
    }

    private static bool IsPreparedWorldCurrent(
        MapBox world)
    {
        return world != null &&
               preparedGeneration ==
               SimulationTime.Generation &&
               ReferenceEquals(
                   preparedSource,
                   world.units.getSimpleList()) &&
               ReferenceEquals(
                   preparedChunks,
                   world.map_chunk_manager.chunks) &&
               preparedSource.Count ==
               world.units.Count &&
               preparedStructuralVersion ==
               ActorMetaPartitionVersion
                   .GetStructuralVersion(
                       world.units.version);
    }

    private static void ValidateDirtyActors()
    {
        for (int i = 0; i < DirtyActors.Count; i++)
        {
            Actor actor = DirtyActors[i].Actor;
            if (!ActorRanks.TryGetValue(
                    actor,
                    out int actorRank))
            {
                throw new InvalidOperationException(
                    "增量空间成员表遇到未知角色");
            }

            bool oldAlive =
                committedAlive[actorRank] != 0;
            bool newAlive = actor.isAlive();
            WorldTile oldTile =
                committedTiles[actorRank];
            WorldTile newTile =
                newAlive
                    ? actor.current_tile
                    : null;
            if (newAlive &&
                (newTile?.chunk == null ||
                 newTile.region?.island == null))
            {
                throw new InvalidOperationException(
                    "活体角色缺少有效空间成员");
            }

            if (!oldAlive)
            {
                continue;
            }

            if (oldTile?.chunk == null ||
                oldTile.region?.island == null)
            {
                throw new InvalidOperationException(
                    "已提交角色缺少有效空间成员");
            }

            bool tileChanged =
                !ReferenceEquals(oldTile, newTile);
            if (tileChanged &&
                !TileUnitsField(oldTile)
                    .Contains(actor))
            {
                throw new InvalidOperationException(
                    "tile 角色成员表与增量基线不一致");
            }

            bool chunkChanged =
                !newAlive ||
                oldTile.chunk.id !=
                newTile.chunk.id;
            if (chunkChanged &&
                !actorsByChunk[oldTile.chunk.id]
                    .Contains(actor))
            {
                throw new InvalidOperationException(
                    "chunk 角色成员表与增量基线不一致");
            }
        }
    }

    private static void RebuildIslandMembership()
    {
        ParallelIslandActorMembership
            .Rebuild(preparedSource);
    }

    private static bool ApplyUnitMembershipChanges(
        List<WorldTile> tilesToClear)
    {
        NextDirtyChunkMark();
        bool chunkMembershipChanged = false;
        for (int i = 0; i < DirtyActors.Count; i++)
        {
            ActorZoneDirtyEntry entry =
                DirtyActors[i];
            Actor actor = entry.Actor;
            int actorRank = ActorRanks[actor];
            bool oldAlive =
                committedAlive[actorRank] != 0;
            bool newAlive = actor.isAlive();
            WorldTile oldTile =
                committedTiles[actorRank];
            WorldTile newTile =
                newAlive
                    ? actor.current_tile
                    : null;
            bool tileChanged =
                !ReferenceEquals(oldTile, newTile);

            if (oldAlive && tileChanged)
            {
                TileUnitsField(oldTile)
                    .Remove(actor);
            }

            if (newAlive && tileChanged)
            {
                List<Actor> units =
                    TileUnitsField(newTile);
                InsertActorAtRank(
                    units,
                    actor,
                    actorRank);
                if (TrackedTiles.Add(newTile))
                {
                    tilesToClear.Add(newTile);
                }
            }

            int oldChunkIndex =
                oldAlive
                    ? oldTile.chunk.id
                    : -1;
            int newChunkIndex =
                newAlive
                    ? newTile.chunk.id
                    : -1;
            if (oldChunkIndex != newChunkIndex)
            {
                if (oldChunkIndex >= 0)
                {
                    actorsByChunk[
                            oldChunkIndex]
                        .Remove(actor);
                    MarkDirtyChunk(
                        oldChunkIndex);
                }

                if (newChunkIndex >= 0)
                {
                    InsertActorAtRank(
                        actorsByChunk[
                            newChunkIndex],
                        actor,
                        actorRank);
                    MarkDirtyChunk(
                        newChunkIndex);
                }

                chunkMembershipChanged = true;
            }
            else if (newChunkIndex >= 0 &&
                     (entry.Kind &
                      ActorZoneDirtyKind
                          .ChunkMetadata) != 0)
            {
                MarkDirtyChunk(newChunkIndex);
            }

            if ((entry.Kind &
                 ActorZoneDirtyKind
                     .CityEligibility) != 0)
            {
                UpdateCityMembershipCandidate(
                    actor,
                    actorRank);
            }

            committedAlive[actorRank] =
                newAlive
                    ? (byte)1
                    : (byte)0;
            committedTiles[actorRank] = newTile;
        }

        return chunkMembershipChanged;
    }

    private static void UpdateCityMembershipCandidate(
        Actor actor,
        int actorRank)
    {
        bool previous =
            cityMembershipFlags[actorRank] != 0;
        bool next =
            ParallelSimObjectZoneUnits
                .ShouldQueueCityMembership(actor);
        if (previous == next)
        {
            return;
        }

        cityMembershipFlags[actorRank] =
            next
                ? (byte)1
                : (byte)0;
        if (next)
        {
            CityMembershipActorRanks.Add(
                actorRank);
        }
        else
        {
            CityMembershipActorRanks.Remove(
                actorRank);
        }
    }

    private static void RebuildDirtyChunks()
    {
        for (int i = 0; i < DirtyChunks.Count; i++)
        {
            int chunkIndex = DirtyChunks[i];
            MapChunk chunk =
                preparedChunks[chunkIndex];
            chunk.clearObjects(
                pForceClearBuildings: false);
            List<Actor> actors =
                actorsByChunk[chunkIndex];
            for (int actorIndex = 0;
                 actorIndex < actors.Count;
                 actorIndex++)
            {
                chunk.objects.addActor(
                    actors[actorIndex]);
            }
        }
    }

    private static void MarkDirtyBuildingChunks(
        HashSet<MapChunk> dirtyBuildingChunks)
    {
        foreach (MapChunk chunk in
                 dirtyBuildingChunks)
        {
            MarkDirtyChunk(chunk.id);
        }
    }

    private static void ClearIslandDocks()
    {
        ListPool<TileIsland> islands =
            World.world.islands_calculator.islands;
        for (int i = 0; i < islands.Count; i++)
        {
            TileIsland island = islands[i];
            island.docks?.Dispose();
            island.docks = null;
        }
    }

    private static void RebuildDirtyBuildings(
        HashSet<MapChunk> dirtyBuildingChunks)
    {
        List<Building> buildings =
            World.world.buildings.getSimpleList();
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (!building.isUsable())
            {
                continue;
            }

            MapChunk chunk = building.chunk;
            if (!chunk.buildings_dirty)
            {
                continue;
            }

            if (building.isCiv() &&
                building.asset.docks &&
                building.component_docks
                    .hasOceanTiles())
            {
                building.component_docks
                    .tiles_ocean[0]
                    .region
                    .island
                    .addDock(building);
            }

            chunk.objects.addBuilding(building);
        }

        foreach (MapChunk chunk in
                 dirtyBuildingChunks)
        {
            chunk.finishBuildingsCheck();
        }

        dirtyBuildingChunks.Clear();
    }

    private static void MarkDirtyChunk(
        int chunkIndex)
    {
        if (dirtyChunkMarks[chunkIndex] ==
            dirtyChunkMark)
        {
            return;
        }

        dirtyChunkMarks[chunkIndex] =
            dirtyChunkMark;
        DirtyChunks.Add(chunkIndex);
    }

    private static void NextDirtyChunkMark()
    {
        DirtyChunks.Clear();
        int next = unchecked(++dirtyChunkMark);
        if (next != 0)
        {
            return;
        }

        Array.Clear(
            dirtyChunkMarks,
            0,
            dirtyChunkMarks.Length);
        dirtyChunkMark = 1;
    }

    private static void InsertActorAtRank(
        List<Actor> target,
        Actor actor,
        int actorRank)
    {
        int low = 0;
        int high = target.Count;
        while (low < high)
        {
            int middle =
                low + (high - low) / 2;
            int middleRank =
                ActorRanks[target[middle]];
            if (middleRank < actorRank)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        target.Insert(low, actor);
    }

    private static int CompareDirtyActors(
        ActorZoneDirtyEntry left,
        ActorZoneDirtyEntry right)
    {
        bool hasLeft = ActorRanks.TryGetValue(
            left.Actor,
            out int leftRank);
        bool hasRight = ActorRanks.TryGetValue(
            right.Actor,
            out int rightRank);
        if (!hasLeft)
        {
            return hasRight ? 1 : 0;
        }

        return !hasRight
            ? -1
            : leftRank.CompareTo(rightRank);
    }

    private static void EnsureStorage(
        int actorCount,
        int chunkCount)
    {
        if (committedTiles.Length < actorCount)
        {
            committedTiles =
                new WorldTile[actorCount];
            committedAlive =
                new byte[actorCount];
            cityMembershipFlags =
                new byte[actorCount];
        }

        if (actorsByChunk.Length != chunkCount)
        {
            actorsByChunk =
                new List<Actor>[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                actorsByChunk[i] =
                    new List<Actor>();
            }
        }

        if (dirtyChunkMarks.Length != chunkCount)
        {
            dirtyChunkMarks =
                new int[chunkCount];
            dirtyChunkMark = 0;
        }
    }
}
