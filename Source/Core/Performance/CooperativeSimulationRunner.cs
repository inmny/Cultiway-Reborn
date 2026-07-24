using System;
using System.Collections.Generic;
using Cultiway.Const;
using life.taxi;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeSimulationRunner
{
    private enum SimulationStage
    {
        Idle,
        DirtyCleanup,
        Maintenance,
        Explosions,
        CityZones,
        NutritionTimer,
        WorldTime,
        Taxi,
        MetaHistory,
        AnimationTime,
        EnemyCache,
        ControllableUnit,
        Heat,
        MapChunks,
        MapLayersUpdate,
        MapLayersDraw,
        MapModules,
        Cities,
        ActorsStart,
        Actors,
        BuildingsStart,
        Buildings,
        Drops,
        Cultures,
        StackEffects,
        ResourceThrows,
        WorldBehaviours,
        Armies,
        Kingdoms,
        Diplomacy,
        Subspecies,
        Plots,
        Clans,
        Alliances,
        Wars,
        Languages,
        Religions,
        Projectiles,
        Statuses,
        Era,
        CultiwayStart,
        Cultiway,
        DelayedActions,
        Complete
    }

    public static CooperativeSimulationRunner Instance { get; } = new();

    private readonly CooperativeBatchRunner<BatchActors, Actor> actorRunner = new("vanilla.actors");
    private readonly CooperativeBatchRunner<BatchBuildings, Building> buildingRunner = new("vanilla.buildings");
    private readonly CooperativeWorldMaintenanceRunner maintenanceRunner = new();
    private readonly List<MapLayer> mapLayers = new();
    private readonly List<BaseModule> mapModules = new();
    private readonly List<WorldBehaviourAsset> worldBehaviours = new();
    private MapBox world;
    private SimulationStage stage;
    private float cycleElapsed;
    private bool cyclePaused;
    private int listIndex;
    private double admissionCredits;
    private float lastRequestedSpeed = -1f;
    private WorldTimeScaleAsset lastTimeScaleAsset;
    private int lastControlledFrame = -1;
    private bool ownsCultiwayCycle;
    private bool advancingGameDelayedActions;
    private long logicalTicksAdmitted;
    private long logicalTicksCompleted;
    private long completedAtRateWindowStart;
    private float rateWindowStartedAt = -1f;
    private float requestedSpeed;
    private float actualSpeed;

    public bool Active => stage != SimulationStage.Idle;
    public bool IsAtCycleBoundary => !Active;
    public bool RequiresControl => PerformanceSettings.EnableFramePriorityScheduler || Active;
    public bool ControlledThisFrame => lastControlledFrame == UnityEngine.Time.frameCount;
    public bool OwnsCultiwayCycle => ownsCultiwayCycle;
    public bool IsAdvancingGameDelayedActions => advancingGameDelayedActions;
    public long LogicalTicksAdmitted => logicalTicksAdmitted;
    public long LogicalTicksCompleted => logicalTicksCompleted;
    public float RequestedSpeed => requestedSpeed;
    public float ActualSpeed => actualSpeed;

    public void RunFrame(MapBox map, bool allowNewCycles = true)
    {
        FramePriorityGovernor.BeginFrame();
        ConfigureWorkers(map);
        lastControlledFrame = UnityEngine.Time.frameCount;

        if (Active && !ReferenceEquals(world, map))
        {
            Abort();
        }

        PrepareAdmissionCredits(map, allowNewCycles);

        while (true)
        {
            if (!Active)
            {
                if (!CanAdmitCycle(map, allowNewCycles))
                {
                    break;
                }

                const string startPhase = "vanilla.cycle.start";
                if (!FramePriorityGovernor.CanRun(SimulationDomain.Vanilla, startPhase))
                {
                    FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, startPhase);
                    break;
                }

                admissionCredits -= 1.0;
                FramePriorityGovernor.RunPhase(
                    SimulationDomain.Vanilla,
                    startPhase,
                    () => StartCycle(map));
                continue;
            }

            string phase = GetNextPhaseName();
            SimulationDomain domain = GetCurrentDomain();
            if (!FramePriorityGovernor.CanRun(domain, phase))
            {
                FramePriorityGovernor.SetPhase(domain, phase);
                break;
            }

            FramePriorityGovernor.RunPhase(
                domain,
                phase,
                ExecuteCurrentStage);
        }
    }

    public void Abort()
    {
        actorRunner.Abort();
        buildingRunner.Abort();
        maintenanceRunner.Abort();
        if (ownsCultiwayCycle)
        {
            ModClass.I?.LogicScheduler.Abort();
        }

        mapLayers.Clear();
        mapModules.Clear();
        worldBehaviours.Clear();
        world = null;
        stage = SimulationStage.Idle;
        listIndex = 0;
        admissionCredits = 0.0;
        ownsCultiwayCycle = false;
        advancingGameDelayedActions = false;
        SimulationTime.CancelTick();
        PresentationInterpolator.Reset();
        FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, "idle");
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    public void DrainToBoundary()
    {
        while (Active)
        {
            ExecuteCurrentStage();
        }
    }

    private void StartCycle(MapBox map)
    {
        world = map;
        cycleElapsed = PerformanceSettings.FixedSimulationStepSeconds;
        cyclePaused = map.isPaused();
        SimulationTime.BeginTick(cycleElapsed);
        mapLayers.Clear();
        mapLayers.AddRange(map._map_layers);
        mapModules.Clear();
        mapModules.AddRange(map._map_modules);
        worldBehaviours.Clear();
        worldBehaviours.AddRange(AssetManager.world_behaviours.list);
        listIndex = 0;
        stage = SimulationStage.DirtyCleanup;
        logicalTicksAdmitted++;
        FramePriorityGovernor.RecordVanillaCycleStarted();
    }

    private string GetNextPhaseName()
    {
        return stage switch
        {
            SimulationStage.Actors => actorRunner.GetNextPhaseName(),
            SimulationStage.Buildings => buildingRunner.GetNextPhaseName(),
            SimulationStage.Maintenance => maintenanceRunner.GetNextPhaseName(),
            SimulationStage.MapLayersUpdate when listIndex < mapLayers.Count =>
                "vanilla.map_layer.update." + mapLayers[listIndex].GetType().Name,
            SimulationStage.MapLayersDraw when listIndex < mapLayers.Count =>
                "vanilla.map_layer.draw." + mapLayers[listIndex].GetType().Name,
            SimulationStage.MapModules when listIndex < mapModules.Count =>
                "vanilla.map_module." + mapModules[listIndex].GetType().Name,
            SimulationStage.WorldBehaviours when listIndex < worldBehaviours.Count =>
                "vanilla.world_behaviour." + worldBehaviours[listIndex].id,
            SimulationStage.CultiwayStart => "cultiway.cycle.start",
            SimulationStage.Cultiway => ModClass.I.LogicScheduler.GetNextPhaseName(),
            _ => "vanilla." + stage.ToString().ToLowerInvariant()
        };
    }

    private SimulationDomain GetCurrentDomain()
    {
        return stage is SimulationStage.CultiwayStart or SimulationStage.Cultiway
            ? SimulationDomain.Cultiway
            : SimulationDomain.Vanilla;
    }

    private void ExecuteCurrentStage()
    {
        FixedStepSimulationContext.Run(world, cyclePaused, ExecuteCurrentStageCore);
    }

    private void ExecuteCurrentStageCore()
    {
        switch (stage)
        {
            case SimulationStage.DirtyCleanup:
                maintenanceRunner.Start(world);
                Advance(SimulationStage.Maintenance);
                break;
            case SimulationStage.Maintenance:
                if (maintenanceRunner.Step())
                {
                    Advance(SimulationStage.Explosions);
                }

                break;
            case SimulationStage.Explosions:
                world.explosion_checker.update(cycleElapsed);
                Advance(SimulationStage.CityZones);
                break;
            case SimulationStage.CityZones:
                world.city_zone_helper.update(cycleElapsed);
                Advance(SimulationStage.NutritionTimer);
                break;
            case SimulationStage.NutritionTimer:
                if (!cyclePaused)
                {
                    world.updateTimerNutrition(cycleElapsed);
                }

                Advance(SimulationStage.WorldTime);
                break;
            case SimulationStage.WorldTime:
                if (!cyclePaused)
                {
                    world.map_stats.updateWorldTime(cycleElapsed);
                }

                Advance(SimulationStage.Taxi);
                break;
            case SimulationStage.Taxi:
                if (!cyclePaused)
                {
                    TaxiManager.update(cycleElapsed);
                }

                Advance(SimulationStage.MetaHistory);
                break;
            case SimulationStage.MetaHistory:
                if (!cyclePaused)
                {
                    world.updateMetaHistory();
                }

                Advance(SimulationStage.AnimationTime);
                break;
            case SimulationStage.AnimationTime:
                // 调度开启时动画属于表现时钟，由 MapBox.Update 每个渲染帧推进。
                Advance(SimulationStage.EnemyCache);
                break;
            case SimulationStage.EnemyCache:
                EnemiesFinder.clear();
                Advance(SimulationStage.ControllableUnit);
                break;
            case SimulationStage.ControllableUnit:
                ControllableUnit.updateControllableUnit();
                Advance(SimulationStage.Heat);
                break;
            case SimulationStage.Heat:
                world.heat.update(cycleElapsed);
                Advance(SimulationStage.MapChunks);
                break;
            case SimulationStage.MapChunks:
                world.map_chunk_manager.update(cycleElapsed);
                listIndex = 0;
                stage = SimulationStage.MapLayersUpdate;
                break;
            case SimulationStage.MapLayersUpdate:
                if (listIndex < mapLayers.Count)
                {
                    mapLayers[listIndex++].update(cycleElapsed);
                }
                else
                {
                    listIndex = 0;
                    stage = SimulationStage.MapLayersDraw;
                }

                break;
            case SimulationStage.MapLayersDraw:
                if (listIndex < mapLayers.Count)
                {
                    mapLayers[listIndex++].draw(cycleElapsed);
                }
                else
                {
                    listIndex = 0;
                    stage = SimulationStage.MapModules;
                }

                break;
            case SimulationStage.MapModules:
                if (listIndex < mapModules.Count)
                {
                    mapModules[listIndex++].update(cycleElapsed);
                }
                else
                {
                    listIndex = 0;
                    stage = SimulationStage.Cities;
                }

                break;
            case SimulationStage.Cities:
                if (DebugConfig.isOn(DebugOption.SystemUpdateCities))
                {
                    world.cities.update(cycleElapsed);
                }

                Advance(SimulationStage.ActorsStart);
                break;
            case SimulationStage.ActorsStart:
                if (!DebugConfig.isOn(DebugOption.SystemUpdateUnits))
                {
                    Advance(SimulationStage.BuildingsStart);
                    break;
                }

                world.units.checkContainer();
                JobManagerActors actorManager = world.units.getJobManager();
                actorRunner.Start(actorManager, actorManager.active_batches, cycleElapsed);
                stage = SimulationStage.Actors;
                break;
            case SimulationStage.Actors:
                if (actorRunner.Step())
                {
                    world.units.checkContainer();
                    Advance(SimulationStage.BuildingsStart);
                }

                break;
            case SimulationStage.BuildingsStart:
                if (!DebugConfig.isOn(DebugOption.SystemUpdateBuildings))
                {
                    Advance(SimulationStage.Drops);
                    break;
                }

                world.buildings.checkContainer();
                JobManagerBuildings buildingManager = world.buildings.getJobManager();
                buildingRunner.Start(
                    buildingManager,
                    buildingManager._batches_active,
                    cycleElapsed);
                stage = SimulationStage.Buildings;
                break;
            case SimulationStage.Buildings:
                if (buildingRunner.Step())
                {
                    world.buildings.checkContainer();
                    Advance(SimulationStage.Drops);
                }

                break;
            case SimulationStage.Drops:
                world.drop_manager.update(cycleElapsed);
                Advance(SimulationStage.Cultures);
                break;
            case SimulationStage.Cultures:
                world.cultures.update(cycleElapsed);
                Advance(SimulationStage.StackEffects);
                break;
            case SimulationStage.StackEffects:
                world.stack_effects.update(cycleElapsed);
                Advance(SimulationStage.ResourceThrows);
                break;
            case SimulationStage.ResourceThrows:
                world.resource_throw_manager.update(cycleElapsed);
                listIndex = 0;
                stage = SimulationStage.WorldBehaviours;
                break;
            case SimulationStage.WorldBehaviours:
                if (!DebugConfig.isOn(DebugOption.SystemWorldBehaviours))
                {
                    listIndex = 0;
                    stage = SimulationStage.Armies;
                    break;
                }

                if (listIndex < worldBehaviours.Count)
                {
                    WorldBehaviourAsset behaviour = worldBehaviours[listIndex++];
                    if (behaviour.enabled)
                    {
                        behaviour.manager.update(cycleElapsed);
                    }
                }
                else
                {
                    listIndex = 0;
                    stage = SimulationStage.Armies;
                }

                break;
            case SimulationStage.Armies:
                world.armies.update(cycleElapsed);
                Advance(SimulationStage.Kingdoms);
                break;
            case SimulationStage.Kingdoms:
                world.kingdoms.update(cycleElapsed);
                Advance(SimulationStage.Diplomacy);
                break;
            case SimulationStage.Diplomacy:
                world.diplomacy.update(cycleElapsed);
                Advance(SimulationStage.Subspecies);
                break;
            case SimulationStage.Subspecies:
                world.subspecies.update(cycleElapsed);
                Advance(SimulationStage.Plots);
                break;
            case SimulationStage.Plots:
                world.plots.update(cycleElapsed);
                Advance(SimulationStage.Clans);
                break;
            case SimulationStage.Clans:
                world.clans.update(cycleElapsed);
                Advance(SimulationStage.Alliances);
                break;
            case SimulationStage.Alliances:
                world.alliances.update(cycleElapsed);
                Advance(SimulationStage.Wars);
                break;
            case SimulationStage.Wars:
                world.wars.update(cycleElapsed);
                Advance(SimulationStage.Languages);
                break;
            case SimulationStage.Languages:
                world.languages.update(cycleElapsed);
                Advance(SimulationStage.Religions);
                break;
            case SimulationStage.Religions:
                world.religions.update(cycleElapsed);
                Advance(SimulationStage.Projectiles);
                break;
            case SimulationStage.Projectiles:
                world.projectiles.update(cycleElapsed);
                Advance(SimulationStage.Statuses);
                break;
            case SimulationStage.Statuses:
                world.statuses.update(cycleElapsed);
                Advance(SimulationStage.Era);
                break;
            case SimulationStage.Era:
                world.era_manager.update(cycleElapsed);
                Advance(SimulationStage.DelayedActions);
                break;
            case SimulationStage.CultiwayStart:
                CultiwayLogicScheduler logicScheduler = ModClass.I.LogicScheduler;
                logicScheduler.StartCycle(new Friflo.Engine.ECS.UpdateTick(cycleElapsed, SimulationTime.NowFloat));
                ownsCultiwayCycle = logicScheduler.Active;
                Advance(ownsCultiwayCycle
                    ? SimulationStage.Cultiway
                    : SimulationStage.Complete);
                break;
            case SimulationStage.Cultiway:
                if (ModClass.I.LogicScheduler.Step())
                {
                    ownsCultiwayCycle = false;
                    Advance(SimulationStage.Complete);
                }

                break;
            case SimulationStage.DelayedActions:
                // 游戏速度相关的延迟动作逐基础 tick 推进；真实时间动作仍由 MapBox.Update 处理。
                advancingGameDelayedActions = true;
                try
                {
                    world.delayed_actions_manager.update(cycleElapsed, 0f);
                }
                finally
                {
                    advancingGameDelayedActions = false;
                }

                Advance(SimulationStage.CultiwayStart);
                break;
            case SimulationStage.Complete:
                CompleteCycle();
                break;
            case SimulationStage.Idle:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void CompleteCycle()
    {
        SimulationTime.CompleteTick();
        mapLayers.Clear();
        mapModules.Clear();
        worldBehaviours.Clear();
        world = null;
        listIndex = 0;
        stage = SimulationStage.Idle;
        logicalTicksCompleted++;
        FramePriorityGovernor.RecordVanillaCycleCompleted();
        FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, "idle");
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    private void Advance(SimulationStage nextStage)
    {
        stage = nextStage;
    }

    private static void ConfigureWorkers(MapBox map)
    {
        JobConst.MAX_ELEMENTS = PerformanceSettings.SimulationBatchSize;
        if (map?.parallel_options != null)
        {
            map.parallel_options.MaxDegreeOfParallelism = PerformanceSettings.WorkerCount;
        }
    }

    private void PrepareAdmissionCredits(MapBox map, bool allowNewCycles)
    {
        UpdateActualSpeed();

        WorldTimeScaleAsset timeScale = Config.time_scale_asset;
        float nextRequestedSpeed = Math.Max(0f, timeScale.multiplier) * Math.Max(1, timeScale.ticks);
        if (!ReferenceEquals(timeScale, lastTimeScaleAsset) ||
            Math.Abs(nextRequestedSpeed - lastRequestedSpeed) > 0.001f)
        {
            admissionCredits = 0.0;
            lastTimeScaleAsset = timeScale;
            lastRequestedSpeed = nextRequestedSpeed;
        }

        requestedSpeed = nextRequestedSpeed;
        if (!allowNewCycles ||
            !PerformanceSettings.EnableFramePriorityScheduler ||
            map.isPaused() ||
            requestedSpeed <= 0f)
        {
            admissionCredits = 0.0;
            return;
        }

        // 额度只是“允许开始”的节奏许可，不代表已经创建的逻辑 tick。
        // 容量饱和后停止生成许可，从源头背压，避免形成必须追赶或丢弃的无限债务。
        double capacity = Math.Max(1.0, requestedSpeed);
        double generatedCredits =
            Math.Max(0f, UnityEngine.Time.unscaledDeltaTime) *
            PerformanceSettings.BaseSimulationTicksPerSecond *
            requestedSpeed;
        admissionCredits = Math.Min(capacity, admissionCredits + generatedCredits);
    }

    private bool CanAdmitCycle(MapBox map, bool allowNewCycles)
    {
        return allowNewCycles &&
               PerformanceSettings.EnableFramePriorityScheduler &&
               admissionCredits >= 1.0 &&
               !map.isPaused() &&
               ModClass.I?.LogicScheduler.Active != true;
    }

    private void UpdateActualSpeed()
    {
        float now = UnityEngine.Time.unscaledTime;
        if (rateWindowStartedAt < 0f)
        {
            rateWindowStartedAt = now;
            completedAtRateWindowStart = logicalTicksCompleted;
            return;
        }

        float elapsed = now - rateWindowStartedAt;
        if (elapsed < 0.5f)
        {
            return;
        }

        long completed = logicalTicksCompleted - completedAtRateWindowStart;
        actualSpeed = completed * PerformanceSettings.FixedSimulationStepSeconds / elapsed;
        rateWindowStartedAt = now;
        completedAtRateWindowStart = logicalTicksCompleted;
    }
}
