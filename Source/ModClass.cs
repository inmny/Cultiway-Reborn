using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cultiway.AbstractGame.AbstractEngine;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Systems;
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
        public CityExtendManager CityExtendManager { get; private set; }
        public        TileExtendManager       TileExtendManager    { get; private set; }
        public        CustomMapModeManager    CustomMapModeManager { get; private set; }
        public WorldboxGame Game { get; private set; }
        public        AEngine                 Engine               { get; }
        public SystemRoot GeneralLogicSystems  { get; private set; }
        public SystemRoot GeneralRenderSystems { get; private set; }
        public SystemRoot TileLogicSystems   { get; private set; }
        public SystemRoot TileRenderSystems  { get; private set; }
        public        EntityStore             W                    { get; private set; }
        public        WorldRecord             WorldRecord          { get; private set; }
        public        Core.SkillLibV2.Manager SkillV2              { get; private set; }
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
                            SkillV2.UpdateLogic(logic_update_tick);
                            Geo.UpdateLogic(logic_update_tick);
                        }
                    }
                    else
                    {
                        GeneralLogicSystems.Update(render_update_tick);
                        TileLogicSystems.Update(render_update_tick);
                        SkillV2.UpdateLogic(render_update_tick);
                        Geo.UpdateLogic(render_update_tick);
                    }
                }

                GeneralRenderSystems.Update(render_update_tick);
                TileRenderSystems.Update(render_update_tick);
                SkillV2.UpdateRender(render_update_tick);
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
            SkillV2.AppendPerfLog(sb);
            sb.Append('\n');
            Geo.AppendPerfLog(sb);
            LogInfo($"{sb}");
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
            CityExtendManager = new CityExtendManager(W);
            TileExtendManager = new();

            Try.Start(() =>
            {
                L = new Core.Libraries.Manager();
                L.Init();
            });

            GeneralLogicSystems = new SystemRoot(nameof(GeneralLogicSystems));
            GeneralRenderSystems = new SystemRoot(nameof(GeneralRenderSystems));
            TileLogicSystems = new SystemRoot(nameof(TileLogicSystems));
            TileRenderSystems = new SystemRoot(nameof(TileRenderSystems));

            GeneralLogicSystems.AddStore(W);
            GeneralRenderSystems.AddStore(W);
            TileLogicSystems.AddStore(TileExtendManager.World);
            TileRenderSystems.AddStore(TileExtendManager.World);
            
            GeneralLogicSystems.Add(new AnimFrameUpdateSystem(W));
            
            GeneralLogicSystems.Add(new AliveTimerSystem());
            GeneralLogicSystems.Add(new AliveTimerCheckSystem());
            
            GeneralLogicSystems.Add(new DisposeActorExtendSystem());
            GeneralLogicSystems.Add(new DisposeCityExtendSystem());
            GeneralLogicSystems.Add(new RecycleAnimRendererSystem());
            GeneralLogicSystems.Add(new RecycleStatusEffectSystem());
            GeneralLogicSystems.Add(new RecycleDefaultEntitySystem());
            
            GeneralLogicSystems.Add(new RestoreHealthSystem());
            GeneralLogicSystems.Add(new RestoreQiyunSystem());
            
            GeneralLogicSystems.Add(new SyncCityRelationSystem());
            
            GeneralRenderSystems.Add(new RenderAnimFrameSystem(W));

            CustomMapModeManager = new();
            CustomMapModeManager.Initialize();

            SkillV2 = new Core.SkillLibV2.Manager(Game);
            Geo = new Core.GeoLib.Manager(Game);
            _ui = new UI.Manager();
            _patch = new Patch.Manager();
            _content = new Manager();

            _ui.Init();
            _patch.Init();
            SkillV2.Init();
            _content.Init();

            ExampleTriggerActions.Init();
            ExampleSkillEntities.Init();

            if (Environment.UserName == "Inmny")
            {
                Config.isEditor = true;
                DebugConfig.setOption(DebugOption.FastCultures, true);
                DebugConfig.setOption(DebugOption.CityInfiniteResources, true);
                DebugConfig.setOption(DebugOption.CityFastConstruction, true);
                DebugConfig.setOption(DebugOption.CityFastUpgrades, true);
                
                GeneralLogicSystems.SetMonitorPerf(true);
                GeneralRenderSystems.SetMonitorPerf(true);
                SkillV2.SetMonitorPerf(true);
                Geo.SetMonitorPerf(true);
            }
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

            LM.ApplyLocale();
        }
    }
}