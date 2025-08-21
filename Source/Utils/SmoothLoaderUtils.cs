using System;

namespace Cultiway.Utils;

public static class SmoothLoaderUtils
{
    public static void Insert(MapLoaderAction action, string id, Func<MapLoaderContainer, bool> predicate)
    {
        for (int i = SmoothLoader._index; i < SmoothLoader._actions.Count; i++)
        {
            if (predicate(SmoothLoader._actions[i]))
            {
                SmoothLoader._actions.Insert(i, new MapLoaderContainer(action, id));
                break;
            }
        }
    }
}