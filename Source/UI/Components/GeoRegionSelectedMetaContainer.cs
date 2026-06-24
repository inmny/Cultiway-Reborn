using System.Collections.Generic;
using Cultiway.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

internal class GeoRegionSelectedMetaContainer : GeoRegionSelectedContainerBase
{
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
    protected override string BackgroundTitleKey => "Cultiway.SelectedGeoRegion.Contains";

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

    protected override void Build(GeoRegion region)
    {
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        List<GeoRegion> containedRegions = manager.GetContainedRegions(region, 8);

        for (int i = 0; i < containedRegions.Count; i++)
        {
            AddGeoRegionBanner(containedRegions[i]);
        }

        if (containedRegions.Count > 0) return;

        List<City> cities = manager.GetCitiesInRegion(region, 6);
        for (int i = 0; i < cities.Count; i++)
        {
            AddCityBanner(cities[i]);
        }
    }
}
