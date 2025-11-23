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
using Cultiway.Core.EventSystem.Systems;
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
            PrefabLibrary.gameObject.SetActive(false);
            Game = new WorldboxGame();
            Try.Start(() =>
            {
                W = new EntityStore()
                {
                    JobRunner = new ParallelJobRunner(Environment.ProcessorCount)
                };
            });

            WorldRecord = new(W);

            LoadLocales();

            ActorExtendManager = new ActorExtendManager(W);
            BookExtendManager = new BookExtendManager(W);
            CityExtendManager = new CityExtendManager(W);
            TileExtendManager = new();

            Try.Start(() =>
            {
                L = new Core.Libraries.Manager();
                L.Init();
            });

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
            GeneralLogicSystems.Add(new StatusParticleSystem());
            
            GeneralLogicSystems.Add(LogicPrepareRecycleSystemGroup);
            LogicPrepareRecycleSystemGroup.Add(new DisposeActorExtendSystem());
            LogicPrepareRecycleSystemGroup.Add(new DisposeCityExtendSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleAnimRendererSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleStatusEffectSystem());
            LogicPrepareRecycleSystemGroup.Add(new RecycleUnknownAssetsSystem());
            GeneralLogicSystems.Add(new RecycleDefaultEntitySystem());
            
            GeneralLogicSystems.Add(LogicRestoreStatusSystemGroup);
            LogicRestoreStatusSystemGroup.Add(new RestoreHealthSystem());
            LogicRestoreStatusSystemGroup.Add(new RestoreQiyunSystem());
            
            GeneralLogicSystems.Add(LogicEventProcessSystemGroup);
            LogicEventProcessSystemGroup.Add(new ActorNameGeneratedEventSystem());
            LogicEventProcessSystemGroup.Add(new EntityNameGeneratedEventSystem());
            
            GeneralLogicSystems.Add(new SyncCityRelationSystem());
            
            GeneralRenderSystems.Add(new RenderAnimFrameSystem(W));

            CustomMapModeManager = new();
            CustomMapModeManager.Initialize();

            SkillV3 = new Core.SkillLibV3.Manager(Game);
            Geo = new Core.GeoLib.Manager(Game);
            _ui = new UI.Manager();
            _patch = new Patch.Manager();
            _content = new Manager();

            _ui.Init();
            _patch.Init();
            SkillV3.Init();
            _content.Init();
            GeneralLogicSystems.Add(new RemoveDirtyTagSystem());

            if (Environment.UserName == "Inmny")
            {
                Config.isEditor = true;
                DebugConfig.setOption(DebugOption.FastCultures, true);
                DebugConfig.setOption(DebugOption.CityInfiniteResources, true);
                DebugConfig.setOption(DebugOption.CityFastConstruction, true);
                DebugConfig.setOption(DebugOption.CityFastUpgrades, true);
                
                GeneralLogicSystems.SetMonitorPerf(true);
                GeneralRenderSystems.SetMonitorPerf(true);
                Geo.SetMonitorPerf(true);
            }
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
