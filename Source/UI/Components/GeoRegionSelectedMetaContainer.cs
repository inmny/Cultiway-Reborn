using System.Collections.Generic;
using System.Text;
using Cultiway.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>选中底栏的内部对象区域，优先显示子地区；没有子地区时显示地区内的城市。</summary>
internal class GeoRegionSelectedMetaContainer : GeoRegionSelectedContainerBase
{
    // 当前准备显示的子地区或城市名单。
    private List<GeoRegion> _resolvedRegions = new();
    private List<City> _resolvedCities = new();

    // 双列旗帜区域的尺寸、留白和从上到下排列方式。
    protected override float LeftPadding => 8f;
    protected override float RightPadding => 8f;
    protected override float MinimumWidth => 180f;
    protected override float MinimumHeight => 92f;
    protected override bool UseHostAsGrid => true;
    protected override int ConstraintCount => 2;
    protected override LayoutGroupExt.GridLayoutGroupExtended.Axis StartAxis => LayoutGroupExt.GridLayoutGroupExtended.Axis.Vertical;
    protected override TextAnchor ChildAlignment => TextAnchor.UpperLeft;
    protected override Vector2 CellSize => new(36f, 44f);
    protected override Vector2 GridSpacing => new(6f, 2f);
    /// <summary>内部对象区域背景标题的本地化文本编号。</summary>
    protected override string BackgroundTitleKey => "Cultiway.SelectedGeoRegion.Contains";

    /// <summary>隐藏原版国家内部旗帜，避免与地区的子地区或城市重复显示。</summary>
    protected override void CleanupOriginalChildren()
    {
        BannerBase[] banners = GetComponentsInChildren<BannerBase>(true);
        for (int i = 0; i < banners.Length; i++)
        {
            GameObject obj = banners[i].gameObject;
            obj.SetActive(false);
            if (obj.TryGetComponent(out LayoutElement layout))
            {
                layout.ignoreLayout = true;
            }
        }
    }

    /// <summary>查找子地区或城市；显示名单变化时让底栏重建旗帜。</summary>
    protected override string GetRefreshKey(GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        _resolvedRegions = manager.GetContainedRegions(region, 8);
        _resolvedCities = _resolvedRegions.Count == 0
            ? manager.GetCitiesInRegion(region, 6)
            : new List<City>();

        var key = new StringBuilder();
        key.Append(region.getID()).Append(_resolvedRegions.Count > 0 ? "|regions" : "|cities");
        if (_resolvedRegions.Count > 0)
        {
            for (int i = 0; i < _resolvedRegions.Count; i++)
            {
                key.Append('|').Append(_resolvedRegions[i].getID());
            }
        }
        else
        {
            for (int i = 0; i < _resolvedCities.Count; i++)
            {
                key.Append('|').Append(_resolvedCities[i].getID());
            }
        }
        return key.ToString();
    }

    /// <summary>优先添加子地区旗帜，没有子地区时改为添加城市旗帜。</summary>
    protected override void Build(GeoRegion region)
    {
        for (int i = 0; i < _resolvedRegions.Count; i++)
        {
            AddGeoRegionBanner(_resolvedRegions[i]);
        }

        if (_resolvedRegions.Count > 0) return;

        for (int i = 0; i < _resolvedCities.Count; i++)
        {
            AddCityBanner(_resolvedCities[i]);
        }
    }
}
