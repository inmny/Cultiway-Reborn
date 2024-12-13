using Cultiway.Abstract;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class CreatureInfoPage : APrefabPreview<CreatureInfoPage>
{
    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(CreatureInfoPage), typeof(RectTransform));
        Prefab = obj.AddComponent<CreatureInfoPage>();
    }
}