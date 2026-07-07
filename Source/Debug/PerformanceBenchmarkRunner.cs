using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

    private static readonly string[] DefaultActorEntries =
    {
        "update_jobs_parallel",
        "b6_update_ai",
        "b5_checkPathMovement",
        "u10_checkSmoothMovement",
        "b3_findEnemyTarget",
        "b6_0_update_decision",
        "u5_curTileAction",
        "u1_checkInside",
        "b2_checkCurrentEnemyTarget",
        "b1_checkUnderForce",
        "b4_checkTaskVerifier",
        "update_stats",
        "update_timers",
        "update_visibility",
        "prepare",
        "clear_parallel_results",
        "apply_parallel_results"
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
    private int _initialHumans;
    private int _startMeasureUnits;
    private float _durationSeconds;
    private float _warmupMaxSeconds;
    private float _logIntervalSeconds;
    private bool _createWorld;
    private bool _enableAiBench;
    private bool _quitOnComplete;
    private bool _configured;
    private bool _initialUnitsSpawned;
    private float _stateElapsed;
    private float _runElapsed;
    private float _logElapsed;
    private double _frameTimeSumMs;
    private float _frameTimeMaxMs;
    private int _framesOver33Ms;
    private int _framesOver50Ms;
    private int _framesOver100Ms;
    private readonly HashSet<string> _reportedInvalidHandRenderers = new();
    private float _handRendererScanElapsed;

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
        _mode = mode.Trim();
        _mapSize = GetEnvString("CULTIWAY_PERF_MAP_SIZE", MapSizeLibrary.iceberg);
        _mapTemplate = GetEnvString("CULTIWAY_PERF_MAP_TEMPLATE", Config.current_map_template);
        _speedId = GetEnvString("CULTIWAY_PERF_SPEED", "x20");
        _initialHumans = GetEnvInt("CULTIWAY_PERF_INITIAL_HUMANS", 200);
        _startMeasureUnits = GetEnvInt("CULTIWAY_PERF_START_MEASURE_UNITS", 3000);
        _durationSeconds = GetEnvFloat("CULTIWAY_PERF_DURATION", 180f);
        _warmupMaxSeconds = GetEnvFloat("CULTIWAY_PERF_WARMUP_MAX", 900f);
        _logIntervalSeconds = Math.Max(5f, GetEnvFloat("CULTIWAY_PERF_LOG_INTERVAL", 30f));
        _createWorld = GetEnvBool("CULTIWAY_PERF_CREATE_WORLD", true);
        _enableAiBench = GetEnvBool("CULTIWAY_PERF_AI_BENCH", false);
        _quitOnComplete = GetEnvBool("CULTIWAY_PERF_QUIT_ON_DONE", false);
        _configured = true;

        ModClass.LogInfo(
            $"{Prefix} 已启用 mode={_mode} mapSize={_mapSize} template={_mapTemplate} speed={_speedId} initialHumans={_initialHumans} startMeasureUnits={_startMeasureUnits} duration={_durationSeconds:0.#}s warmupMax={_warmupMaxSeconds:0.#}s aiBench={_enableAiBench}");
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
            ScanInvalidHandRenderers(delta);
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
        Config.paused = false;

        if (_createWorld)
        {
            Config.customMapSize = _mapSize;
            Config.current_map_template = _mapTemplate;
            ModClass.LogInfo($"{Prefix} 生成新世界 mapSize={_mapSize} template={_mapTemplate}");
            World.world.generateNewMap();
            SetState(RunnerState.WaitingForWorldLoaded);
            return;
        }

        SetState(RunnerState.SpawningInitialUnits);
    }

    private void UpdateWaitingForWorldLoaded()
    {
        if (SmoothLoader.isLoading() || _stateElapsed < SetupDelaySeconds)
        {
            return;
        }

        SetState(RunnerState.SpawningInitialUnits);
    }

    private void UpdateSpawningInitialUnits()
    {
        if (SmoothLoader.isLoading() || _stateElapsed < SetupDelaySeconds)
        {
            return;
        }

        Config.paused = false;
        Config.setWorldSpeed(_speedId);

        if (!_initialUnitsSpawned && _initialHumans > 0)
        {
            int spawned = SpawnInitialHumans(_initialHumans);
            _initialUnitsSpawned = true;
            ModClass.LogInfo($"{Prefix} 初始人类投放 requested={_initialHumans} spawned={spawned} units={CountUnits()} cities={CountCities()}");
        }

        if (_startMeasureUnits > 0 && CountUnits() < _startMeasureUnits)
        {
            ResetFrameStats();
            SetState(RunnerState.WarmingUp);
            return;
        }

        StartMeasurement();
    }

    private void UpdateWarmingUp(float delta)
    {
        Config.paused = false;
        Config.setWorldSpeed(_speedId);
        _runElapsed += delta;
        _logElapsed += delta;

        if (CountUnits() >= _startMeasureUnits)
        {
            ModClass.LogInfo($"{Prefix} 人口达到统计阈值 units={CountUnits()} elapsed={_runElapsed:0.0}s");
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
        Bench.bench_enabled = true;
        DebugConfig.setOption(DebugOption.BenchAiEnabled, _enableAiBench);
        Bench.bench_ai_enabled = _enableAiBench;
        ResetFrameStats();
        _runElapsed = 0f;
        _logElapsed = 0f;
        SetState(RunnerState.Measuring);
        ModClass.LogInfo($"{Prefix} 开始统计 units={CountUnits()} cities={CountCities()} speed={_speedId}");
    }

    private void UpdateMeasuring(float delta)
    {
        Config.paused = false;
        Config.setWorldSpeed(_speedId);
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

    private void LogMeasurement(string phase)
    {
        var frameStats = SnapshotFrameStats();
        var sb = new StringBuilder(2048);
        sb.Append(Prefix)
            .Append(' ')
            .Append(phase)
            .Append(" elapsed=").Append(_runElapsed.ToString("0.0", CultureInfo.InvariantCulture)).Append('s')
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

        AppendBenchSummary(sb, "main", "game_total", "main", 8);
        AppendBenchSummary(sb, "game_total", "game_total", "main", 12, DefaultGameTotalEntries);
        AppendBenchSummary(sb, "actors", "actors", "game_total", 16, DefaultActorEntries);
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
                spawned++;
            }
        }

        return spawned;
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
