using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cultiway.AbstractGame.AbstractEngine;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Localization;
using Cultiway.Core.Logging;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Debug;
using Cultiway.LocaleKeys;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using HarmonyLib;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using NeoModLoader.services;
using NeoModLoader.utils;
using UnityEngine;
using SystemUtils = Cultiway.Utils.SystemUtils;

namespace Cultiway
{
    public class ModClass : BasicMod<ModClass>, IReloadable
    {
        private Manager       _content;
        private Patch.Manager _patch;

        private       UI.Manager              _ui;
        public static Assembly                A                    { get; private set; }
        public static Core.Libraries.Manager  L                    { get; private set; }
        public        ActorExtendManager      ActorExtendManager   { get; private set; }
        public BookExtendManager BookExtendManager { get; private set; }
        public CityExtendManager CityExtendManager { get; private set; }
        public        TileExtendManager       TileExtendManager    { get; private set; }
        public        CustomMapModeManager    CustomMapModeManager { get; private set; }
        public WorldboxGame Game { get; private set; }
        public        AEngine                 Engine               { get; }
        public SystemRoot GeneralLogicSystems  { get; private set; }
        public SystemRoot GeneralRenderSystems { get; private set; }
        public SystemGroup LogicPrepareRecycleSystemGroup { get; private set; }
        public SystemGroup LogicRestoreStatusSystemGroup { get; private set; }
        public SystemGroup LogicEventProcessSystemGroup { get; private set; }
        public SystemRoot TileLogicSystems   { get; private set; }
        public SystemRoot TileRenderSystems  { get; private set; }
        public        EntityStore             W                    { get; private set; }
        public        CommandBuffer             CommandBuffer        { get; private set; }
        public        WorldRecord             WorldRecord          { get; private set; }
        public Core.SkillLibV3.Manager SkillV3 { get; private set; }
        public        Core.GeoLib.Manager     Geo                  { get; private set; }

        private void Start()
        {
            L.PostInit();
        }

        private float accum_time = 0;
        private float time_for_log_perf = 0;
        private float time_for_save_loggers = 0;

        private void Update()
        {
            if (!Game.IsLoaded()) return; 
            //DebugConfig.setOption(DebugOption.ParallelJobsUpdater, false);
            var render_update_tick = new UpdateTick(Game.GetRenderDeltaTime(), Game.GetGameTime());
            try
            {
                time_for_save_loggers += Time.deltaTime;
                if (time_for_save_loggers > 60)
                {
                    time_for_save_loggers = 0;
                    PersistentLogger.Save();
                }
                if (!Game.IsPaused())
                {
                    time_for_log_perf += Time.deltaTime;
                    if (time_for_log_perf > 600)
                    {
                        time_for_log_perf = 0;
                        LogPerf();
                    }

                    if (TimeScales.precise_simulate)
                        accum_time += Mathf.Sqrt(Config.time_scale_asset.multiplier);
                    var logic_update_tick = new UpdateTick(Game.GetLogicDeltaTime(), Game.GetGameTime());
                    if (TimeScales.precise_simulate)
                    {
                        while (accum_time > 1)
                        {
                            accum_time -= 1;
                            GeneralLogicSystems.Update(logic_update_tick);
                            TileLogicSystems.Update(logic_update_tick);
                            Geo.UpdateLogic(logic_update_tick);
                        }
                    }
                    else
                    {
                        GeneralLogicSystems.Update(render_update_tick);
                        TileLogicSystems.Update(render_update_tick);
                        Geo.UpdateLogic(render_update_tick);
                    }
                }

                GeneralRenderSystems.Update(render_update_tick);
                TileRenderSystems.Update(render_update_tick);
            }
            catch (Exception e)
            {
                LogError(SystemUtils.GetFullExceptionMessage(e));
                Game.Pause();
            }
        }

        public void LogPerf(bool force = false)
        {
            if (Environment.UserName != "Inmny" && !force) return;
            StringBuilder sb = new();
            sb.Append('\n');
            GeneralLogicSystems.AppendPerfLog(sb);
            sb.Append('\n');
            GeneralRenderSystems.AppendPerfLog(sb);
            sb.Append('\n');
            Geo.AppendPerfLog(sb);
            LogInfo($"{sb}");
        }

        public void SyncCultiLogFromPlayerConfig(bool resetDiskToday = false)
        {
            CultiLog.SetEnabled(IsCultiLogPlayerOptionEnabled(CultiLogPlayerOptions.Enabled));

            CultiLogCategory mask = CultiLogCategory.None;
            CultiLogCategory[] categories = CultiLogPlayerOptions.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                CultiLogCategory category = categories[i];
                if (IsCultiLogPlayerOptionEnabled(CultiLogPlayerOptions.GetCategoryOptionId(category)))
                {
                    mask |= category;
                }
            }

            CultiLog.SetCategoryMask(mask);

            bool diskEnabled = IsCultiLogPlayerOptionEnabled(CultiLogPlayerOptions.DiskEnabled);
            CultiLog.SetDiskEnabled(diskEnabled, diskEnabled && resetDiskToday);
        }

        public void OnCultiLogEnabledToggled()
        {
            SyncCultiLogFromPlayerConfig();
            ShowCultiLogTip($"玩法日志{(CultiLog.Enabled ? "已启用" : "已禁用")}");
        }

        public void OnCultiLogDiskToggled()
        {
            SyncCultiLogFromPlayerConfig(true);
            if (CultiLog.DiskEnabled)
            {
                string path = CultiLog.GetDiskFilePath();
                CultiLog.General.Info($"日志落盘已重新开始: {path}");
                ShowCultiLogTip($"日志落盘已重新开始: {path}", 5f);
            }
            else
            {
                ShowCultiLogTip("日志落盘已禁用");
            }
        }

        public void CycleCultiLogMinLevel()
        {
            CultiLogLevel level = CultiLog.MinLevel switch
            {
                CultiLogLevel.Trace => CultiLogLevel.Debug,
                CultiLogLevel.Debug => CultiLogLevel.Info,
                CultiLogLevel.Info => CultiLogLevel.Warning,
                CultiLogLevel.Warning => CultiLogLevel.Error,
                _ => CultiLogLevel.Trace
            };
            CultiLog.SetMinLevel(level);
            ShowCultiLogTip($"日志最低等级: {level}");
        }

        public void OnCultiLogCategoryToggled(CultiLogCategory category)
        {
            SyncCultiLogFromPlayerConfig();
            ShowCultiLogTip($"{GetCultiLogCategoryLabel(category)}日志{(CultiLog.IsCategoryEnabled(category) ? "已启用" : "已禁用")}");
        }

        public void ExportCultiLog()
        {
            string path = CultiLog.ExportRecentToJsonl();
            ShowCultiLogTip($"已导出最近日志: {path}", 5f);
        }

        public void ClearCultiLog()
        {
            CultiLog.ClearRecent();
            ShowCultiLogTip("已清空最近日志缓存");
        }

        public void LogCultiLogStats()
        {
            CultiLogStats stats = CultiLog.GetStats();
            ShowCultiLogTip(
                $"日志: {(stats.Enabled ? "开" : "关")} 落盘:{(stats.DiskEnabled ? "开" : "关")} 等级:{stats.MinLevel} 最近:{stats.RecentCount}/{stats.RecentCapacity} 队列:{stats.QueuedCount} 丢弃:{stats.DroppedCount}",
                5f);
        }

        private static void ShowCultiLogTip(string message, float time = 3f)
        {
            WorldTip.showNow(message, false, "top", time);
        }

        private static bool IsCultiLogPlayerOptionEnabled(string optionId)
        {
            return PlayerConfig.dict != null &&
                   PlayerConfig.dict.TryGetValue(optionId, out PlayerOptionData data) &&
                   data.boolVal;
        }

        private static string GetCultiLogCategoryLabel(CultiLogCategory category)
        {
            return category switch
            {
                CultiLogCategory.General => "通用",
                CultiLogCategory.Combat => "战斗",
                CultiLogCategory.Sect => "宗门",
                CultiLogCategory.Cultivation => "修炼",
                CultiLogCategory.Book => "书籍",
                CultiLogCategory.Skill => "技能",
                CultiLogCategory.Pathfinding => "寻路",
                CultiLogCategory.Item => "物品",
                CultiLogCategory.Train => "列车",
                CultiLogCategory.Geo => "地理",
                CultiLogCategory.AI => "AI",
                CultiLogCategory.UI => "UI",
                CultiLogCategory.Perf => "性能",
                CultiLogCategory.AIGC => "AIGC",
                CultiLogCategory.Error => "错误",
                _ => category.ToString()
            };
        }

        public static void LogInfoConcurrent(string message)
        {
            LogService.LogInfoConcurrent("[" + Instance.GetDeclaration().Name + "]: " + message);
        }

        public static void LogWarningConcurrent(string message)
        {
            LogService.LogWarningConcurrent("[" + Instance.GetDeclaration().Name + "]: " + message);
        }

        public static void LogErrorConcurrent(string message)
        {
            LogService.LogErrorConcurrent("[" + Instance.GetDeclaration().Name + "]: " + message);
        }

        [Hotfixable]
        public void Reload()
        {
            LoadLocales();
            typeof(ResourcesPatch).GetMethod("LoadResourceFromFolder", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null,
                    new object[] { Path.Combine(GetDeclaration().FolderPath, "GameResources") });
            _content.OnReload();

            ActorExtendManager.AllStatsDirty();

            foreach (var actor in World.world.units)
            {
                if (actor.kingdom == null)
                {
                    LogError($"Actor {actor.data.id} has null kingdom");
                }
                else if (actor.kingdom.asset == null)
                {
                    LogError($"Actor {actor.data.id} has null kingdom({actor.kingdom.id}) asset");
                }
            }
            
            foreach (var city in World.world.cities.list)
            {
                if (city.units.Any(x => x == null))
                {
                    LogError($"City {city.name} has null units");
                }
            }
            LogPerf();
        }

        public static GameObject NewPrefabPreview(string name, params Type[] types)
        {
            var obj = new GameObject(name, types);
            obj.transform.SetParent(Instance.PrefabLibrary);
            return obj;
        }

        protected override void OnModLoad()
        {
            Harmony.CreateAndPatchAll(typeof(FinalizerPatch));
            Try.Start(() => { _ = LK.Root; });
            A = Assembly.GetExecutingAssembly();
            CultiLog.Initialize(GetDeclaration().FolderPath);
            PrefabLibrary.gameObject.SetActive(false);

            _ui = new UI.Manager();
            _ui.Init();
            SyncCultiLogFromPlayerConfig(true);

            Game = new WorldboxGame();
            Try.Start(() =>
            {
                L = new Core.Libraries.Manager();
                L.Init();
            });
            Game.Init();
            Try.Start(() =>
            {
                W = new EntityStore()
                {
                    JobRunner = new ParallelJobRunner(Environment.ProcessorCount)
                };
                CommandBuffer = W.GetCommandBuffer();
                CommandBuffer.ReuseBuffer = true;
            });

            WorldRecord = new(W);

            LoadLocales();
            ModifiableLocalizationManager.Initialize(GetDeclaration().FolderPath);

            ActorExtendManager = new ActorExtendManager(W);
            BookExtendManager = new BookExtendManager(W);
            CityExtendManager = new CityExtendManager(W);
            TileExtendManager = new();

            GeneralLogicSystems = new SystemRoot(nameof(GeneralLogicSystems));
            GeneralRenderSystems = new SystemRoot(nameof(GeneralRenderSystems));
            LogicPrepareRecycleSystemGroup = new SystemGroup(nameof(LogicPrepareRecycleSystemGroup));
            LogicRestoreStatusSystemGroup = new SystemGroup(nameof(LogicRestoreStatusSystemGroup));
            LogicEventProcessSystemGroup = new SystemGroup(nameof(LogicEventProcessSystemGroup));
            
            TileLogicSystems = new SystemRoot(nameof(TileLogicSystems));
            TileRenderSystems = new SystemRoot(nameof(TileRenderSystems));

            GeneralLogicSystems.AddStore(W);
            GeneralRenderSystems.AddStore(W);
            TileLogicSystems.AddStore(TileExtendManager.World);
            TileRenderSystems.AddStore(TileExtendManager.World);
            
            GeneralLogicSystems.Add(new AnimFrameUpdateSystem(W));
            
            GeneralLogicSystems.Add(new AliveTimerSystem());
            GeneralLogicSystems.Add(new AliveTimerCheckSystem());
            GeneralLogicSystems.Add(new DelayActiveCheckSystem());
            GeneralLogicSystems.Add(new StatusTickSystem());
            
            GeneralLogicSystems.Add(LogicRestoreStatusSystemGroup);
            LogicRestoreStatusSystemGroup.Add(new RestoreHealthSystem());
            LogicRestoreStatusSystemGroup.Add(new RestoreQiyunSystem());
            
            GeneralLogicSystems.Add(LogicEventProcessSystemGroup);
            LogicEventProcessSystemGroup.Add(new ActorNameGeneratedEventSystem());
            LogicEventProcessSystemGroup.Add(new EntityNameGeneratedEventSystem());
            LogicEventProcessSystemGroup.Add(new WorldGeneratedPartitionGeoRegionsEventSystem());
            LogicEventProcessSystemGroup.Add(new GeoRegionAutoClassifyAndNameEventSystem());
            LogicEventProcessSystemGroup.Add(new GetHitEventSystem());
            
            GeneralLogicSystems.Add(new WaterConnectivitySystem());
            GeneralLogicSystems.Add(PortalManager.Instance);
            
            GeneralRenderSystems.Add(new RenderAnimFrameSystem(W));
            GeneralRenderSystems.Add(new RenderStatusParticleSystem());
            GeneralRenderSystems.Add(new RenderSkillFlyOverParticleSystem());

            CustomMapModeManager = new();
            CustomMapModeManager.Initialize();

            SkillV3 = new Core.SkillLibV3.Manager(Game);
            Geo = new Core.GeoLib.Manager(Game);
            _patch = new Patch.Manager();
            _content = new Manager();

            _patch.Init();
            SkillV3.Init();
            _content.Init();
            
            GeneralLogicSystems.Add(new StructuralChangeSystem());
            GeneralLogicSystems.Add(LogicPrepareRecycleSystemGroup);
            //LogicPrepareRecycleSystemGroup.Add(new DisposeActorExtendSystem());
            //LogicPrepareRecycleSystemGroup.Add(new DisposeCityExtendSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleAnimRendererSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleStatusEffectSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleUnknownAssetsSystem());
            GeneralLogicSystems.Add(new RecycleDefaultEntitySystem());
            GeneralLogicSystems.Add(new RemoveDirtyTagSystem());

            if (Environment.UserName == "Inmny")
            {
                Config.isEditor = true;
                DebugConfig.setOption(DebugOption.FastCultures, true);
                DebugConfig.setOption(DebugOption.CityInfiniteResources, true);
                DebugConfig.setOption(DebugOption.CityFastConstruction, true);
                DebugConfig.setOption(DebugOption.CityFastUpgrades, true);
                CombatDamageDebug.EnableForFavoriteUnits();
                
                GeneralLogicSystems.SetMonitorPerf(true);
                GeneralRenderSystems.SetMonitorPerf(true);
                Geo.SetMonitorPerf(true);
            }
            PerformanceBenchmarkRunner.Install(gameObject);
            PathFinder.Instance.UseGenerator(new PortalAwarePathGenerator(PortalRegistry.Instance, new PathfindingConfig()));
        }

        private void OnApplicationQuit()
        {
            PersistentLogger.Save();
            CultiLog.Shutdown();
        }

        public override void PostInit()
        {
            base.PostInit();
            _ui.PostInit();
        }

        private void LoadLocales()
        {
            var folder = GetLocaleFilesDirectory(GetDeclaration());

            if (!Directory.Exists(folder))
                return;

            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            foreach (var locale_file in files)
            {
                try
                {
                    if (locale_file.EndsWith(".json"))
                    {
                        LM.LoadLocale(Path.GetFileNameWithoutExtension(locale_file), locale_file);
                    }
                    else if (locale_file.EndsWith(".csv"))
                    {
                        LM.LoadLocales(locale_file);
                    }
                }
                catch (FormatException e)
                {
                    LogWarning(e.Message);
                }
            }

            LM.ApplyLocale(false);

            var dict = LocalizedTextManager.instance._localized_text;
            foreach (var k in dict.Keys.ToList())
            {
                dict[k.Underscore()] = dict[k];
            }
            LocalizedTextManager.updateTexts();
        }
    }
}
