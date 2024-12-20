using NeoModLoader.General.UI.Prefabs;
using NeoModLoader.utils;
using UnityEngine;

namespace Cultiway.Abstract;

public abstract class APrefabPreview<T> : APrefab<T> where T : APrefabPreview<T>
{
    public static T Instantiate(Transform pParent = null, bool pWorldPositionStays = false, string pName = null)
    {
        T t = Instantiate(Prefab, pParent, pWorldPositionStays);
        if (!string.IsNullOrEmpty(pName))
            t.name = pName;
        return t;
    }

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