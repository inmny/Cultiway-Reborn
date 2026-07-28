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
        GeoRegionUnits,
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
    private readonly List<Building> occupiedBuildings = new();
    private readonly List<BaseSystemManager> metaManagers = new();
    private readonly Action<int> dirtyManagerWorkItemAction;
    private MapBox world;
    private MaintenanceStage stage;
    private int index;
    private bool windowOnScreen;
    private int preparedWorldGeneration = -1;
    private int preparedActorVersion = -1;
    private int preparedActorPartitionVersion = -1;
    private int lastAnythingChangedFrame = -1;
    private bool actorPartitionsReady;
    private bool hasDirtyMetaManagers;
    private int dirtyMetaManagerCount;
    private long[] dirtyManagerTicks = Array.Empty<long>();

    internal CooperativeWorldMaintenanceRunner()
    {
        dirtyManagerWorkItemAction = RunDirtyManagerAt;
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
            lastAnythingChangedFrame = -1;
            actorPartitionsReady = false;
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
                bool actorPartitionsDirty =
                    !actorPartitionsReady ||
                    preparedActorVersion !=
                    world.units.version ||
                    preparedActorPartitionVersion !=
                    ActorMetaPartitionVersion.Version;
                if (!actorPartitionsDirty)
                {
                    stage =
                        MaintenanceStage.GeoRegionUnits;
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
                ProcessActorMetaBatch();
                if (index >= actors.Count)
                {
                    preparedActorVersion =
                        world.units.version;
                    preparedActorPartitionVersion =
                        ActorMetaPartitionVersion.Version;
                    actorPartitionsReady = true;
                    stage = MaintenanceStage.GeoRegionUnits;
                }

                break;
            case MaintenanceStage.GeoRegionUnits:
                WorldboxGame.I?.GeoRegions
                    ?.ApplyPendingUnitChanges();
                hasDirtyMetaManagers =
                    HasDirtyMetaManagers();
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
                    stage = MaintenanceStage.DirtyMetaObjectsFirst;
                }

                break;
            case MaintenanceStage.DirtyManagersParallel:
                RunDirtyManagersParallel();
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
        actors.Clear();
        occupiedBuildings.Clear();
        metaManagers.Clear();
        world = null;
        stage = MaintenanceStage.Idle;
        index = 0;
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

    private void ProcessActorMetaBatch()
    {
        int end = Math.Min(actors.Count, index + PerformanceSettings.SimulationBatchSize);
        for (; index < end; index++)
        {
            Actor actor = actors[index];
            if (actor.isAlive())
            {
                if (actor.kingdom.wild)
                {
                    world.units.units_only_wild.Add(actor);
                }
                else
                {
                    world.units.units_only_civ.Add(actor);
                }

                world.units.units_only_alive.Add(actor);
            }
            else
            {
                world.units.units_only_dying.Add(actor);
                world.units.have_dying_units = true;
            }
        }
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
}
