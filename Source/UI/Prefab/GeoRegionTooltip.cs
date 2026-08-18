using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Prefab;

/// <summary>地区悬停提示的可复用内容，使用原版国家提示外观展示地区旗帜和信息。</summary>
public class GeoRegionTooltip : APrefabPreview<GeoRegionTooltip>
{
    /// <summary>承载提示文字和位置的原版提示组件。</summary>
    public Tooltip Tooltip { get; private set; }
    // 提示顶部显示的地区旗帜。
    private GeoRegionBanner _banner;

    /// <summary>首次使用提示时找到原版提示组件和地区旗帜。</summary>
    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
        _banner = GetComponentInChildren<GeoRegionBanner>(true);
    }

    /// <summary>将指定地区载入提示旗帜，玩家悬停地区入口时即可看到对应信息。</summary>
    public void Setup(GeoRegion geoRegion)
    {
        Init();
        if (_banner == null)
        {
            throw new System.InvalidOperationException("GeoRegion tooltip 缺少地区 banner");
        }

        _banner.load(geoRegion);
    }

    /// <summary>以原版国家提示为基础创建地区提示，并移除地区不需要的特性区域。</summary>
    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>("tooltips/tooltip_kingdom"), ModClass.I.PrefabLibrary);
        obj.name = "tooltip_cultiway_geo_region";
        obj.transform.HideChildrenByPath("Traits Background");
        ReplaceKingdomBanners(obj);

        Prefab = obj.AddComponent<GeoRegionTooltip>();
    }

    /// <summary>把提示中的所有国家旗帜替换为地区旗帜，保留原有图像位置和样式。</summary>
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
