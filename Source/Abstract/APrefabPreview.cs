using NeoModLoader.General.UI.Prefabs;
using NeoModLoader.utils;
using UnityEngine;

namespace Cultiway.Abstract;

public abstract class APrefabPreview<T> : APrefab<T> where T : APrefabPreview<T>
{
    protected sealed override void Init()
    {
        if (Initialized) return;
        base.Init();
        OnInit();
    }

    protected virtual void OnInit()
    {
    }

    public static void PatchTo<TComponentType>(string pPath) where TComponentType : Component
    {
        ResourcesPatch.PatchResource(pPath, Prefab.GetComponent<TComponentType>());
    }
}