using Cultiway.Abstract;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class GeoRegionTooltip : APrefabPreview<GeoRegionTooltip>
{
    public Tooltip Tooltip { get; private set; }
    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
    }

    public void Setup(GeoRegion geoRegion)
    {
        Init();
    }
    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        
        Prefab = obj.AddComponent<GeoRegionTooltip>();
    }
}