using System;
using System.IO;
using System.Reflection;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Core.SkillLibV2.Predefined;
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

        private       UI.Manager             _ui;
        public static Assembly               A                    { get; private set; }
        public static Core.Libraries.Manager L                    { get; private set; }
        public        ActorExtendManager     ActorExtendManager   { get; private set; }
        public        TileExtendManager      TileExtendManager    { get; private set; }
        public        CustomMapModeManager   CustomMapModeManager { get; private set; }

        public SystemRoot              RenderSystems { get; private set; }
        public SystemRoot              LogicSystems  { get; private set; }
        public EntityStore             W             { get; private set; }
        public WorldRecord             WorldRecord   { get; private set; }
        public Core.SkillLibV2.Manager SkillV2 { get; private set; }
        public Core.GeoLib.Manager     Geo           { get; private set; }

        private void Start()
        {
            L.PostInit();
        }

        [Hotfixable]
        private void Update()
        {
            var update_tick = new UpdateTick(Time.deltaTime, (float)World.world.getCurSessionTime());
            LogicSystems.Update(update_tick);
            SkillV2.UpdateLogic(update_tick);
            Geo.UpdateLogic(update_tick);

            RenderSystems.Update(update_tick);
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

        protected override void OnModLoad()
        {
            A = Assembly.GetExecutingAssembly();
            PrefabLibrary.gameObject.SetActive(false);
            try
            {
                W = new EntityStore();
            }
            catch (Exception e)
            {
                do
                {
                    LogError($"{e.GetType()}: {e.Message}\n{e.StackTrace}");
                    e = e.InnerException;
                } while (e != null);
            }

            WorldRecord = new(W);

            LoadLocales();

            ActorExtendManager = new();
            TileExtendManager = new();

            L = new Core.Libraries.Manager();
            L.Init();

            LogicSystems = new SystemRoot(nameof(LogicSystems));
            RenderSystems = new SystemRoot(nameof(RenderSystems));

            LogicSystems.AddStore(ActorExtendManager.World);
            LogicSystems.AddStore(TileExtendManager.World);
            RenderSystems.AddStore(ActorExtendManager.World);
            RenderSystems.AddStore(TileExtendManager.World);

            CustomMapModeManager = new();
            CustomMapModeManager.Initialize();

            SkillV2 = new Core.SkillLibV2.Manager();
            Geo = new();
            _ui = new UI.Manager();
            _patch = new Patch.Manager();
            _content = new Manager();

            _ui.Init();
            _patch.Init();
            _content.Init();

            TriggerActions.Init();
            Trajectories.Init();
            SkillEntities.Init();

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