using System;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class GameObjectTools
{
    public static GameObject NewChild(this GameObject parent, string name, params Type[] types)
    {
        var go = new GameObject(name, types);
        go.transform.SetParent(parent.transform);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;
        return go;
    }
}