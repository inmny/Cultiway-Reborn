using System;
using System.IO;
using System.Reflection;
using Cultiway.AbstractGame.AbstractEngine;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Core.SkillLibV2.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Debug;
using Cultiway.LocaleKeys;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using NeoModLoader.utils;
using UnityEngine;

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

        [Hotfixable]
        private void Update()
        {
            if (Game.IsPaused()) return;
            var update_tick = new UpdateTick(Game.GetLogicDeltaTime(), Game.GetGameTime());
            GeneralLogicSystems.Update(update_tick);
            TileLogicSystems.Update(update_tick);
            SkillV2.UpdateLogic(update_tick);
            Geo.UpdateLogic(update_tick);

            GeneralRenderSystems.Update(update_tick);
            TileRenderSystems.Update(update_tick);
            SkillV2.UpdateRender(update_tick);
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
        }

        public static GameObject NewPrefabPreview(string name, params Type[] types)
        {
            var obj = new GameObject(name, types);
            obj.transform.SetParent(Instance.PrefabLibrary);
            return obj;
        }

        protected override void OnModLoad()
        {
            Try.Start(() => { _ = LK.Root; });
            A = Assembly.GetExecutingAssembly();
            PrefabLibrary.gameObject.SetActive(false);
            Game = new WorldboxGame();
            Try.Start(() => { W = new EntityStore(); });

            WorldRecord = new(W);

            LoadLocales();

            ActorExtendManager = new ActorExtendManager(W);
            CityExtendManager = new CityExtendManager(W);
            TileExtendManager = new();

            L = new Core.Libraries.Manager();
            L.Init();

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
            
            GeneralLogicSystems.Add(new RecycleAnimRendererSystem());
            GeneralLogicSystems.Add(new RecycleStatusEffectSystem());
            GeneralLogicSystems.Add(new RecycleDefaultEntitySystem());
            
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