using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class GeoRegionTooltip : APrefabPreview<GeoRegionTooltip>
{
    public Tooltip Tooltip { get; private set; }
    private GeoRegionBanner _banner;

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
        _banner = GetComponentInChildren<GeoRegionBanner>(true);
    }

    public void Setup(GeoRegion geoRegion)
    {
        Init();
        if (_banner == null)
        {
            throw new System.InvalidOperationException("GeoRegion tooltip 缺少地区 banner");
        }

        _banner.load(geoRegion);
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>("tooltips/tooltip_kingdom"), ModClass.I.PrefabLibrary);
        obj.name = "tooltip_cultiway_geo_region";
        obj.transform.HideChildrenByPath("Traits Background");
        ReplaceKingdomBanners(obj);

        Prefab = obj.AddComponent<GeoRegionTooltip>();
    }

    private static void ReplaceKingdomBanners(GameObject obj)
    {
        KingdomBanner[] banners = obj.GetComponentsInChildren<KingdomBanner>(true);
        if (banners.Length == 0)
        {
            throw new System.InvalidOperationException("GeoRegion tooltip 原版 Header 缺少 KingdomBanner");
        }

        for (int i = 0; i < banners.Length; i++)
        {
            KingdomBanner kingdomBanner = banners[i];
            GeoRegionBanner geoRegionBanner = kingdomBanner.gameObject.AddComponent<GeoRegionBanner>();
            kingdomBanner.CopyCompatibleSerializedFieldsTo(geoRegionBanner);
            Object.DestroyImmediate(kingdomBanner);
        }
    }
}
