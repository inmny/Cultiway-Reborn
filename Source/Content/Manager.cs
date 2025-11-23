using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
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
            try
            {
                library?.Init();
                ModClass.LogInfo($"({nameof(Content)}) initializes {t}");
            }
            catch (Exception e)
            {
                ModClass.LogError($"({nameof(Content)}) failed to initialize {t}\n{e.Message}\n{e.StackTrace}");
            }
            libraries.Add(library);
        }

        new Patch.Manager().Init();
        ModClass.I.GeneralLogicSystems.Add(new FlyCancelSystem());
        ModClass.I.GeneralLogicSystems.Add(new RestoreWakanSystem());
        ModClass.I.GeneralLogicSystems.Add(new WakanSpreadSystem());
        ModClass.I.GeneralLogicSystems.Add(new CityDistributeItemsSystem());
        ModClass.I.GeneralRenderSystems.Add(new CloudRenderSystem());
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
