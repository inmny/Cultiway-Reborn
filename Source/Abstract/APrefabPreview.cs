using NeoModLoader.General.UI.Prefabs;
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
}