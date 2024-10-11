using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Systems.Logic;
using Cultiway.Content.Systems.Render;

namespace Cultiway.Content;

internal class Manager
{
    private List<ICanInit> libraries = new();

    public void Init()
    {
        var ns = GetType().Namespace;
        var library_ts = ModClass.A.GetTypes()
                                 .Where(t => t.Namespace != null && t.BaseType != null && t.Namespace.StartsWith(ns) &&
                                             t.BaseType.Name.Contains("ExtendLibrary")).ToList();
        SortManagerTypes(library_ts);
        foreach (var t in library_ts)
        {
            var library = Activator.CreateInstance(t) as ICanInit;
            library?.Init();
            libraries.Add(library);
        }

        new Patch.Manager().Init();
        ModClass.I.LogicSystems.Add(new FlyCancelSystem());
        ModClass.I.RenderSystems.Add(new CloudRenderSystem());
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

    private static void SortManagerTypes(List<Type> manager_types)
    {
        for (int i = 0; i < manager_types.Count; i++)
        {
            var i_attr = manager_types[i].GetCustomAttribute<DependencyAttribute>();
            if (i_attr == null) continue;
            for (int j = i + 1; j < manager_types.Count; j++)
            {
                if (i_attr.Types.Contains(manager_types[j]))
                {
                    manager_types.Swap(i, j);
                    break;
                }
            }
        }
    }
}