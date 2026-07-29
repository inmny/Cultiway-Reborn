using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cultiway.Const;
using life.taxi;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeWorldMaintenanceRunner
{
    private const string DirtyManagerPhasePrefix =
        "vanilla.maintenance.dirtymanagers.";
    private const int ActorMetaPartitionStride = 4;
    private const int AlivePartitionIndex = 0;
    private const int WildPartitionIndex = 1;
    private const int CivilizedPartitionIndex = 2;
    private const int DyingPartitionIndex = 3;

    private static readonly Dictionary<Type, string> DirtyManagerPhaseNames = new();

    private enum MaintenanceStage
    {
        Idle,
        BuildingZones,
        CheckListsBefore,
        UnitContainer,
        BuildingContainer,
        SimObjectZones,
        PrepareActorsStart,
        PrepareActors,
        PrepareActorsIncremental,
        GeoRegionUnits,
        DirtyActorIndex,
        DirtyManagersStart,
        DirtyManagers,
        DirtyManagersParallel,
        DirtyMetaObjectsFirst,
        DestroyMetaObjects,
        DestroyObjects,
        CheckListsAfter,
        UnitDestroyStart,
        UnitDestroy,
        BuildingDestroyStart,
        BuildingDestroy,
        HousesStart,
        HousesBuildings,
        HousesActorsStart,
        HousesActors,
        DirtyMetaObjectsSecond,
        AnythingChanged,
        Complete
    }

    private readonly List<Actor> actors = new();
    private readonly List<Actor> dirtyActorPartitions =
        new();
    private readonly Dictionary<Actor, int> actorMetaIndices =
        new();
    private readonly List<Building> occupiedBuildings = new();
    private readonly List<BaseSystemManager> metaManagers = new();
    private readonly Action<int> dirtyManagerWorkItemAction;
    private readonly Action<int> classifyActorMetaWorkItemAction;
    private readonly Action<int> scatterActorMetaWorkItemAction;
    private MapBox world;
    private MaintenanceStage stage;
    private int index;
    private bool windowOnScreen;
    private int preparedWorldGeneration = -1;
    private int preparedActorVersion = -1;
    private int preparedActorPartitionVersion = -1;
    private int pendingActorPartitionVersion = -1;
    private int lastAnythingChangedFrame = -1;
    private bool actorPartitionsReady;
    private bool hasDirtyMetaManagers;
    private int dirtyMetaManagerCount;
    private long[] dirtyManagerTicks = Array.Empty<long>();
    private ActorMetaPartitionKind[] actorMetaPartitions =
        Array.Empty<ActorMetaPartitionKind>();
    private int[] actorMetaPartitionCounts = Array.Empty<int>();
    private int[] actorMetaPartitionOffsets = Array.Empty<int>();
    private Actor[] aliveActors = Array.Empty<Actor>();
    private Actor[] wildActors = Array.Empty<Actor>();
    private Actor[] civilizedActors = Array.Empty<Actor>();
    private Actor[] dyingActors = Array.Empty<Actor>();
    private int bufferedAliveActorCount;
    private int bufferedWildActorCount;
    private int bufferedCivilizedActorCount;
    private int bufferedDyingActorCount;
    private int actorMetaWorkCount;

    internal CooperativeWorldMaintenanceRunner()
    {
        dirtyManagerWorkItemAction = RunDirtyManagerAt;
        classifyActorMetaWorkItemAction =
            ClassifyActorMetaRange;
        scatterActorMetaWorkItemAction =
            ScatterActorMetaRange;
    }

    public bool Active => stage != MaintenanceStage.Idle;

    public void Start(MapBox map)
    {
        Abort();
        world = map;
        int worldGeneration = SimulationTime.Generation;
        if (preparedWorldGeneration != worldGeneration)
        {
            preparedWorldGeneration = worldGeneration;
            preparedActorVersion = -1;
            preparedActorPartitionVersion = -1;
            pendingActorPartitionVersion = -1;
            lastAnythingChangedFrame = -1;
            actorPartitionsReady = false;
            actorMetaIndices.Clear();
            DirtyMetaActorIndex.Clear();
        }

        windowOnScreen = map.isWindowOnScreen();
        stage = MaintenanceStage.BuildingZones;
    }

    public string GetNextPhaseName()
    {
        if (stage == MaintenanceStage.DirtyManagers &&
            index < metaManagers.Count)
        {
            return GetDirtyManagerPhaseName(metaManagers[index]);
        }

        return "vanilla.maintenance." + stage.ToString().ToLowerInvariant();
    }

    public bool Step()
    {
        switch (stage)
        {
            case MaintenanceStage.Idle:
                return true;
            case MaintenanceStage.BuildingZones:
                BuildingZonesSystem.update();
                stage = MaintenanceStage.CheckListsBefore;
                break;
            case MaintenanceStage.CheckListsBefore:
                world.checkSimManagerLists();
                stage = MaintenanceStage.UnitContainer;
                break;
            case MaintenanceStage.UnitContainer:
                world.units.checkContainer();
                stage = MaintenanceStage.BuildingContainer;
                break;
            case MaintenanceStage.BuildingContainer:
                world.buildings.checkContainer();
                stage = MaintenanceStage.SimObjectZones;
                break;
            case MaintenanceStage.SimObjectZones:
                world.sim_object_zones.update();
                stage = MaintenanceStage.PrepareActorsStart;
                break;
            case MaintenanceStage.PrepareActorsStart:
                metaManagers.Clear();
                metaManagers.AddRange(
                    world._list_meta_main_managers);
                int actorStructuralVersion =
                    ActorMetaPartitionVersion
                        .GetStructuralVersion(
                            world.units.version);
                bool actorStructureDirty =
                    !actorPartitionsReady ||
                    preparedActorVersion !=
                    actorStructuralVersion;
                bool actorPartitionsDirty =
                    actorStructureDirty ||
                    preparedActorPartitionVersion !=
                    ActorMetaPartitionVersion.Version;
                if (!actorPartitionsDirty)
                {
                    stage =
                        MaintenanceStage.GeoRegionUnits;
                    break;
                }

                pendingActorPartitionVersion =
                    ActorMetaPartitionVersion
                        .ConsumeDirtyActors(
                            dirtyActorPartitions);
                if (!actorStructureDirty)
                {
                    stage =
                        MaintenanceStage
                            .PrepareActorsIncremental;
                    break;
                }

                actors.Clear();
                actors.AddRange(world.units.getSimpleList());
                world.units.units_only_wild.Clear();
                world.units.units_only_alive.Clear();
                world.units.units_only_dying.Clear();
                world.units.units_only_civ.Clear();
                world.units.have_dying_units = false;
                index = 0;
                stage = MaintenanceStage.PrepareActors;
                break;
            case MaintenanceStage.PrepareActors:
                RebuildActorMetaPartitions();
                RebuildActorMetaIndices();
                preparedActorVersion =
                    ActorMetaPartitionVersion
                        .GetStructuralVersion(
                            world.units.version);
                preparedActorPartitionVersion =
                    pendingActorPartitionVersion;
                dirtyActorPartitions.Clear();
                actorPartitionsReady = true;
                stage = MaintenanceStage.GeoRegionUnits;

                break;
            case MaintenanceStage.PrepareActorsIncremental:
                ApplyActorMetaPartitionChanges();
                preparedActorVersion =
                    ActorMetaPartitionVersion
                        .GetStructuralVersion(
                            world.units.version);
                preparedActorPartitionVersion =
                    pendingActorPartitionVersion;
                dirtyActorPartitions.Clear();
                stage = MaintenanceStage.GeoRegionUnits;
                break;
            case MaintenanceStage.GeoRegionUnits:
                WorldboxGame.I?.GeoRegions
                    ?.ApplyPendingUnitChanges();
                hasDirtyMetaManagers =
                    HasDirtyMetaManagers();
                stage = hasDirtyMetaManagers
                    ? MaintenanceStage.DirtyActorIndex
                    : MaintenanceStage.DirtyManagersStart;
                break;
            case MaintenanceStage.DirtyActorIndex:
                DirtyMetaActorIndex.Prepare(
                    metaManagers,
                    aliveActors,
                    bufferedAliveActorCount,
                    dyingActors,
                    bufferedDyingActorCount);
                stage = MaintenanceStage.DirtyManagersStart;
                break;
            case MaintenanceStage.DirtyManagersStart:
                index = 0;
                if (!hasDirtyMetaManagers)
                {
                    stage =
                        MaintenanceStage.DirtyMetaObjectsFirst;
                }
                else if (dirtyMetaManagerCount >= 3)
                {
                    stage =
                        MaintenanceStage.DirtyManagersParallel;
                }
                else
                {
                    stage = MaintenanceStage.DirtyManagers;
                }

                break;
            case MaintenanceStage.DirtyManagers:
                if (index < metaManagers.Count)
                {
                    BaseSystemManager manager = metaManagers[index++];
                    if (manager.isUnitsDirty())
                    {
                        long startedAt = SimulationTickBenchmark.IsCapturing
                            ? Stopwatch.GetTimestamp()
                            : 0L;
                        manager.parallelDirtyUnitsCheck();
                        if (startedAt != 0L)
                        {
                            double seconds =
                                (Stopwatch.GetTimestamp() - startedAt) /
                                (double)Stopwatch.Frequency;
                            SimulationTickBenchmark.RecordDirtyManagerMetric(
                                GetManagerBenchmarkId(manager),
                                seconds);
                        }
                    }
                }
                else
                {
                    DirtyMetaActorIndex.End();
                    stage = MaintenanceStage.DirtyMetaObjectsFirst;
                }

                break;
            case MaintenanceStage.DirtyManagersParallel:
                RunDirtyManagersParallel();
                DirtyMetaActorIndex.End();
                stage = MaintenanceStage.DirtyMetaObjectsFirst;
                break;
            case MaintenanceStage.DirtyMetaObjectsFirst:
                world.checkDirtyMetaObjects();
                stage = MaintenanceStage.DestroyMetaObjects;
                break;
            case MaintenanceStage.DestroyMetaObjects:
                if (!windowOnScreen)
                {
                    world.checkMetaObjectsDestroy();
                }

                stage = MaintenanceStage.DestroyObjects;
                break;
            case MaintenanceStage.DestroyObjects:
                if (!windowOnScreen)
                {
                    world.checkObjectsToDestroy();
                }

                stage = MaintenanceStage.CheckListsAfter;
                break;
            case MaintenanceStage.CheckListsAfter:
                world.checkSimManagerLists();
                stage = MaintenanceStage.UnitDestroyStart;
                break;
            case MaintenanceStage.UnitDestroyStart:
                index = 0;
                if (world.units.event_destroy)
                {
                    world.units.event_destroy = false;
                    RefreshActors();
                    stage = MaintenanceStage.UnitDestroy;
                }
                else
                {
                    stage = MaintenanceStage.BuildingDestroyStart;
                }

                break;
            case MaintenanceStage.UnitDestroy:
                ProcessUnitDestroyBatch();
                if (index >= actors.Count)
                {
                    TaxiManager.removeDeadUnits();
                    stage = MaintenanceStage.BuildingDestroyStart;
                }

                break;
            case MaintenanceStage.BuildingDestroyStart:
                index = 0;
                if (world.buildings.event_destroy)
                {
                    world.buildings.event_destroy = false;
                    RefreshActors();
                    stage = MaintenanceStage.BuildingDestroy;
                }
                else
                {
                    stage = MaintenanceStage.HousesStart;
                }

                break;
            case MaintenanceStage.BuildingDestroy:
                ProcessBuildingDestroyBatch();
                if (index >= actors.Count)
                {
                    stage = MaintenanceStage.HousesStart;
                }

                break;
            case MaintenanceStage.HousesStart:
                index = 0;
                occupiedBuildings.Clear();
                if (world.buildings.event_houses)
                {
                    world.buildings.event_houses = false;
                    occupiedBuildings.AddRange(world.buildings.occupied_buildings);
                    stage = MaintenanceStage.HousesBuildings;
                }
                else
                {
                    stage = MaintenanceStage.DirtyMetaObjectsSecond;
                }

                break;
            case MaintenanceStage.HousesBuildings:
                ProcessOccupiedBuildingBatch();
                if (index >= occupiedBuildings.Count)
                {
                    stage = MaintenanceStage.HousesActorsStart;
                }

                break;
            case MaintenanceStage.HousesActorsStart:
                RefreshActors();
                index = 0;
                stage = MaintenanceStage.HousesActors;
                break;
            case MaintenanceStage.HousesActors:
                ProcessHouseActorBatch();
                if (index >= actors.Count)
                {
                    stage = MaintenanceStage.DirtyMetaObjectsSecond;
                }

                break;
            case MaintenanceStage.DirtyMetaObjectsSecond:
                world.checkDirtyMetaObjects();
                stage = MaintenanceStage.AnythingChanged;
                break;
            case MaintenanceStage.AnythingChanged:
                int frame = UnityEngine.Time.frameCount;
                if (lastAnythingChangedFrame != frame)
                {
                    world.checkAnyMetaAddedRemoved();
                    lastAnythingChangedFrame = frame;
                }

                stage = MaintenanceStage.Complete;
                break;
            case MaintenanceStage.Complete:
                Abort();
                return true;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return false;
    }

    public void Abort()
    {
        DirtyMetaActorIndex.End();
        actors.Clear();
        dirtyActorPartitions.Clear();
        occupiedBuildings.Clear();
        metaManagers.Clear();
        world = null;
        stage = MaintenanceStage.Idle;
        index = 0;
        actorMetaWorkCount = 0;
    }

    private bool HasDirtyMetaManagers()
    {
        dirtyMetaManagerCount = 0;
        for (int i = 0; i < metaManagers.Count; i++)
        {
            if (metaManagers[i].isUnitsDirty())
            {
                dirtyMetaManagerCount++;
            }
        }

        return dirtyMetaManagerCount > 0;
    }

    private void RunDirtyManagersParallel()
    {
        int count = metaManagers.Count;
        if (dirtyManagerTicks.Length < count)
        {
            dirtyManagerTicks = new long[count];
        }

        SimulationWorkerPool.Instance.RunIndexed(
            0,
            count,
            dirtyManagerWorkItemAction);
        if (!SimulationTickBenchmark.IsCapturing)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            long ticks = dirtyManagerTicks[i];
            if (ticks <= 0L)
            {
                continue;
            }

            SimulationTickBenchmark.RecordDirtyManagerMetric(
                GetManagerBenchmarkId(metaManagers[i]),
                ticks / (double)Stopwatch.Frequency);
        }
    }

    private void RunDirtyManagerAt(int managerIndex)
    {
        BaseSystemManager manager =
            metaManagers[managerIndex];
        if (!manager.isUnitsDirty())
        {
            dirtyManagerTicks[managerIndex] = 0L;
            return;
        }

        long startedAt = SimulationTickBenchmark.IsCapturing
            ? Stopwatch.GetTimestamp()
            : 0L;
        manager.parallelDirtyUnitsCheck();
        dirtyManagerTicks[managerIndex] = startedAt == 0L
            ? 0L
            : Stopwatch.GetTimestamp() - startedAt;
    }

    private void RebuildActorMetaPartitions()
    {
        int count = actors.Count;
        if (actorMetaPartitions.Length < count)
        {
            actorMetaPartitions =
                new ActorMetaPartitionKind[
                    Math.Max(
                        PerformanceSettings.SimulationBatchSize,
                        count)];
        }

        actorMetaWorkCount =
            (count +
             PerformanceSettings.SimulationBatchSize -
             1) /
            PerformanceSettings.SimulationBatchSize;
        int partitionSlotCount =
            actorMetaWorkCount *
            ActorMetaPartitionStride;
        if (actorMetaPartitionCounts.Length <
            partitionSlotCount)
        {
            actorMetaPartitionCounts =
                new int[partitionSlotCount];
            actorMetaPartitionOffsets =
                new int[partitionSlotCount];
        }

        if (actorMetaWorkCount > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                actorMetaWorkCount,
                classifyActorMetaWorkItemAction);
        }
        else if (actorMetaWorkCount == 1)
        {
            ClassifyActorMetaRange(0);
        }

        int aliveCount = 0;
        int wildCount = 0;
        int civilizedCount = 0;
        int dyingCount = 0;
        for (int workIndex = 0;
             workIndex < actorMetaWorkCount;
             workIndex++)
        {
            int slot =
                workIndex *
                ActorMetaPartitionStride;
            actorMetaPartitionOffsets[
                slot + AlivePartitionIndex] =
                aliveCount;
            actorMetaPartitionOffsets[
                slot + WildPartitionIndex] =
                wildCount;
            actorMetaPartitionOffsets[
                slot + CivilizedPartitionIndex] =
                civilizedCount;
            actorMetaPartitionOffsets[
                slot + DyingPartitionIndex] =
                dyingCount;
            aliveCount +=
                actorMetaPartitionCounts[
                    slot + AlivePartitionIndex];
            wildCount +=
                actorMetaPartitionCounts[
                    slot + WildPartitionIndex];
            civilizedCount +=
                actorMetaPartitionCounts[
                    slot + CivilizedPartitionIndex];
            dyingCount +=
                actorMetaPartitionCounts[
                    slot + DyingPartitionIndex];
        }

        EnsureActorBufferCapacity(
            ref aliveActors,
            aliveCount);
        EnsureActorBufferCapacity(
            ref wildActors,
            wildCount);
        EnsureActorBufferCapacity(
            ref civilizedActors,
            civilizedCount);
        EnsureActorBufferCapacity(
            ref dyingActors,
            dyingCount);

        if (actorMetaWorkCount > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                actorMetaWorkCount,
                scatterActorMetaWorkItemAction);
        }
        else if (actorMetaWorkCount == 1)
        {
            ScatterActorMetaRange(0);
        }

        ClearStaleActorReferences(
            aliveActors,
            aliveCount,
            ref bufferedAliveActorCount);
        ClearStaleActorReferences(
            wildActors,
            wildCount,
            ref bufferedWildActorCount);
        ClearStaleActorReferences(
            civilizedActors,
            civilizedCount,
            ref bufferedCivilizedActorCount);
        ClearStaleActorReferences(
            dyingActors,
            dyingCount,
            ref bufferedDyingActorCount);

        AddActorRange(
            world.units.units_only_alive,
            aliveActors,
            aliveCount);
        AddActorRange(
            world.units.units_only_wild,
            wildActors,
            wildCount);
        AddActorRange(
            world.units.units_only_civ,
            civilizedActors,
            civilizedCount);
        AddActorRange(
            world.units.units_only_dying,
            dyingActors,
            dyingCount);
        world.units.have_dying_units =
            dyingCount > 0;

        index = count;
        actorMetaWorkCount = 0;
    }

    private void RebuildActorMetaIndices()
    {
        actorMetaIndices.Clear();
        for (int i = 0; i < actors.Count; i++)
        {
            actorMetaIndices.Add(actors[i], i);
        }
    }

    private void ApplyActorMetaPartitionChanges()
    {
        List<Actor> alive =
            world.units.units_only_alive;
        List<Actor> wild =
            world.units.units_only_wild;
        List<Actor> civilized =
            world.units.units_only_civ;
        List<Actor> dying =
            world.units.units_only_dying;

        for (int i = 0;
             i < dirtyActorPartitions.Count;
             i++)
        {
            Actor actor = dirtyActorPartitions[i];
            int actorIndex = actorMetaIndices[actor];
            ActorMetaPartitionKind previous =
                actorMetaPartitions[actorIndex];
            ActorMetaPartitionKind next =
                GetActorMetaPartition(actor);
            if (previous == next)
            {
                continue;
            }

            bool previousAlive =
                previous != ActorMetaPartitionKind.Dying;
            bool nextAlive =
                next != ActorMetaPartitionKind.Dying;
            if (previousAlive != nextAlive)
            {
                if (previousAlive)
                {
                    RemoveActorAtRank(
                        alive,
                        actor,
                        actorIndex);
                }
                else
                {
                    InsertActorAtRank(
                        alive,
                        actor,
                        actorIndex);
                }
            }

            switch (previous)
            {
                case ActorMetaPartitionKind.AliveWild:
                    RemoveActorAtRank(
                        wild,
                        actor,
                        actorIndex);
                    break;
                case ActorMetaPartitionKind.AliveCivilized:
                    RemoveActorAtRank(
                        civilized,
                        actor,
                        actorIndex);
                    break;
                case ActorMetaPartitionKind.Dying:
                    RemoveActorAtRank(
                        dying,
                        actor,
                        actorIndex);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            actorMetaPartitions[actorIndex] = next;
            switch (next)
            {
                case ActorMetaPartitionKind.AliveWild:
                    InsertActorAtRank(
                        wild,
                        actor,
                        actorIndex);
                    break;
                case ActorMetaPartitionKind.AliveCivilized:
                    InsertActorAtRank(
                        civilized,
                        actor,
                        actorIndex);
                    break;
                case ActorMetaPartitionKind.Dying:
                    InsertActorAtRank(
                        dying,
                        actor,
                        actorIndex);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        CopyActorListToBuffer(
            alive,
            ref aliveActors,
            ref bufferedAliveActorCount);
        CopyActorListToBuffer(
            dying,
            ref dyingActors,
            ref bufferedDyingActorCount);
        world.units.have_dying_units =
            dying.Count > 0;
    }

    private void RemoveActorAtRank(
        List<Actor> source,
        Actor actor,
        int actorIndex)
    {
        int indexAtRank =
            FindActorRankIndex(source, actorIndex);
        if (indexAtRank >= source.Count ||
            !ReferenceEquals(
                source[indexAtRank],
                actor))
        {
            throw new InvalidOperationException(
                "角色元数据分区顺序与容器索引不一致");
        }

        source.RemoveAt(indexAtRank);
    }

    private void InsertActorAtRank(
        List<Actor> target,
        Actor actor,
        int actorIndex)
    {
        target.Insert(
            FindActorRankIndex(target, actorIndex),
            actor);
    }

    private int FindActorRankIndex(
        List<Actor> source,
        int actorIndex)
    {
        int low = 0;
        int high = source.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            int middleActorIndex =
                actorMetaIndices[source[middle]];
            if (middleActorIndex < actorIndex)
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

    private static void CopyActorListToBuffer(
        List<Actor> source,
        ref Actor[] buffer,
        ref int previousCount)
    {
        int count = source.Count;
        EnsureActorBufferCapacity(
            ref buffer,
            count);
        if (count > 0)
        {
            source.CopyTo(buffer, 0);
        }

        ClearStaleActorReferences(
            buffer,
            count,
            ref previousCount);
    }

    private void ClassifyActorMetaRange(int workIndex)
    {
        int start =
            workIndex *
            PerformanceSettings.SimulationBatchSize;
        int end = Math.Min(
            actors.Count,
            start + PerformanceSettings.SimulationBatchSize);
        int aliveCount = 0;
        int wildCount = 0;
        int civilizedCount = 0;
        int dyingCount = 0;
        for (int i = start; i < end; i++)
        {
            Actor actor = actors[i];
            ActorMetaPartitionKind partition =
                GetActorMetaPartition(actor);
            switch (partition)
            {
                case ActorMetaPartitionKind.AliveWild:
                    aliveCount++;
                    wildCount++;
                    break;
                case ActorMetaPartitionKind.AliveCivilized:
                    aliveCount++;
                    civilizedCount++;
                    break;
                case ActorMetaPartitionKind.Dying:
                    dyingCount++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            actorMetaPartitions[i] = partition;
        }

        int slot =
            workIndex *
            ActorMetaPartitionStride;
        actorMetaPartitionCounts[
            slot + AlivePartitionIndex] =
            aliveCount;
        actorMetaPartitionCounts[
            slot + WildPartitionIndex] =
            wildCount;
        actorMetaPartitionCounts[
            slot + CivilizedPartitionIndex] =
            civilizedCount;
        actorMetaPartitionCounts[
            slot + DyingPartitionIndex] =
            dyingCount;
    }

    private static ActorMetaPartitionKind
        GetActorMetaPartition(Actor actor)
    {
        if (!actor.isAlive())
        {
            return ActorMetaPartitionKind.Dying;
        }

        return actor.kingdom.wild
            ? ActorMetaPartitionKind.AliveWild
            : ActorMetaPartitionKind.AliveCivilized;
    }

    private void ScatterActorMetaRange(int workIndex)
    {
        int start =
            workIndex *
            PerformanceSettings.SimulationBatchSize;
        int end = Math.Min(
            actors.Count,
            start + PerformanceSettings.SimulationBatchSize);
        int slot =
            workIndex *
            ActorMetaPartitionStride;
        int aliveIndex =
            actorMetaPartitionOffsets[
                slot + AlivePartitionIndex];
        int wildIndex =
            actorMetaPartitionOffsets[
                slot + WildPartitionIndex];
        int civilizedIndex =
            actorMetaPartitionOffsets[
                slot + CivilizedPartitionIndex];
        int dyingIndex =
            actorMetaPartitionOffsets[
                slot + DyingPartitionIndex];
        for (int i = start; i < end; i++)
        {
            Actor actor = actors[i];
            switch (actorMetaPartitions[i])
            {
                case ActorMetaPartitionKind.AliveWild:
                    aliveActors[aliveIndex++] = actor;
                    wildActors[wildIndex++] = actor;
                    break;
                case ActorMetaPartitionKind.AliveCivilized:
                    aliveActors[aliveIndex++] = actor;
                    civilizedActors[civilizedIndex++] =
                        actor;
                    break;
                case ActorMetaPartitionKind.Dying:
                    dyingActors[dyingIndex++] = actor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static void EnsureActorBufferCapacity(
        ref Actor[] buffer,
        int required)
    {
        if (buffer.Length >= required)
        {
            return;
        }

        buffer =
            new Actor[
                Math.Max(
                    PerformanceSettings.SimulationBatchSize,
                    required)];
    }

    private static void ClearStaleActorReferences(
        Actor[] buffer,
        int currentCount,
        ref int previousCount)
    {
        if (previousCount > currentCount)
        {
            Array.Clear(
                buffer,
                currentCount,
                previousCount - currentCount);
        }

        previousCount = currentCount;
    }

    private static void AddActorRange(
        List<Actor> target,
        Actor[] source,
        int count)
    {
        if (count == 0)
        {
            return;
        }

        target.AddRange(
            new ArraySegment<Actor>(
                source,
                0,
                count));
    }

    private void ProcessUnitDestroyBatch()
    {
        int end = Math.Min(actors.Count, index + PerformanceSettings.SimulationBatchSize);
        for (; index < end; index++)
        {
            Actor actor = actors[index];
            if (actor.beh_actor_target != null && !actor.beh_actor_target.isAlive())
            {
                actor.beh_actor_target = null;
            }

            if (actor.attackedBy != null && !actor.attackedBy.isAlive())
            {
                actor.attackedBy = null;
            }

            if (actor.hasLover() && !actor.lover.isAlive())
            {
                actor.lover.lover = null;
                actor.lover = null;
            }
        }
    }

    private void ProcessBuildingDestroyBatch()
    {
        int end = Math.Min(actors.Count, index + PerformanceSettings.SimulationBatchSize);
        for (; index < end; index++)
        {
            Actor actor = actors[index];
            if (actor.beh_building_target != null && !actor.beh_building_target.isAlive())
            {
                actor.beh_building_target = null;
            }

            if (actor.attackedBy != null && !actor.attackedBy.isAlive())
            {
                actor.attackedBy = null;
            }
        }
    }

    private void ProcessOccupiedBuildingBatch()
    {
        int end = Math.Min(occupiedBuildings.Count, index + PerformanceSettings.SimulationBatchSize);
        for (; index < end; index++)
        {
            Building building = occupiedBuildings[index];
            building.residents.Clear();
            if (building.asset.docks)
            {
                building.component_docks.clearBoatCounter();
            }
        }
    }

    private void ProcessHouseActorBatch()
    {
        int end = Math.Min(actors.Count, index + PerformanceSettings.SimulationBatchSize);
        for (; index < end; index++)
        {
            Actor actor = actors[index];
            actor.checkHomeBuilding();
            Building home = actor.home_building;
            if (home != null)
            {
                if (home.asset.docks)
                {
                    home.component_docks.increaseBoatCounter(actor);
                }
                else
                {
                    home.residents.Add(actor.data.id);
                }
            }

            Building inside = actor.inside_building;
            if (inside != null && (!inside.isUsable() || inside.isAbandoned()))
            {
                actor.exitBuilding();
                actor.cancelAllBeh();
            }
        }
    }

    private void RefreshActors()
    {
        actors.Clear();
        actors.AddRange(world.units.getSimpleList());
    }

    private static string GetManagerBenchmarkId(BaseSystemManager manager)
    {
        return manager.GetType().FullName ?? manager.GetType().Name;
    }

    private static string GetDirtyManagerPhaseName(BaseSystemManager manager)
    {
        Type type = manager.GetType();
        if (!DirtyManagerPhaseNames.TryGetValue(type, out string phase))
        {
            phase = DirtyManagerPhasePrefix + (type.FullName ?? type.Name);
            DirtyManagerPhaseNames.Add(type, phase);
        }

        return phase;
    }

    private enum ActorMetaPartitionKind : byte
    {
        AliveWild,
        AliveCivilized,
        Dying
    }
}
