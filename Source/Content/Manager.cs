using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.ActorComponents;
using Cultiway.Content.Systems.Logic;
using Cultiway.Content.Systems.Render;

namespace Cultiway.Content;

internal class Manager
{
    private List<ICanInit> libraries = new();

    public void Init()
    {
        Libraries.Manager.Init();

        var ns = GetType().Namespace;
        var library_ts = ModClass.A.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith(ns) &&
                        t.GetInterfaces().Contains(typeof(ICanInit))).ToList();
        library_ts = DependencyAttribute.SortManagerTypes(library_ts);
        foreach (var t in library_ts)
        {
            var library = Activator.CreateInstance(t) as ICanInit;

            if (library == null) 
            {
                ModClass.LogError($"({nameof(Content)}) failed to create instance of {t}");
                continue;
            }
            libraries.Add(library);
        }
        foreach (var library in libraries)
        {
            try
            {
                library.Init();
                ModClass.LogInfo($"({nameof(Content)}) initializes {library.GetType().Name}");
            }
            catch (Exception e)
            {
                ModClass.LogError($"({nameof(Content)}) failed to initialize {library.GetType().Name}\n{e.Message}\n{e.StackTrace}");
            }
        }

        new Patch.Manager().Init();
        ModClass.I.GeneralLogicSystems.Add(new FlyCancelSystem());
        ModClass.I.GeneralLogicSystems.Add(new RestoreWakanSystem());
        ModClass.I.GeneralLogicSystems.Add(new WakanSpreadSystem());
        ModClass.I.GeneralLogicSystems.Add(new CityDistributeItemsSystem());
        ModClass.I.GeneralLogicSystems.Add(new ContinuousCultivateSystem());
        ModClass.I.GeneralRenderSystems.Add(new BreakthroughVisualSystem());
        ModClass.I.GeneralRenderSystems.Add(new CloudRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmAuraRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmElementParticleRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmIndicatorRenderSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new CultibookGeneratedEventSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new CultibookImprovedEventSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new ElixirEffectGeneratedEventSystem());
        
        CultivateMethodTriggers.Init();
        Train.Init();
    }

    public void OnReload()
    {
        foreach (var l in libraries)
        {
            if (l is not ICanReload lr)
                continue;
            lr.OnReload();
        }
    }
}
