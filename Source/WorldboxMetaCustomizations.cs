using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.utils;
using strings;
using UnityEngine;

namespace Cultiway;

public partial class WorldboxGame
{
    public class MetaCustomizations : ExtendLibrary<MetaCustomizationAsset, MetaCustomizations>
    {
        public static MetaCustomizationAsset Sect { get; private set; }
        public static MetaCustomizationAsset GeoRegion { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            GeoRegion.meta_type = MetaTypeExtend.GeoRegion.Back();
            GeoRegion.banner_prefab_id = "ui/PrefabBannerGeoRegion";
            ResourcesPatch.PatchResource(GeoRegion.banner_prefab_id, GeoRegionBanner.Prefab);
            GeoRegion.get_banner = delegate(MetaCustomizationAsset pAsset, NanoObject pNanoObject, Transform pParent)
            {
                GeoRegionBanner geoRegionBanner = Object.Instantiate<GeoRegionBanner>(GeoRegionBanner.Prefab, pParent);
                geoRegionBanner.enable_default_click = false;
                geoRegionBanner.load(pNanoObject as GeoRegion);
                return geoRegionBanner;
            };
            GeoRegion.customize_component = delegate(GameObject pGameObject)
            {
                pGameObject.AddComponent<GeoRegionCustomizeWindow>();
            };
            GeoRegion.customize_window_id = "geo_region_customize";
            GeoRegion.option_1_get = () => I.SelectedGeoRegion.data.BannerBackgroundIndex;            
            GeoRegion.option_1_set = delegate(int pValue)
            {
                I.SelectedGeoRegion.data.BannerBackgroundIndex = pValue;    
            };
            GeoRegion.option_2_get = () => I.SelectedGeoRegion.data.BannerIconIndex;
            GeoRegion.option_2_set = delegate(int pValue)
            {
                I.SelectedGeoRegion.data.BannerIconIndex = pValue;
            };
            GeoRegion.color_get = () => I.SelectedGeoRegion.data.color_id;
            GeoRegion.color_set = delegate(int pValue)
            {
                I.SelectedGeoRegion.data.setColorID(pValue);
            };
            GeoRegion.color_library = () => AssetManager.families_colors_library;
            GeoRegion.option_1_count = () => ModClass.L.GeoRegionBannerLibrary.get(I.SelectedGeoRegion.getActorAsset().banner_id).backgrounds.Count;
            GeoRegion.option_2_count = () => ModClass.L.GeoRegionBannerLibrary.get(I.SelectedGeoRegion.getActorAsset().banner_id).icons.Count;
            GeoRegion.title_locale = "customize_geo_region";
            GeoRegion.option_1_locale = "banner_design";
            GeoRegion.option_2_locale = "banner_emblem";
            GeoRegion.color_locale = "family_color";
            GeoRegion.icon_banner = "iconCrown";
            GeoRegion.icon_creature = "iconBiomass";
        }
    }
}