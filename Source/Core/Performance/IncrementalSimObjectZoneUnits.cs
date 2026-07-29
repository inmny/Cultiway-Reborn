using System;
using System.Collections.Generic;
using System.Globalization;
using Cultiway.Const;
using Cultiway.Utils;
using HarmonyLib;

namespace Cultiway.Core.Performance;

/// <summary>
/// 在完整重建得到稳定基线后，仅维护发生变化的角色空间成员关系。
/// 容器结构变化时退回完整重建；建筑按脏 chunk 更新，
/// 岛屿角色表在拓扑稳定时按角色脏标记更新。
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
    private static readonly Dictionary<TileIsland, int>
        IslandRanks = new();
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
    private static TileIsland[] committedIslands =
        Array.Empty<TileIsland>();
    private static long[] committedKingdomIds =
        Array.Empty<long>();
    private static byte[] committedAlive =
        Array.Empty<byte>();
    private static byte[] cityMembershipFlags =
        Array.Empty<byte>();
    private static int[] dirtyChunkMarks =
        Array.Empty<int>();
    private static int[] islandValidationCursors =
        Array.Empty<int>();
    private static TileIsland[] preparedIslandSequence =
        Array.Empty<TileIsland>();
    private static List<Actor> preparedSource;
    private static MapChunk[] preparedChunks;
    private static List<WorldTile> preparedTilesToClear;
    private static ListPool<TileIsland> preparedIslands;
    private static int preparedGeneration = -1;
    private static int preparedStructuralVersion = -1;
    private static int dirtyChunkMark;
    private static int committedAliveCount;
    private static int islandValidationCounter;
    private static bool ready;
    private static long attempts;
    private static long handled;
    private static long fullRebuilds;
    private static long islandRebuilds;
    private static long islandIncrementalPasses;
    private static long islandMembershipChanges;
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
        committedAliveCount = 0;
        Array.Clear(
            committedTiles,
            0,
            committedTiles.Length);
        Array.Clear(
            committedIslands,
            0,
            committedIslands.Length);
        Array.Clear(
            committedKingdomIds,
            0,
            committedKingdomIds.Length);
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
            committedIslands[i] =
                tile.region.island;
            committedKingdomIds[i] =
                actor.kingdom.id;
            committedAliveCount++;
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
        CaptureIslandTopology();
        preparedGeneration = SimulationTime.Generation;
        preparedStructuralVersion =
            ActorMetaPartitionVersion
                .GetStructuralVersion(
                    World.world.units.version);
        DirtyActors.Clear();
        DirtyChunks.Clear();
        islandValidationCounter = 0;
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
        bool islandMembershipCurrent =
            IsPreparedIslandMembershipCurrent();
        ValidateDirtyActors(
            islandMembershipCurrent);

        if (benchmark)
        {
            Bench.bench(
                "checkUnits.incremental_islands",
                "sim_zones");
        }

        int islandChanges;
        if (islandMembershipCurrent)
        {
            islandChanges =
                ApplyIslandMembershipChanges();
            islandIncrementalPasses++;
            islandMembershipChanges +=
                islandChanges;
        }
        else
        {
            RebuildIslandMembership();
            CaptureIslandMembership();
            islandRebuilds++;
            islandChanges =
                committedAliveCount;
        }

        if (benchmark)
        {
            Bench.benchEnd(
                "checkUnits.incremental_islands",
                "sim_zones",
                pSaveCounter: true,
                islandChanges);
            Bench.bench(
                "checkUnits.incremental_membership",
                "sim_zones");
        }

        bool chunkMembershipChanged =
            ApplyUnitMembershipChanges(
                tilesToClear);

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

        if (buildingsDirty)
        {
            RebuildDirtyBuildingChunks(
                dirtyBuildingChunks);
        }

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
        ValidateIslandMembershipSampled();
        handled++;
        return true;
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "attempts={0} handled={1} full={2} " +
            "islands={3}/{4}/{5}(full/incremental/changes) " +
            "reject=disabled:{6},not_ready:{7},buildings:{8}," +
            "world:{9},tiles:{10},disposed:{11}",
            attempts,
            handled,
            fullRebuilds,
            islandRebuilds,
            islandIncrementalPasses,
            islandMembershipChanges,
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
        preparedIslands = null;
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

    private static void ValidateDirtyActors(
        bool islandMembershipCurrent)
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

            TileIsland oldIsland =
                committedIslands[actorRank];
            TileIsland newIsland =
                newAlive
                    ? newTile.region.island
                    : null;
            if (islandMembershipCurrent &&
                !ReferenceEquals(
                    oldIsland,
                    newIsland) &&
                (oldIsland == null ||
                 !oldIsland.actors.Contains(actor)))
            {
                throw new InvalidOperationException(
                    "island 角色成员表与增量基线不一致");
            }
        }
    }

    private static void RebuildIslandMembership()
    {
        ParallelIslandActorMembership
            .Rebuild(preparedSource);
    }

    private static bool
        IsPreparedIslandMembershipCurrent()
    {
        ListPool<TileIsland> islands =
            World.world.islands_calculator.islands;
        if (preparedIslandSequence.Length !=
            islands.Count)
        {
            return false;
        }

        int actorCount = 0;
        for (int i = 0; i < islands.Count; i++)
        {
            TileIsland island = islands[i];
            if (!ReferenceEquals(
                    preparedIslandSequence[i],
                    island))
            {
                return false;
            }

            actorCount += island.actors.Count;
        }

        if (actorCount != committedAliveCount)
        {
            return false;
        }

        // clearDirty 会替换 ListPool 外壳，即使岛屿对象序列未变。
        // 成员关系由 TileIsland 持有，只需接纳新的容器引用。
        preparedIslands = islands;
        return true;
    }

    private static void CaptureIslandMembership()
    {
        committedAliveCount = 0;
        Array.Clear(
            committedIslands,
            0,
            committedIslands.Length);
        for (int i = 0;
             i < preparedSource.Count;
             i++)
        {
            Actor actor = preparedSource[i];
            if (!actor.isAlive())
            {
                continue;
            }

            committedIslands[i] =
                actor.current_tile
                    .region
                    .island;
            committedAliveCount++;
        }

        CaptureIslandTopology();
    }

    private static void CaptureIslandTopology()
    {
        ListPool<TileIsland> islands =
            World.world.islands_calculator.islands;
        if (preparedIslandSequence.Length !=
            islands.Count)
        {
            preparedIslandSequence =
                new TileIsland[islands.Count];
        }

        for (int i = 0; i < islands.Count; i++)
        {
            preparedIslandSequence[i] =
                islands[i];
        }

        preparedIslands = islands;
    }

    private static int ApplyIslandMembershipChanges()
    {
        int changes = 0;
        for (int i = 0; i < DirtyActors.Count; i++)
        {
            Actor actor = DirtyActors[i].Actor;
            int actorRank = ActorRanks[actor];
            bool oldAlive =
                committedAlive[actorRank] != 0;
            bool newAlive = actor.isAlive();
            TileIsland oldIsland =
                committedIslands[actorRank];
            TileIsland newIsland =
                newAlive
                    ? actor.current_tile
                        .region
                        .island
                    : null;
            if (ReferenceEquals(
                    oldIsland,
                    newIsland))
            {
                continue;
            }

            if (oldAlive &&
                (oldIsland == null ||
                 !oldIsland.actors.Remove(actor)))
            {
                throw new InvalidOperationException(
                    "无法从旧岛屿成员表移除角色");
            }

            if (newAlive)
            {
                InsertActorAtRank(
                    newIsland.actors,
                    actor,
                    actorRank);
            }

            if (oldAlive != newAlive)
            {
                committedAliveCount +=
                    newAlive
                        ? 1
                        : -1;
            }

            committedIslands[actorRank] =
                newIsland;
            changes++;
        }

        return changes;
    }

    private static void
        ValidateIslandMembershipSampled()
    {
        if (!SystemUtils.IsUnderDeveloper())
        {
            return;
        }

        int validationIndex =
            unchecked(++islandValidationCounter);
        if (validationIndex <= 0)
        {
            islandValidationCounter = 1;
            validationIndex = 1;
        }

        if (validationIndex > 32 &&
            validationIndex % 256 != 0)
        {
            return;
        }

        if (!IsPreparedIslandMembershipCurrent())
        {
            throw new InvalidOperationException(
                "岛屿角色成员容器在增量提交后失效");
        }

        ListPool<TileIsland> islands =
            preparedIslands;
        IslandRanks.Clear();
        if (islandValidationCursors.Length <
            islands.Count)
        {
            islandValidationCursors =
                new int[islands.Count];
        }
        else
        {
            Array.Clear(
                islandValidationCursors,
                0,
                islands.Count);
        }

        for (int i = 0; i < islands.Count; i++)
        {
            IslandRanks.Add(
                islands[i],
                i);
        }

        int aliveCount = 0;
        for (int actorRank = 0;
             actorRank < preparedSource.Count;
             actorRank++)
        {
            Actor actor =
                preparedSource[actorRank];
            bool alive = actor.isAlive();
            if ((committedAlive[actorRank] != 0) !=
                alive)
            {
                throw new InvalidOperationException(
                    "角色存活状态变更未写入空间脏索引");
            }

            WorldTile tile =
                alive
                    ? actor.current_tile
                    : null;
            if (!ReferenceEquals(
                    committedTiles[actorRank],
                    tile))
            {
                throw new InvalidOperationException(
                    "角色 tile 变更未写入空间脏索引");
            }

            TileIsland island =
                alive
                    ? tile?.region?.island
                    : null;
            if (!ReferenceEquals(
                    committedIslands[actorRank],
                    island))
            {
                throw new InvalidOperationException(
                    "角色 island 变更未写入空间脏索引");
            }

            if (!alive)
            {
                continue;
            }

            if (committedKingdomIds[actorRank] !=
                actor.kingdom.id)
            {
                throw new InvalidOperationException(
                    "角色 kingdom 变更未写入空间脏索引");
            }

            if (!IslandRanks.TryGetValue(
                    island,
                    out int islandRank))
            {
                throw new InvalidOperationException(
                    "活体角色所在岛屿不属于当前拓扑");
            }

            int cursor =
                islandValidationCursors[
                    islandRank]++;
            List<Actor> islandActors =
                island.actors;
            if (cursor >= islandActors.Count ||
                !ReferenceEquals(
                    islandActors[cursor],
                    actor))
            {
                throw new InvalidOperationException(
                    "岛屿角色成员顺序与 World.units 不一致");
            }

            aliveCount++;
        }

        for (int i = 0; i < islands.Count; i++)
        {
            if (islandValidationCursors[i] !=
                islands[i].actors.Count)
            {
                throw new InvalidOperationException(
                    "岛屿角色成员数量与 World.units 不一致");
            }
        }

        if (aliveCount != committedAliveCount)
        {
            throw new InvalidOperationException(
                "岛屿角色存活计数与增量基线不一致");
        }

        for (int i = 0;
             i < preparedChunks.Length;
             i++)
        {
            IncrementalChunkActorMembership
                .Validate(
                    preparedChunks[i].objects,
                    actorsByChunk[i]);
        }
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
            long oldKingdomId =
                oldAlive
                    ? committedKingdomIds[
                        actorRank]
                    : 0L;
            long newKingdomId =
                newAlive
                    ? actor.kingdom.id
                    : 0L;
            if (oldChunkIndex != newChunkIndex)
            {
                if (oldChunkIndex >= 0)
                {
                    IncrementalChunkActorMembership
                        .Remove(
                            preparedChunks[
                                    oldChunkIndex]
                                .objects,
                            actor,
                            oldKingdomId);
                    actorsByChunk[
                            oldChunkIndex]
                        .Remove(actor);
                    MarkDirtyChunk(
                        oldChunkIndex);
                }

                if (newChunkIndex >= 0)
                {
                    IncrementalChunkActorMembership
                        .Add(
                            preparedChunks[
                                    newChunkIndex]
                                .objects,
                            actor,
                            newKingdomId,
                            actorRank,
                            ActorRanks);
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
                     oldKingdomId !=
                     newKingdomId)
            {
                IncrementalChunkActorMembership
                    .ChangeKingdom(
                        preparedChunks[
                                newChunkIndex]
                            .objects,
                        actor,
                        oldKingdomId,
                        newKingdomId,
                        actorRank,
                        ActorRanks);
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
            committedKingdomIds[actorRank] =
                newKingdomId;
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

    private static void RebuildDirtyBuildingChunks(
        HashSet<MapChunk> dirtyBuildingChunks)
    {
        foreach (MapChunk chunk in
                 dirtyBuildingChunks)
        {
            chunk.clearObjects(
                pForceClearBuildings: false);
            List<Actor> actors =
                actorsByChunk[chunk.id];
            for (int actorIndex = 0;
                 actorIndex < actors.Count;
                 actorIndex++)
            {
                chunk.objects.addActor(
                    actors[actorIndex]);
            }
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
            committedIslands =
                new TileIsland[actorCount];
            committedKingdomIds =
                new long[actorCount];
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
