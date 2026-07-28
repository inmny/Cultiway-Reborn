using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
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

    private readonly CooperativeBatchRunner<BatchActors, Actor> actorRunner =
        new(
            "vanilla.actors",
            new CooperativeActorPostRunner(),
            deferParallelToPresentation: true);
    private readonly CooperativeBatchRunner<BatchBuildings, Building> buildingRunner =
        new(
            "vanilla.buildings",
            deferParallelToPresentation: true);
    private readonly CooperativeWorldMaintenanceRunner maintenanceRunner = new();
    private readonly List<MapLayer> mapLayers = new();
    private readonly List<BaseModule> mapModules = new();
    private readonly List<WorldBehaviourAsset> worldBehaviours = new();
    private MapBox world;
    private WorldTimeScaleAsset cycleTimeScale;
    private SimulationStage stage;
    private float cycleElapsed;
    private float logicCycleElapsed;
    private bool cyclePaused;
    private bool cycleUsesVanillaLargeStep;
    private int simulationPassesRemaining;
    private int listIndex;
    private double admissionCredits;
    private float lastRequestedSpeed = -1f;
    private bool lastLargeStepMode;
    private WorldTimeScaleAsset lastTimeScaleAsset;
    private int lastControlledFrame = -1;
    private bool ownsCultiwayCycle;
    private bool advancingGameDelayedActions;
    private long logicalTicksAdmitted;
    private long logicalTicksCompleted;
    private float requestedSpeed;
    private string admissionBlockReason = "not_prepared";
    private long presentationOverlapLaunches;
    private long presentationOverlapCompletions;
    private long presentationOverlapFallbacks;
    private long presentationOverlapForcedJoins;
    private long presentationOverlapWallTicks;
    private long presentationOverlapWaitTicks;
    private long lastPresentationOverlapWallTicks;
    private long lastPresentationOverlapWaitTicks;
    private long presentationReadOnlyBoundaries;
    private long presentationReadOnlyForcedWaits;
    private long presentationReadOnlyWaitTicks;
    private string lastPresentationBoundaryReason = "none";
    private long buildingPresentationOverlapLaunches;
    private long buildingPresentationOverlapCompletions;
    private long buildingPresentationOverlapFallbacks;
    private long buildingPresentationOverlapForcedJoins;
    private long buildingPresentationOverlapWallTicks;
    private long buildingPresentationOverlapWaitTicks;
    private long lastBuildingPresentationOverlapWallTicks;
    private long lastBuildingPresentationOverlapWaitTicks;
    private string lastBuildingPresentationBoundaryReason = "none";

    public bool Active => stage != SimulationStage.Idle;
    public bool IsAtCycleBoundary => !Active;
    public bool RequiresControl => PerformanceSettings.EnableFramePriorityScheduler || Active;
    public bool ControlledThisFrame => lastControlledFrame == UnityEngine.Time.frameCount;
    internal bool HasMutatingPresentationWorkInFlight =>
        actorRunner.MutatingParallelWorkInFlight ||
        buildingRunner.MutatingParallelWorkInFlight;
    public bool OwnsCultiwayCycle => ownsCultiwayCycle;
    public bool IsAdvancingGameDelayedActions => advancingGameDelayedActions;
    public long LogicalTicksAdmitted => logicalTicksAdmitted;
    public long LogicalTicksCompleted => logicalTicksCompleted;
    public float RequestedSpeed => requestedSpeed;
    public float ActualSpeed => WorldTimeRateTracker.ActualSpeed;
    public double AdmissionCredits => admissionCredits;
    public string AdmissionBlockReason => admissionBlockReason;
    public string AdmissionMode => !PerformanceSettings.EnableFramePriorityScheduler
        ? "native"
        : PerformanceSettings.EnableVanillaLargeSimulationStep
            ? "large"
            : "fixed";

    public void RunFrame(MapBox map, bool allowNewCycles = true)
    {
        FramePriorityGovernor.BeginFrame();
        JobConst.MAX_ELEMENTS = PerformanceSettings.SimulationBatchSize;
        PerformanceSettings.ApplyParallelBudget(map);
        lastControlledFrame = UnityEngine.Time.frameCount;

        if (Active && !ReferenceEquals(world, map))
        {
            Abort();
        }

        PrepareAdmissionCredits(map, allowNewCycles);

        while (true)
        {
            if ((stage == SimulationStage.Actors &&
                 actorRunner.WaitingForPresentationDispatch) ||
                (stage == SimulationStage.Buildings &&
                 buildingRunner.WaitingForPresentationDispatch))
            {
                FramePriorityGovernor.SetPhase(
                    SimulationDomain.Vanilla,
                    GetNextPhaseName());
                break;
            }

            bool actorBackgroundPending =
                actorRunner.WaitingForBackgroundWork &&
                !actorRunner.IsBackgroundWorkCompleted;
            bool buildingBackgroundPending =
                buildingRunner.WaitingForBackgroundWork &&
                !buildingRunner.IsBackgroundWorkCompleted;
            if (actorBackgroundPending || buildingBackgroundPending)
            {
                // 先用极短窗口吸收即将完成的任务；超时后立刻交还渲染帧，
                // 下一帧仍从同一有序提交屏障继续。
                string awaitPhase = actorBackgroundPending
                    ? actorRunner.GetNextPhaseName()
                    : buildingRunner.GetNextPhaseName();
                if (!FramePriorityGovernor.CanRun(SimulationDomain.Vanilla, awaitPhase))
                {
                    FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, awaitPhase);
                    break;
                }

                bool joined = false;
                FramePriorityGovernor.RunPhase(
                    SimulationDomain.Vanilla,
                    awaitPhase,
                    () => joined = actorBackgroundPending
                        ? actorRunner.TryJoinBackgroundWork(
                            PerformanceSettings.BackgroundJoinMilliseconds)
                        : buildingRunner.TryJoinBackgroundWork(
                            PerformanceSettings.BackgroundJoinMilliseconds));
                if (!joined)
                {
                    FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, awaitPhase);
                    break;
                }

                continue;
            }

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
                    () => StartAdmissionCycle(map));
                continue;
            }

            string phase = GetNextPhaseName();
            SimulationDomain domain = GetCurrentDomain();
            bool canRun = FramePriorityGovernor.CanRun(domain, phase);
            bool forceBoundaryCommit =
                !canRun &&
                stage == SimulationStage.Cultiway &&
                ModClass.I.LogicScheduler.IsAtGroupCommitBoundary;
            if (!canRun && !forceBoundaryCommit)
            {
                FramePriorityGovernor.SetPhase(domain, phase);
                break;
            }

            FramePriorityGovernor.RunPhase(
                domain,
                phase,
                ExecuteCurrentStage);
            if (forceBoundaryCommit)
            {
                break;
            }
        }
    }

    public bool TryBeginActorPresentationOverlap()
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            ActorPresentationRenderer.PreparedSnapshot == null ||
            stage != SimulationStage.Actors ||
            !actorRunner.BeginParallelPresentationWork())
        {
            return false;
        }

        Interlocked.Increment(ref presentationOverlapLaunches);
        return true;
    }

    public bool EnsureActorReadBoundary(string reason)
    {
        bool reachedBoundary = false;
        if (actorRunner.MutatingParallelWorkInFlight)
        {
            reachedBoundary = true;
            bool completedBeforeWait = actorRunner.IsBackgroundWorkCompleted;
            SimulationCoordinatorThread.WorkResult result =
                actorRunner.CompleteParallelPresentationWork();
            Interlocked.Increment(ref presentationOverlapCompletions);
            if (!completedBeforeWait)
            {
                Interlocked.Increment(ref presentationOverlapForcedJoins);
            }

            Interlocked.Add(
                ref presentationOverlapWallTicks,
                result.WallTicks);
            Interlocked.Add(
                ref presentationOverlapWaitTicks,
                result.WaitTicks);
            Interlocked.Exchange(
                ref lastPresentationOverlapWallTicks,
                result.WallTicks);
            Interlocked.Exchange(
                ref lastPresentationOverlapWaitTicks,
                result.WaitTicks);
            lastPresentationBoundaryReason =
                string.IsNullOrEmpty(reason) ? "unknown" : reason;
        }

        if (!actorRunner.WaitingForBackgroundWork)
        {
            return reachedBoundary;
        }

        reachedBoundary = true;
        bool readOnlyCompleted =
            actorRunner.IsBackgroundWorkCompleted;
        long waitStartedAt = Stopwatch.GetTimestamp();
        actorRunner.WaitForBackgroundWork();
        long waitTicks =
            Stopwatch.GetTimestamp() - waitStartedAt;
        Interlocked.Increment(
            ref presentationReadOnlyBoundaries);
        if (!readOnlyCompleted)
        {
            Interlocked.Increment(
                ref presentationReadOnlyForcedWaits);
        }

        Interlocked.Add(
            ref presentationReadOnlyWaitTicks,
            waitTicks);
        lastPresentationBoundaryReason =
            (string.IsNullOrEmpty(reason) ? "unknown" : reason) +
            ".readonly";
        return true;
    }

    public bool TryBeginBuildingPresentationOverlap()
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            WorldObjectPresentationRenderer.PreparedSnapshot == null ||
            stage != SimulationStage.Buildings ||
            !buildingRunner.BeginParallelPresentationWork())
        {
            return false;
        }

        Interlocked.Increment(ref buildingPresentationOverlapLaunches);
        return true;
    }

    public bool EnsureBuildingReadBoundary(string reason)
    {
        if (!buildingRunner.MutatingParallelWorkInFlight)
        {
            return false;
        }

        bool completedBeforeWait = buildingRunner.IsBackgroundWorkCompleted;
        SimulationCoordinatorThread.WorkResult result =
            buildingRunner.CompleteParallelPresentationWork();
        Interlocked.Increment(ref buildingPresentationOverlapCompletions);
        if (!completedBeforeWait)
        {
            Interlocked.Increment(
                ref buildingPresentationOverlapForcedJoins);
        }

        Interlocked.Add(
            ref buildingPresentationOverlapWallTicks,
            result.WallTicks);
        Interlocked.Add(
            ref buildingPresentationOverlapWaitTicks,
            result.WaitTicks);
        Interlocked.Exchange(
            ref lastBuildingPresentationOverlapWallTicks,
            result.WallTicks);
        Interlocked.Exchange(
            ref lastBuildingPresentationOverlapWaitTicks,
            result.WaitTicks);
        lastBuildingPresentationBoundaryReason =
            string.IsNullOrEmpty(reason) ? "unknown" : reason;
        return true;
    }

    public void FinishPresentationFrame()
    {
        if (stage == SimulationStage.Actors &&
            actorRunner.WaitingForPresentationDispatch)
        {
            // 没有进入角色主体绘制（例如地图视图或窗口遮挡）时，
            // 帧尾仍调度一个 job 组，避免模拟因等待表现切点而停滞。
            if (actorRunner.BeginParallelPresentationWork())
            {
                Interlocked.Increment(ref presentationOverlapLaunches);
                Interlocked.Increment(ref presentationOverlapFallbacks);
            }
        }

        if (stage == SimulationStage.Buildings &&
            buildingRunner.WaitingForPresentationDispatch)
        {
            if (buildingRunner.BeginParallelPresentationWork())
            {
                Interlocked.Increment(
                    ref buildingPresentationOverlapLaunches);
                Interlocked.Increment(
                    ref buildingPresentationOverlapFallbacks);
            }
        }
    }

    public string GetPresentationOverlapDiagnostics()
    {
        long launches = Interlocked.Read(ref presentationOverlapLaunches);
        long completions =
            Interlocked.Read(ref presentationOverlapCompletions);
        return string.Format(
            CultureInfo.InvariantCulture,
            "launch={0} complete={1} fallback={2} forced_join={3} " +
            "wall={4:0.0}ms wait={5:0.0}ms last={6:0.00}/{7:0.00}ms " +
            "readonly={11}/{12}/{13:0.0}ms " +
            "boundary={8} dispatch_wait={9} inflight={10}",
            launches,
            completions,
            Interlocked.Read(ref presentationOverlapFallbacks),
            Interlocked.Read(ref presentationOverlapForcedJoins),
            TicksToMilliseconds(
                Interlocked.Read(ref presentationOverlapWallTicks)),
            TicksToMilliseconds(
                Interlocked.Read(ref presentationOverlapWaitTicks)),
            TicksToMilliseconds(
                Interlocked.Read(ref lastPresentationOverlapWallTicks)),
            TicksToMilliseconds(
                Interlocked.Read(ref lastPresentationOverlapWaitTicks)),
            lastPresentationBoundaryReason,
            actorRunner.WaitingForPresentationDispatch,
            actorRunner.MutatingParallelWorkInFlight,
            Interlocked.Read(ref presentationReadOnlyBoundaries),
            Interlocked.Read(ref presentationReadOnlyForcedWaits),
            TicksToMilliseconds(
                Interlocked.Read(
                    ref presentationReadOnlyWaitTicks)));
    }

    public string GetBuildingPresentationOverlapDiagnostics()
    {
        long launches =
            Interlocked.Read(ref buildingPresentationOverlapLaunches);
        long completions =
            Interlocked.Read(ref buildingPresentationOverlapCompletions);
        return string.Format(
            CultureInfo.InvariantCulture,
            "launch={0} complete={1} fallback={2} forced_join={3} " +
            "wall={4:0.0}ms wait={5:0.0}ms last={6:0.00}/{7:0.00}ms " +
            "boundary={8} dispatch_wait={9} inflight={10}",
            launches,
            completions,
            Interlocked.Read(ref buildingPresentationOverlapFallbacks),
            Interlocked.Read(ref buildingPresentationOverlapForcedJoins),
            TicksToMilliseconds(
                Interlocked.Read(ref buildingPresentationOverlapWallTicks)),
            TicksToMilliseconds(
                Interlocked.Read(ref buildingPresentationOverlapWaitTicks)),
            TicksToMilliseconds(
                Interlocked.Read(
                    ref lastBuildingPresentationOverlapWallTicks)),
            TicksToMilliseconds(
                Interlocked.Read(
                    ref lastBuildingPresentationOverlapWaitTicks)),
            lastBuildingPresentationBoundaryReason,
            buildingRunner.WaitingForPresentationDispatch,
            buildingRunner.MutatingParallelWorkInFlight);
    }

    public void Abort()
    {
        actorRunner.Abort();
        buildingRunner.Abort();
        maintenanceRunner.Abort();
        SimulationTickBenchmark.AbortCurrentTick();
        if (ownsCultiwayCycle)
        {
            ModClass.I?.LogicScheduler.Abort();
        }

        SimulationTime.CancelTick();
        mapLayers.Clear();
        mapModules.Clear();
        worldBehaviours.Clear();
        world = null;
        cycleTimeScale = null;
        stage = SimulationStage.Idle;
        listIndex = 0;
        admissionCredits = 0.0;
        simulationPassesRemaining = 0;
        cycleUsesVanillaLargeStep = false;
        ownsCultiwayCycle = false;
        advancingGameDelayedActions = false;
        ActorPresentationSnapshots.Reset();
        ActorPresentationRenderer.Reset();
        WorldObjectPresentationRenderer.Reset();
        PresentationInterpolator.Reset();
        FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, "idle");
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    public void DrainToBoundary()
    {
        SimulationTickBenchmark.Suspend();
        try
        {
            while (Active)
            {
                if (stage == SimulationStage.Actors &&
                    actorRunner.WaitingForPresentationDispatch)
                {
                    actorRunner.BeginParallelPresentationWork();
                }

                if (stage == SimulationStage.Buildings &&
                    buildingRunner.WaitingForPresentationDispatch)
                {
                    buildingRunner.BeginParallelPresentationWork();
                }

                actorRunner.WaitForBackgroundWork();
                buildingRunner.WaitForBackgroundWork();
                ExecuteCurrentStage();
            }
        }
        finally
        {
            SimulationTickBenchmark.Resume();
        }
    }

    private void StartAdmissionCycle(MapBox map)
    {
        world = map;
        cyclePaused = map.isPaused();
        cycleUsesVanillaLargeStep = PerformanceSettings.EnableVanillaLargeSimulationStep;
        cycleTimeScale = Config.time_scale_asset;
        if (cycleUsesVanillaLargeStep)
        {
            cycleElapsed = PerformanceSettings.FixedSimulationStepSeconds *
                           Math.Max(0f, cycleTimeScale.multiplier);
            logicCycleElapsed = PerformanceSettings.FixedSimulationStepSeconds;
            simulationPassesRemaining = Math.Max(1, cycleTimeScale.ticks);
        }
        else
        {
            cycleElapsed = PerformanceSettings.FixedSimulationStepSeconds;
            logicCycleElapsed = cycleElapsed;
            simulationPassesRemaining = 1;
        }

        StartSimulationPass();
    }

    private void StartSimulationPass()
    {
        SimulationTime.BeginTick(world, cycleElapsed);
        SimulationTickBenchmark.BeginTick(cycleElapsed, cycleUsesVanillaLargeStep);
        mapLayers.Clear();
        mapLayers.AddRange(world._map_layers);
        mapModules.Clear();
        mapModules.AddRange(world._map_modules);
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
        SimulationStepContext.Run(
            world,
            cyclePaused,
            cycleElapsed,
            !cycleUsesVanillaLargeStep,
            cycleTimeScale,
            ExecuteCurrentStageCore);
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
                actorRunner.Start(
                    actorManager,
                    actorManager.active_batches,
                    cycleElapsed,
                    world.parallel_options);
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
                    cycleElapsed,
                    world.parallel_options);
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
                // 原版会先完成本帧全部 ticks，再执行一次帧级延迟动作和 Mod 逻辑。
                Advance(cycleUsesVanillaLargeStep && simulationPassesRemaining > 1
                    ? SimulationStage.Complete
                    : SimulationStage.DelayedActions);
                break;
            case SimulationStage.CultiwayStart:
                CultiwayLogicScheduler logicScheduler = ModClass.I.LogicScheduler;
                logicScheduler.StartCycle(
                    new Friflo.Engine.ECS.UpdateTick(logicCycleElapsed, SimulationTime.NowFloat));
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
                // 游戏速度相关的延迟动作按当前模拟步推进；真实时间动作仍由 MapBox.Update 处理。
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
        SimulationTime.CompleteTick(world);
        ActorPresentationSnapshots.CaptureIfRequested(
            world,
            logicalTicksCompleted + 1);
        SimulationTickBenchmark.MarkTickCompleted();
        mapLayers.Clear();
        mapModules.Clear();
        worldBehaviours.Clear();
        listIndex = 0;
        logicalTicksCompleted++;
        FramePriorityGovernor.RecordVanillaCycleCompleted();

        simulationPassesRemaining--;
        if (simulationPassesRemaining > 0)
        {
            StartSimulationPass();
            return;
        }

        world = null;
        cycleTimeScale = null;
        stage = SimulationStage.Idle;
        cycleUsesVanillaLargeStep = false;
        FramePriorityGovernor.SetPhase(SimulationDomain.Vanilla, "idle");
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    private void Advance(SimulationStage nextStage)
    {
        stage = nextStage;
    }

    private void PrepareAdmissionCredits(MapBox map, bool allowNewCycles)
    {
        WorldTimeScaleAsset timeScale = Config.time_scale_asset;
        bool largeStepMode = PerformanceSettings.EnableVanillaLargeSimulationStep;
        float nextRequestedSpeed = Math.Max(0f, timeScale.multiplier) * Math.Max(1, timeScale.ticks);
        if (!ReferenceEquals(timeScale, lastTimeScaleAsset) ||
            largeStepMode != lastLargeStepMode ||
            Math.Abs(nextRequestedSpeed - lastRequestedSpeed) > 0.001f)
        {
            admissionCredits = 0.0;
            lastTimeScaleAsset = timeScale;
            lastLargeStepMode = largeStepMode;
            lastRequestedSpeed = nextRequestedSpeed;
        }

        requestedSpeed = nextRequestedSpeed;
        if (!allowNewCycles)
        {
            admissionCredits = 0.0;
            admissionBlockReason = "initialization";
            return;
        }

        if (!PerformanceSettings.EnableFramePriorityScheduler)
        {
            admissionCredits = 0.0;
            admissionBlockReason = "disabled";
            return;
        }

        if (map.isPaused())
        {
            admissionCredits = 0.0;
            admissionBlockReason = "paused";
            return;
        }

        if (requestedSpeed <= 0f)
        {
            admissionCredits = 0.0;
            admissionBlockReason = "zero_speed";
            return;
        }

        // 额度只是“允许开始”的节奏许可，不代表已经创建的逻辑 tick。
        // 大步模式的一份额度会按原版 ticks 连续执行多轮放大时间步，
        // 固定步模式的一份额度仍只代表一个 0.02 秒完整 tick。
        double admissionRate = PerformanceSettings.BaseSimulationTicksPerSecond *
                               (largeStepMode ? 1.0 : requestedSpeed);
        // 最多保留一秒的目标额度，既允许低帧率下一帧启动足量工作，
        // 又避免性能不足时形成必须无限追赶的长期债务。
        double capacity = Math.Max(1.0, admissionRate);
        double generatedCredits =
            Math.Max(0f, UnityEngine.Time.unscaledDeltaTime) *
            admissionRate;
        admissionCredits = Math.Min(capacity, admissionCredits + generatedCredits);
        admissionBlockReason = admissionCredits >= 1.0
            ? "ready"
            : "credit";
    }

    private bool CanAdmitCycle(MapBox map, bool allowNewCycles)
    {
        return allowNewCycles &&
               PerformanceSettings.EnableFramePriorityScheduler &&
               admissionCredits >= 1.0 &&
               !map.isPaused() &&
               ModClass.I?.LogicScheduler.Active != true;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
