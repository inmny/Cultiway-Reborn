using System;
using Cultiway.Abstract;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class SpecialItemTooltip : APrefabPreview<SpecialItemTooltip>
{
    private static Action<GameObject> _initiators;

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);


        _initiators?.Invoke(obj);
        Prefab = obj.AddComponent<SpecialItemTooltip>();
    }

    public static void RegisterInitiator(Action<GameObject> initiator)
    {
        _initiators += initiator;
    }
}