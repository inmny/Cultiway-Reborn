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
            library?.Init();
            ModClass.LogInfo($"({nameof(Content)}) initializes {t}");
            libraries.Add(library);
        }

        new Patch.Manager().Init();
        ModClass.I.GeneralLogicSystems.Add(new FlyCancelSystem());
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