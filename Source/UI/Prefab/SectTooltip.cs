using Cultiway.Abstract;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class SectTooltip : APrefabPreview<SectTooltip>
{
    public Tooltip Tooltip { get; private set; }
    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
    }

    public void Setup(Sect sect)
    {
        Init();
    }
    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        
        Prefab = obj.AddComponent<SectTooltip>();
    }
}