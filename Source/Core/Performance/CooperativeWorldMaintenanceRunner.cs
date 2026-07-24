using System;
using System.Collections.Generic;
using Cultiway.Const;
using life.taxi;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeWorldMaintenanceRunner
{
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
        DirtyManagersStart,
        DirtyManagers,
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
    private MapBox world;
    private MaintenanceStage stage;
    private int index;
    private bool windowOnScreen;

    public bool Active => stage != MaintenanceStage.Idle;

    public void Start(MapBox map)
    {
        Abort();
        world = map;
        windowOnScreen = map.isWindowOnScreen();
        stage = MaintenanceStage.BuildingZones;
    }

    public string GetNextPhaseName()
    {
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
                    stage = MaintenanceStage.DirtyManagersStart;
                }

                break;
            case MaintenanceStage.DirtyManagersStart:
                metaManagers.Clear();
                metaManagers.AddRange(world._list_meta_main_managers);
                index = 0;
                stage = MaintenanceStage.DirtyManagers;
                break;
            case MaintenanceStage.DirtyManagers:
                if (index < metaManagers.Count)
                {
                    BaseSystemManager manager = metaManagers[index++];
                    if (manager.isUnitsDirty())
                    {
                        manager.parallelDirtyUnitsCheck();
                    }
                }
                else
                {
                    stage = MaintenanceStage.DirtyMetaObjectsFirst;
                }

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
                world.checkAnyMetaAddedRemoved();
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
}
