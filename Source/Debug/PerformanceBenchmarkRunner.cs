using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Performance;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Debug;

public sealed class PerformanceBenchmarkRunner : MonoBehaviour
{
    private const string Prefix = "[PerfBenchmark]";
    private const float SetupDelaySeconds = 2f;

    private static readonly string[] DefaultGameTotalEntries =
    {
        "game_total",
        "actors",
        "cities",
        "buildings",
        "world_beh",
        "mapbox_update_1",
        "taxi",
        "update_meta_history",
        "nameplates",
        "quantum_sprites",
        "update_sprite_constructor",
        "light_renderer",
        "end_checks"
    };

    private enum RunnerState
    {
        WaitingForGame,
        PreparingWorld,
        WaitingForWorldLoaded,
        SpawningInitialUnits,
        WarmingUp,
        Measuring,
        Complete
    }

    private readonly List<float> _frameTimesMs = new(8192);
    private RunnerState _state = RunnerState.WaitingForGame;
    private string _mode;
    private string _mapSize;
    private string _mapTemplate;
    private string _speedId;
    private int _worldSeed;
    private int _initialHumans;
    private int _startMeasureUnits;
    private float _durationSeconds;
    private float _warmupMaxSeconds;
    private float _settleSeconds;
    private float _logIntervalSeconds;
    private bool _createWorld;
    private bool _quitOnComplete;
    private bool _captureDetails;
    private bool _scanInvalidHandRenderers;
    private bool _configured;
    private bool _initialUnitsSpawned;
    private bool _presentationScenePrepared;
    private bool _presentationStressEnabled;
    private int _initialHumansProcessed;
    private Actor _cameraAnchor;
    private Actor _presentationTarget;
    private Building _presentationBuilding;
    private ResourceAsset _presentationThrowResource;
    private WorldTile _presentationFireTile;
    private float _stateElapsed;
    private float _runElapsed;
    private float _logElapsed;
    private float _presentationStressElapsed;
    private int _presentationProjectileCount;
    private int _presentationThrowCount;
    private int _presentationMissingBuildingCount;
    private int _presentationMissingResourceSpriteCount;
    private double _frameTimeSumMs;
    private float _frameTimeMaxMs;
    private int _framesOver33Ms;
    private int _framesOver50Ms;
    private int _framesOver100Ms;
    private readonly HashSet<string> _reportedInvalidHandRenderers = new();
    private float _handRendererScanElapsed;
    private double _measurementStartWorldTime;
    private long _measurementStartLogicalTicks;
    private long _measurementStartedAt;

    internal static bool IsAutomationRequested =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("CULTIWAY_PERF_AUTO"));

    public static void Install(GameObject host)
    {
        string mode = Environment.GetEnvironmentVariable("CULTIWAY_PERF_AUTO");
        if (string.IsNullOrWhiteSpace(mode) || host == null)
        {
            return;
        }

        if (host.GetComponent<PerformanceBenchmarkRunner>() != null)
        {
            return;
        }

        var runner = host.AddComponent<PerformanceBenchmarkRunner>();
        runner.Configure(mode);
    }

    private void Configure(string mode)
    {
        // 自动基准常以隐藏窗口运行；不允许 Unity 因失焦把主循环降到约 1 FPS。
        Application.runInBackground = true;
        _mode = mode.Trim();
        _presentationStressEnabled =
            _mode.IndexOf(
                "presentation_snapshot",
                StringComparison.OrdinalIgnoreCase) >= 0;
        _mapSize = GetEnvString("CULTIWAY_PERF_MAP_SIZE", MapSizeLibrary.iceberg);
        _mapTemplate = GetEnvString("CULTIWAY_PERF_MAP_TEMPLATE", Config.current_map_template);
        _speedId = GetEnvString("CULTIWAY_PERF_SPEED", "x40");
        _worldSeed = GetEnvInt("CULTIWAY_PERF_WORLD_SEED", 0);
        _initialHumans = GetEnvInt("CULTIWAY_PERF_INITIAL_HUMANS", 10000);
        _startMeasureUnits = GetEnvInt("CULTIWAY_PERF_START_MEASURE_UNITS", 10000);
        _durationSeconds = GetEnvFloat("CULTIWAY_PERF_DURATION", 180f);
        _warmupMaxSeconds = GetEnvFloat("CULTIWAY_PERF_WARMUP_MAX", 900f);
        _settleSeconds = Math.Max(
            0f,
            GetEnvFloat("CULTIWAY_PERF_SETTLE_DURATION", 0f));
        _logIntervalSeconds = Math.Max(5f, GetEnvFloat("CULTIWAY_PERF_LOG_INTERVAL", 30f));
        _createWorld = GetEnvBool("CULTIWAY_PERF_CREATE_WORLD", true);
        _quitOnComplete = GetEnvBool("CULTIWAY_PERF_QUIT_ON_DONE", false);
        _captureDetails = GetEnvBool(
            "CULTIWAY_PERF_CAPTURE_DETAILS",
            true);
        _scanInvalidHandRenderers = GetEnvBool(
            "CULTIWAY_PERF_SCAN_INVALID_HAND_RENDERERS",
            false);
        PerformanceSettings.SetTargetRenderFps(
            GetEnvFloat("CULTIWAY_PERF_TARGET_FPS", PerformanceSettings.TargetRenderFps));
        PerformanceSettings.SetMaxSimulationMillisecondsPerFrame(
            GetEnvFloat(
                "CULTIWAY_PERF_MAX_SIMULATION_MS",
                PerformanceSettings.MaxSimulationMillisecondsPerFrame));
        PerformanceSettings.SwitchPresentationSmoothing(
            GetEnvBool(
                "CULTIWAY_PERF_PRESENTATION_SMOOTHING",
                PerformanceSettings.EnablePresentationSmoothing));
        SimulationTickBenchmark.SetAiDetailsOverride(
            GetEnvNullableBool("CULTIWAY_PERF_AI_DETAILS"));
        _configured = true;

        ModClass.LogInfo(
            $"{Prefix} 已启用 mode={_mode} mapSize={_mapSize} template={_mapTemplate} speed={_speedId} seed={_worldSeed} initialHumans={_initialHumans} startMeasureUnits={_startMeasureUnits} duration={_durationSeconds:0.#}s settle={_settleSeconds:0.#}s warmupMax={_warmupMaxSeconds:0.#}s targetFps={PerformanceSettings.TargetRenderFps:0.#} maxSimulation={PerformanceSettings.MaxSimulationMillisecondsPerFrame:0.#}ms smoothing={PerformanceSettings.EnablePresentationSmoothing} details={_captureDetails} handScan={_scanInvalidHandRenderers} aiBench={SimulationTickBenchmark.ShouldCollectAiDetails}");
    }

    private void Update()
    {
        if (!_configured)
        {
            return;
        }

        float delta = Time.unscaledDeltaTime;
        _stateElapsed += delta;

        if (_state is RunnerState.WarmingUp or RunnerState.Measuring)
        {
            RecordFrame(delta);
            if (_scanInvalidHandRenderers)
            {
                ScanInvalidHandRenderers(delta);
            }

            UpdatePresentationStress(delta);
        }

        try
        {
            switch (_state)
            {
                case RunnerState.WaitingForGame:
                    UpdateWaitingForGame();
                    break;
                case RunnerState.PreparingWorld:
                    UpdatePreparingWorld();
                    break;
                case RunnerState.WaitingForWorldLoaded:
                    UpdateWaitingForWorldLoaded();
                    break;
                case RunnerState.SpawningInitialUnits:
                    UpdateSpawningInitialUnits();
                    break;
                case RunnerState.WarmingUp:
                    UpdateWarmingUp(delta);
                    break;
                case RunnerState.Measuring:
                    UpdateMeasuring(delta);
                    break;
            }
        }
        catch (Exception e)
        {
            ModClass.LogErrorConcurrent($"{Prefix} {e}");
            enabled = false;
        }
    }

    private void UpdateWaitingForGame()
    {
        if (!Config.game_loaded || World.world == null || AssetManager.actor_library == null)
        {
            return;
        }

        SetState(RunnerState.PreparingWorld);
    }

    private void UpdatePreparingWorld()
    {
        Bench.bench_enabled = false;
        DebugConfig.setOption(DebugOption.BenchAiEnabled, false);
        Bench.bench_ai_enabled = false;
        EnsureSimulationPaused();

        if (_createWorld)
        {
            Config.customMapSize = _mapSize;
            Config.current_map_template = _mapTemplate;
            if (_worldSeed != 0)
            {
                Randy.resetSeed(_worldSeed);
            }

            ModClass.LogInfo($"{Prefix} 生成新世界 mapSize={_mapSize} template={_mapTemplate}");
            World.world.generateNewMap();
            SetState(RunnerState.WaitingForWorldLoaded);
            return;
        }

        SetState(RunnerState.SpawningInitialUnits);
    }

    private void UpdateWaitingForWorldLoaded()
    {
        EnsureSimulationPaused();
        if (SmoothLoader.isLoading() ||
            _stateElapsed < SetupDelaySeconds ||
            !IsCultiwayWorldReady())
        {
            return;
        }

        ModClass.LogInfo(
            $"{Prefix} 世界与 GeoRegion 索引均已就绪 elapsed={_stateElapsed:0.0}s");
        SetState(RunnerState.SpawningInitialUnits);
    }

    private void UpdateSpawningInitialUnits()
    {
        if (SmoothLoader.isLoading() ||
            _stateElapsed < SetupDelaySeconds ||
            !IsCultiwayWorldReady())
        {
            return;
        }

        EnsureSimulationPaused();

        if (!_initialUnitsSpawned && _initialHumans > 0)
        {
            int requestCount = Math.Min(250, _initialHumans - _initialHumansProcessed);
            int spawned = SpawnInitialHumans(requestCount);
            _initialHumansProcessed += requestCount;
            _initialUnitsSpawned = _initialHumansProcessed >= _initialHumans;
            if (!_initialUnitsSpawned)
            {
                return;
            }

            ModClass.LogInfo(
                $"{Prefix} 初始人类投放完成 requested={_initialHumans} units={CountUnits()} cities={CountCities()} lastBatchSpawned={spawned}");
        }

        PreparePresentationScene();

        if ((_startMeasureUnits > 0 &&
             CountUnits() < _startMeasureUnits) ||
            _settleSeconds > 0f)
        {
            ResetFrameStats();
            SetState(RunnerState.WarmingUp);
            return;
        }

        StartMeasurement();
    }

    private void UpdateWarmingUp(float delta)
    {
        EnsureSimulationRunning();
        _runElapsed += delta;
        _logElapsed += delta;

        bool populationReady =
            _startMeasureUnits <= 0 ||
            CountUnits() >= _startMeasureUnits;
        if (populationReady && _runElapsed >= _settleSeconds)
        {
            ModClass.LogInfo(
                $"{Prefix} 预热完成 units={CountUnits()} elapsed={_runElapsed:0.0}s settle={_settleSeconds:0.0}s");
            StartMeasurement();
            return;
        }

        if (_logElapsed >= _logIntervalSeconds)
        {
            LogWarmup();
            _logElapsed = 0f;
        }

        if (_warmupMaxSeconds > 0f && _runElapsed >= _warmupMaxSeconds)
        {
            ModClass.LogWarningConcurrent(
                $"{Prefix} warmup 超时，未达到人口阈值，仍开始统计 units={CountUnits()} target={_startMeasureUnits} elapsed={_runElapsed:0.0}s");
            StartMeasurement();
        }
    }

    private void StartMeasurement()
    {
        Bench.bench_enabled = _captureDetails;
        if (_captureDetails)
        {
            SimulationTickBenchmark.ApplyAiDetailsPolicy();
        }
        else
        {
            DebugConfig.setOption(
                DebugOption.BenchAiEnabled,
                false);
            Bench.bench_ai_enabled = false;
        }

        ResetFrameStats();
        _runElapsed = 0f;
        _logElapsed = 0f;
        _measurementStartWorldTime =
            World.world?.map_stats?.world_time ?? 0.0;
        _measurementStartLogicalTicks =
            CooperativeSimulationRunner.Instance
                .LogicalTicksCompleted;
        _measurementStartedAt =
            System.Diagnostics.Stopwatch.GetTimestamp();
        SetState(RunnerState.Measuring);
        ModClass.LogInfo(
            $"{Prefix} 开始统计 units={CountUnits()} cities={CountCities()} speed={_speedId} world={_measurementStartWorldTime:0.000} ticks={_measurementStartLogicalTicks}");
    }

    private void UpdateMeasuring(float delta)
    {
        EnsureSimulationRunning();
        _runElapsed += delta;
        _logElapsed += delta;

        if (_durationSeconds > 0f && _runElapsed >= _durationSeconds)
        {
            LogMeasurement("final");
            SetState(RunnerState.Complete);
            ModClass.LogInfo($"{Prefix} 统计结束 elapsed={_runElapsed:0.0}s units={CountUnits()}");
            if (_quitOnComplete)
            {
                Application.Quit();
            }
            return;
        }

        if (_logElapsed >= _logIntervalSeconds)
        {
            LogMeasurement("interval");
            ResetFrameStats();
            _logElapsed = 0f;
        }
    }

    private void LogWarmup()
    {
        var frameStats = SnapshotFrameStats();
        ModClass.LogInfo(
            $"{Prefix} warmup elapsed={_runElapsed:0.0}s units={CountUnits()}/{_startMeasureUnits} cities={CountCities()} kingdoms={CountKingdoms()} fpsNow={FPS.getFPS()} frameAvg={frameStats.AvgMs:0.00}ms frameMax={frameStats.MaxMs:0.00}ms minFps={frameStats.MinFps:0.0}");
        ResetFrameStats();
    }

    private void EnsureSimulationRunning()
    {
        CloseBlockingWindows();
        Config.paused = false;
        Config.setWorldSpeed(_speedId);
    }

    private void EnsureSimulationPaused()
    {
        CloseBlockingWindows();
        Config.paused = true;
        Config.setWorldSpeed(_speedId);
    }

    private static void CloseBlockingWindows()
    {
        if (ScrollWindow.isWindowActive())
        {
            // 批处理启动时可能保留加载页或模组窗口。MapBox 会把窗口状态计入暂停，
            // 因此仅设置 Config.paused=false 并不足以启动自动化模拟。
            ScrollWindow.moveAllToRightAndRemove(false);
        }
    }

    private void LogMeasurement(string phase)
    {
        var frameStats = SnapshotFrameStats();
        var sb = new StringBuilder(2048);
        double measurementWallSeconds =
            _measurementStartedAt <= 0L
                ? Math.Max(0.0, _runElapsed)
                : Math.Max(
                    0.0,
                    (System.Diagnostics.Stopwatch.GetTimestamp() -
                     _measurementStartedAt) /
                    (double)System.Diagnostics.Stopwatch.Frequency);
        double currentWorldTime =
            World.world?.map_stats?.world_time ??
            _measurementStartWorldTime;
        double measurementWorldSeconds =
            Math.Max(
                0.0,
                currentWorldTime -
                _measurementStartWorldTime);
        long currentLogicalTicks =
            CooperativeSimulationRunner.Instance
                .LogicalTicksCompleted;
        long measurementLogicalTicks =
            Math.Max(
                0L,
                currentLogicalTicks -
                _measurementStartLogicalTicks);
        double measuredActualSpeed =
            measurementWallSeconds > 0.0
                ? measurementWorldSeconds /
                  measurementWallSeconds
                : 0.0;
        double measuredTicksPerSecond =
            measurementWallSeconds > 0.0
                ? measurementLogicalTicks /
                  measurementWallSeconds
                : 0.0;
        sb.Append(Prefix)
            .Append(' ')
            .Append(phase)
            .Append(" elapsed=").Append(_runElapsed.ToString("0.0", CultureInfo.InvariantCulture)).Append('s')
            .Append(" wall=").Append(measurementWallSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append('s')
            .Append(" worldDelta=").Append(measurementWorldSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append('s')
            .Append(" measured=").Append(measuredActualSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append('x')
            .Append(" ticksDelta=").Append(measurementLogicalTicks)
            .Append(" tickRate=").Append(measuredTicksPerSecond.ToString("0.0", CultureInfo.InvariantCulture)).Append("/s")
            .Append(" units=").Append(CountUnits())
            .Append(" cities=").Append(CountCities())
            .Append(" kingdoms=").Append(CountKingdoms())
            .Append(" fpsNow=").Append(FPS.getFPS())
            .Append(" frameAvg=").Append(frameStats.AvgMs.ToString("0.00", CultureInfo.InvariantCulture)).Append("ms")
            .Append(" frameMax=").Append(frameStats.MaxMs.ToString("0.00", CultureInfo.InvariantCulture)).Append("ms")
            .Append(" frameP95=").Append(frameStats.P95Ms.ToString("0.00", CultureInfo.InvariantCulture)).Append("ms")
            .Append(" frameP99=").Append(frameStats.P99Ms.ToString("0.00", CultureInfo.InvariantCulture)).Append("ms")
            .Append(" minFps=").Append(frameStats.MinFps.ToString("0.0", CultureInfo.InvariantCulture))
            .Append(" over33=").Append(frameStats.Over33)
            .Append(" over50=").Append(frameStats.Over50)
            .Append(" over100=").Append(frameStats.Over100)
            .AppendLine();

        if (_captureDetails)
        {
            AppendBenchSummary(
                sb,
                "main",
                "game_total",
                "main",
                8);
            AppendBenchSummary(
                sb,
                "game_total",
                "game_total",
                "main",
                12,
                DefaultGameTotalEntries);
            AppendBenchSummary(
                sb,
                "sim_zones",
                "sim_zones",
                "game_total",
                12);
        }

        sb.Append("  scheduler ").Append(FramePriorityGovernor.GetDiagnostics()).AppendLine();
        sb.Append("  initialization_gate ")
            .Append(global::Cultiway.Patch.PatchFramePriorityScheduler.GetInitializationDiagnostics())
            .AppendLine();
        sb.Append("  cultiway_scheduler ").Append(ModClass.I.LogicScheduler.GetDiagnostics()).AppendLine();
        sb.Append("  worker_pool ").Append(SimulationWorkerPool.Instance.GetDiagnostics()).AppendLine();
        sb.Append("  actor_parallel ")
            .Append(CooperativeActorParallelJobRunner.GetDiagnostics())
            .AppendLine();
        sb.Append("  inside_boat_index ")
            .Append(InsideBoatActorIndex.GetDiagnostics())
            .AppendLine();
        sb.Append("  pathfinder_runtime ")
            .Append(global::Cultiway.Core.Pathfinding.PathFinder.Instance.GetDiagnostics())
            .AppendLine();
        sb.Append("  deferred_path_requests ")
            .Append(DeferredPathRequestBatch.GetDiagnostics())
            .AppendLine();
        sb.Append("  simulation_coordinator ")
            .Append(SimulationCoordinatorThread.Instance.GetDiagnostics())
            .AppendLine();
        sb.Append("  actor_presentation_overlap ")
            .Append(CooperativeSimulationRunner.Instance
                .GetPresentationOverlapDiagnostics())
            .AppendLine();
        sb.Append("  building_presentation_overlap ")
            .Append(CooperativeSimulationRunner.Instance
                .GetBuildingPresentationOverlapDiagnostics())
            .AppendLine();
        sb.Append("  presentation_commands ")
            .Append(PresentationCommandQueue.GetDiagnostics())
            .AppendLine();
        sb.Append("  actor_snapshots ").Append(ActorPresentationSnapshots.GetDiagnostics()).AppendLine();
        sb.Append("  nearby_status_targets ")
            .Append(NearbyStatusTargetIndex.GetDiagnostics())
            .AppendLine();
        sb.Append("  geo_region_units ")
            .Append(WorldboxGame.I?.GeoRegions
                ?.GetUnitMembershipDiagnostics() ??
                    "unavailable")
            .AppendLine();
        sb.Append("  free_tile_search ")
            .Append(FreeTileSearchIndex.GetDiagnostics())
            .AppendLine();
        sb.Append("  actor_presentation ").Append(ActorPresentationRenderer.GetDiagnostics()).AppendLine();
        sb.Append("  world_object_presentation ")
            .Append(WorldObjectPresentationRenderer.GetDiagnostics())
            .AppendLine();
        if (_presentationStressEnabled)
        {
            sb.Append("  presentation_stress projectiles_spawned=")
                .Append(_presentationProjectileCount)
                .Append(" throws_spawned=")
                .Append(_presentationThrowCount)
                .Append(" missing_building=")
                .Append(_presentationMissingBuildingCount)
                .Append(" missing_resource_sprite=")
                .Append(_presentationMissingResourceSpriteCount)
                .AppendLine();
        }

        if (_captureDetails)
        {
            int detailLimit =
                SimulationTickBenchmark.ShouldCollectAiDetails
                    ? 30
                    : 10;
            SimulationTickBenchmark.AppendReport(
                sb,
                12,
                detailLimit);
        }

        ModClass.LogInfo(sb.ToString());
    }

    private static void AppendBenchSummary(
        StringBuilder sb,
        string groupId,
        string totalEntry,
        string totalGroup,
        int limit,
        string[] preferredEntries = null)
    {
        double total = Bench.getBenchResultAsDouble(totalEntry, totalGroup, true);
        sb.Append("  bench[").Append(groupId).Append("] total=")
            .Append(FormatMs(total)).Append(" top=");

        var group = Bench.getGroup(groupId);
        var rows = new List<BenchRow>();
        if (preferredEntries != null)
        {
            for (int i = 0; i < preferredEntries.Length; i++)
            {
                if (!group.dict_data.TryGetValue(preferredEntries[i], out var data))
                {
                    continue;
                }

                AddBenchRow(rows, data);
            }
        }

        foreach (var data in group.dict_data.Values)
        {
            if (preferredEntries != null && preferredEntries.Contains(data.id))
            {
                continue;
            }

            AddBenchRow(rows, data);
        }

        rows.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        int count = Math.Min(limit, rows.Count);
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var row = rows[i];
            sb.Append(row.Id).Append('=').Append(FormatMs(row.Ms));
            if (row.Counter > 0)
            {
                sb.Append('/').Append(row.Counter);
            }
        }

        sb.AppendLine();
    }

    private static void AddBenchRow(List<BenchRow> rows, ToolBenchmarkData data)
    {
        if (data == null)
        {
            return;
        }

        double ms = data.getAverage();
        if (double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0.000001)
        {
            return;
        }

        rows.Add(new BenchRow(data.id, ms, data.getAverageCount()));
    }

    private int SpawnInitialHumans(int amount)
    {
        var zones = World.world.zone_calculator?.zones;
        if (zones == null || zones.Count == 0)
        {
            return 0;
        }

        var candidates = new List<TileZone>();
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            if (zone?.centerTile == null)
            {
                continue;
            }

            if (zone.isGoodForNewCity() && FindSpawnTile(zone) != null)
            {
                candidates.Add(zone);
            }
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone?.centerTile != null && FindSpawnTile(zone) != null)
                {
                    candidates.Add(zone);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        candidates.Shuffle();
        int spawned = 0;
        for (int i = 0; i < amount; i++)
        {
            var zone = candidates[i % candidates.Count];
            var tile = FindSpawnTile(zone);
            if (tile == null)
            {
                continue;
            }

            var actor = World.world.units.spawnNewUnit("human", tile, pSpawnSound: false, pMiracleSpawn: true,
                pSpawnHeight: 0f, pSubspecies: null, pGiveOwnerlessItems: false, pAdultAge: true);
            if (actor != null)
            {
                _cameraAnchor ??= actor;
                spawned++;
            }
        }

        return spawned;
    }

    private void PreparePresentationScene()
    {
        if (_presentationScenePrepared ||
            _cameraAnchor?.data == null ||
            !_cameraAnchor.exists)
        {
            return;
        }

        World.world.move_camera.focusOn(_cameraAnchor.current_position);
        World.world.move_camera.forceZoom(20f);
        World.world.zone_camera.fullClear();
        SelectedUnit.clear();
        SelectedUnit.select(_cameraAnchor);
        if (_presentationStressEnabled)
        {
            _cameraAnchor.addStatusEffect("shield", 30f);
            _presentationFireTile =
                FindPresentationFireTile(_cameraAnchor.current_tile);
            _presentationFireTile?.setFireData(true);
            FindPresentationResource();
            FindPresentationTargets();
        }

        _presentationScenePrepared = true;
        ModClass.LogInfo(
            $"{Prefix} 表现测试场景已就绪 actor={_cameraAnchor.data.id} position={_cameraAnchor.current_position} stress={_presentationStressEnabled}");
    }

    private void UpdatePresentationStress(float delta)
    {
        if (!_presentationStressEnabled ||
            _cameraAnchor?.data == null ||
            !_cameraAnchor.exists ||
            !_cameraAnchor.isAlive() ||
            ActorPresentationSnapshots.Current == null)
        {
            return;
        }

        _presentationStressElapsed += delta;
        if (_presentationStressElapsed < 0.2f)
        {
            return;
        }

        _presentationStressElapsed = 0f;
        _cameraAnchor.addStatusEffect("shield", 30f);
        if (_presentationFireTile != null &&
            !_presentationFireTile.isOnFire())
        {
            _presentationFireTile.setFireData(true);
        }

        if (_presentationTarget?.data == null ||
            !_presentationTarget.exists ||
            !_presentationTarget.isAlive())
        {
            FindPresentationTargets();
        }

        if (_presentationTarget != null)
        {
            Vector3 start = _cameraAnchor.current_position;
            Vector3 end = _presentationTarget.current_position;
            start.y += _cameraAnchor.getHeight();
            end.y += _presentationTarget.getHeight();
            if (World.world.projectiles.spawn(
                    _cameraAnchor,
                    _presentationTarget,
                    "arrow",
                    start,
                    end,
                    _presentationTarget.getHeight(),
                    _cameraAnchor.getHeight()) != null)
            {
                _presentationProjectileCount++;
            }
        }

        if (_presentationBuilding?.data == null ||
            !_presentationBuilding.exists ||
            !_presentationBuilding.isAlive())
        {
            FindPresentationTargets();
        }

        if (_presentationBuilding == null)
        {
            _presentationMissingBuildingCount++;
            return;
        }

        if (_presentationThrowResource?.getGameplaySprite() == null)
        {
            FindPresentationResource();
        }

        if (_presentationThrowResource?.getGameplaySprite() == null)
        {
            _presentationMissingResourceSpriteCount++;
            return;
        }

        _presentationBuilding.addStatusEffect("shield", 30f);
        World.world.resource_throw_manager.addNew(
            _cameraAnchor.current_position,
            _presentationBuilding.current_position,
            4f,
            _presentationThrowResource.id,
            1,
            2f,
            _presentationBuilding);
        _presentationThrowCount++;
    }

    private void FindPresentationTargets()
    {
        _presentationTarget = null;
        List<Actor> actors = World.world.units.getSimpleList();
        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            if (ReferenceEquals(actor, _cameraAnchor) ||
                actor?.data == null ||
                !actor.exists ||
                !actor.isAlive())
            {
                continue;
            }

            _presentationTarget = actor;
            break;
        }

        _presentationBuilding = null;
        BuildingManager buildingManager = World.world.buildings;
        buildingManager.checkContainer();
        List<Building> buildings = buildingManager.getSimpleList();
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building?.data == null ||
                !building.exists ||
                !building.isAlive())
            {
                continue;
            }

            _presentationBuilding = building;
            break;
        }

        if (_presentationBuilding != null)
        {
            return;
        }

        ActorPresentationSnapshot snapshot =
            ActorPresentationSnapshots.Current;
        if (snapshot == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.BuildingCount; i++)
        {
            Building building = buildingManager.get(
                snapshot.GetBuildingAt(i).BuildingId);
            if (building?.data == null ||
                !building.exists ||
                !building.isAlive())
            {
                continue;
            }

            _presentationBuilding = building;
            break;
        }
    }

    private void FindPresentationResource()
    {
        _presentationThrowResource = null;
        foreach (ResourceAsset resource in AssetManager.resources.list)
        {
            if (resource?.gameplay_sprites == null ||
                resource.gameplay_sprites.Length == 0 ||
                resource.gameplay_sprites[0] == null)
            {
                continue;
            }

            _presentationThrowResource = resource;
            return;
        }
    }

    private static WorldTile FindPresentationFireTile(WorldTile anchor)
    {
        WorldTile[] tiles = anchor?.zone?.tiles;
        if (tiles == null)
        {
            return anchor;
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            WorldTile tile = tiles[i];
            if (tile?.Type?.ocean == false)
            {
                return tile;
            }
        }

        return anchor;
    }

    private static WorldTile FindSpawnTile(TileZone zone)
    {
        if (zone == null)
        {
            return null;
        }

        if (IsHumanSpawnTile(zone.centerTile))
        {
            return zone.centerTile;
        }

        for (int i = 0; i < zone.tiles.Length; i++)
        {
            var tile = zone.tiles[i];
            if (IsHumanSpawnTile(tile))
            {
                return tile;
            }
        }

        return null;
    }

    private static bool IsHumanSpawnTile(WorldTile tile)
    {
        if (tile?.Type == null)
        {
            return false;
        }

        var type = tile.Type;
        return !type.liquid && !type.lava && !type.block;
    }

    private void RecordFrame(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        float ms = deltaSeconds * 1000f;
        if (ms > 10000f)
        {
            return;
        }

        _frameTimesMs.Add(ms);
        _frameTimeSumMs += ms;
        if (ms > _frameTimeMaxMs)
        {
            _frameTimeMaxMs = ms;
        }

        if (ms > 33.333f)
        {
            _framesOver33Ms++;
        }

        if (ms > 50f)
        {
            _framesOver50Ms++;
        }

        if (ms > 100f)
        {
            _framesOver100Ms++;
        }
    }

    private FrameStats SnapshotFrameStats()
    {
        int count = _frameTimesMs.Count;
        if (count == 0)
        {
            return default;
        }

        var sorted = _frameTimesMs.ToArray();
        Array.Sort(sorted);
        float p95 = sorted[Math.Min(sorted.Length - 1, Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.95) - 1))];
        float p99 = sorted[Math.Min(sorted.Length - 1, Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.99) - 1))];
        float avg = (float)(_frameTimeSumMs / count);
        float minFps = _frameTimeMaxMs > 0f ? 1000f / _frameTimeMaxMs : 0f;
        return new FrameStats(avg, _frameTimeMaxMs, p95, p99, minFps, _framesOver33Ms, _framesOver50Ms,
            _framesOver100Ms);
    }

    private void ResetFrameStats()
    {
        _frameTimesMs.Clear();
        _frameTimeSumMs = 0.0;
        _frameTimeMaxMs = 0f;
        _framesOver33Ms = 0;
        _framesOver50Ms = 0;
        _framesOver100Ms = 0;
    }

    private void ScanInvalidHandRenderers(float delta)
    {
        _handRendererScanElapsed += delta;
        if (_handRendererScanElapsed < 1f)
        {
            return;
        }

        _handRendererScanElapsed = 0f;
        var units = World.world?.units?.getSimpleList();
        if (units == null)
        {
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            var actor = units[i];
            if (actor?.asset == null || !actor.checkHasRenderedItem())
            {
                continue;
            }

            TryReportInvalidHandRenderer(actor);
        }
    }

    private void TryReportInvalidHandRenderer(Actor actor)
    {
        IHandRenderer renderer;
        try
        {
            renderer = actor.getHandRendererAsset();
        }
        catch (Exception e)
        {
            ReportInvalidHandRenderer(actor, "exception", $"getHandRendererAsset exception={e.GetType().Name}:{e.Message}");
            return;
        }

        if (renderer == null)
        {
            ReportInvalidHandRenderer(actor, "null", "renderer=null");
            return;
        }

        Sprite[] sprites;
        try
        {
            sprites = renderer.getSprites();
        }
        catch (Exception e)
        {
            ReportInvalidHandRenderer(actor, DescribeHandRenderer(renderer),
                $"getSprites exception={e.GetType().Name}:{e.Message}");
            return;
        }

        if (sprites == null)
        {
            ReportInvalidHandRenderer(actor, DescribeHandRenderer(renderer), "sprites=null");
            return;
        }

        if (sprites.Length == 0)
        {
            ReportInvalidHandRenderer(actor, DescribeHandRenderer(renderer), "sprites=empty");
        }
    }

    private void ReportInvalidHandRenderer(Actor actor, string rendererKey, string reason)
    {
        if (_reportedInvalidHandRenderers.Count >= 64)
        {
            return;
        }

        var actorId = actor.data?.id ?? 0L;
        var key = actorId.ToString(CultureInfo.InvariantCulture) + "|" + rendererKey + "|" + reason;
        if (!_reportedInvalidHandRenderers.Add(key))
        {
            return;
        }

        ModClass.LogErrorConcurrent(
            $"{Prefix} invalid hand renderer reason={reason} actor={DescribeActor(actor)} source={DescribeHandRendererSource(actor)} renderer={rendererKey}");
    }

    private static string DescribeActor(Actor actor)
    {
        if (actor == null)
        {
            return "null";
        }

        var actorId = actor.data?.id ?? 0L;
        var assetId = actor.asset?.id ?? "null";
        var taskId = actor.hasTask() ? actor.ai?.task?.id ?? "null" : "none";
        var actionName = actor.ai?.action?.GetType().Name ?? "none";
        var kingdomId = actor.kingdom == null
            ? "null"
            : actor.kingdom.id.ToString(CultureInfo.InvariantCulture);
        var tile = actor.current_tile;
        var tileText = tile == null
            ? "null"
            : tile.x.ToString(CultureInfo.InvariantCulture) + "," + tile.y.ToString(CultureInfo.InvariantCulture);
        return $"{assetId}#{actorId} kingdom={kingdomId} task={taskId} action={actionName} tile={tileText}";
    }

    private static string DescribeHandRendererSource(Actor actor)
    {
        if (actor == null)
        {
            return "actor=null";
        }

        if (!actor.asset.use_tool_items)
        {
            return DescribeWeapon(actor);
        }

        if (actor.has_attack_target && actor.hasWeapon())
        {
            return DescribeWeapon(actor);
        }

        if (actor.isCarryingResources())
        {
            return "resource:" + actor.inventory.getItemIDToRender();
        }

        if (actor.hasTask())
        {
            var task = actor.ai.task;
            var tool = task?.cached_hand_tool_asset;
            if (tool != null)
            {
                return $"task_tool task={task.id} force={task.force_hand_tool} tool={tool.id}";
            }
        }

        return actor.hasWeapon() ? DescribeWeapon(actor) : "unknown";
    }

    private static string DescribeWeapon(Actor actor)
    {
        if (actor == null || !actor.hasWeapon())
        {
            return "weapon=none";
        }

        var weapon = actor.getWeapon();
        var asset = weapon?.getAsset();
        if (asset == null)
        {
            return "weapon_asset=null";
        }

        var itemId = weapon.data?.id ?? 0L;
        return
            $"weapon id={asset.id} item={itemId} pool={asset.is_pool_weapon} type={asset.equipment_type} path={asset.path_gameplay_sprite}";
    }

    private static string DescribeHandRenderer(IHandRenderer renderer)
    {
        switch (renderer)
        {
            case EquipmentAsset equipment:
                return
                    $"equipment:{equipment.id}:pool={equipment.is_pool_weapon}:type={equipment.equipment_type}:path={equipment.path_gameplay_sprite}";
            case UnitHandToolAsset tool:
                return $"tool:{tool.id}:path={tool.path_gameplay_sprite}";
            case ResourceAsset resource:
                return $"resource:{resource.id}:path={resource.full_sprite_path}";
            default:
                return renderer.GetType().FullName ?? renderer.GetType().Name;
        }
    }

    private void SetState(RunnerState state)
    {
        _state = state;
        _stateElapsed = 0f;
    }

    private static int CountUnits()
    {
        return World.world?.units?.Count ?? 0;
    }

    private static int CountCities()
    {
        return World.world?.cities?.Count ?? 0;
    }

    private static int CountKingdoms()
    {
        return World.world?.kingdoms?.Count ?? 0;
    }

    private static bool IsCultiwayWorldReady()
    {
        TileExtendManager tileManager = ModClass.I?.TileExtendManager;
        GeoRegionManager geoRegions = WorldboxGame.I?.GeoRegions;
        return tileManager != null &&
               tileManager.Ready() &&
               !tileManager.IsWorldInitializationPending &&
               geoRegions?.IsMembershipReady == true;
    }

    private static string FormatMs(double seconds)
    {
        return (seconds * 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + "ms";
    }

    private static string GetEnvString(string key, string defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static int GetEnvInt(string key, int defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static float GetEnvFloat(string key, float defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(key);
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : defaultValue;
    }

    private static bool GetEnvBool(string key, bool defaultValue)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        value = value.Trim();
        return value == "1" ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool? GetEnvNullableBool(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return GetEnvBool(key, false);
    }

    private readonly struct BenchRow
    {
        public BenchRow(string id, double ms, long counter)
        {
            Id = id;
            Ms = ms;
            Counter = counter;
        }

        public string Id { get; }
        public double Ms { get; }
        public long Counter { get; }
    }

    private readonly struct FrameStats
    {
        public FrameStats(float avgMs, float maxMs, float p95Ms, float p99Ms, float minFps, int over33, int over50,
            int over100)
        {
            AvgMs = avgMs;
            MaxMs = maxMs;
            P95Ms = p95Ms;
            P99Ms = p99Ms;
            MinFps = minFps;
            Over33 = over33;
            Over50 = over50;
            Over100 = over100;
        }

        public float AvgMs { get; }
        public float MaxMs { get; }
        public float P95Ms { get; }
        public float P99Ms { get; }
        public float MinFps { get; }
        public int Over33 { get; }
        public int Over50 { get; }
        public int Over100 { get; }
    }
}
