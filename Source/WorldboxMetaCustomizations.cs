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
            Sect.meta_type = MetaTypeExtend.Sect.Back();
            Sect.localization_title = "Sect";
            Sect.banner_prefab_id = "ui/PrefabBannerSect";
            ResourcesPatch.PatchResource(Sect.banner_prefab_id, SectBanner.Prefab);
            Sect.get_banner = delegate(MetaCustomizationAsset pAsset, NanoObject pNanoObject, Transform pParent)
            {
                SectBanner sectBanner = Object.Instantiate<SectBanner>(SectBanner.Prefab, pParent);
                sectBanner.enable_default_click = false;
                sectBanner.load(pNanoObject as Sect);
                return sectBanner;
            };
            Sect.option_1_get = () => I.SelectedSect == null ? 0 : I.SelectedSect.data.BannerBackgroundIndex;
            Sect.option_1_set = delegate(int pValue)
            {
                if (I.SelectedSect != null)
                {
                    I.SelectedSect.data.BannerBackgroundIndex = pValue;
                }
            };
            Sect.option_2_get = () => I.SelectedSect == null ? 0 : I.SelectedSect.data.BannerIconIndex;
            Sect.option_2_set = delegate(int pValue)
            {
                if (I.SelectedSect != null)
                {
                    I.SelectedSect.data.BannerIconIndex = pValue;
                }
            };
            Sect.color_get = () => I.SelectedSect == null ? 0 : I.SelectedSect.data.color_id;
            Sect.color_set = delegate(int pValue)
            {
                I.SelectedSect?.data.setColorID(pValue);
            };
            Sect.color_library = () => AssetManager.families_colors_library;
            Sect.option_1_count = () => ModClass.L.SectBannerLibrary.getCurrentAsset().backgrounds.Count;
            Sect.option_2_count = () => ModClass.L.SectBannerLibrary.getCurrentAsset().icons.Count;
            Sect.title_locale = "Sect";
            Sect.option_1_locale = "banner_design";
            Sect.option_2_locale = "banner_emblem";
            Sect.color_locale = "family_color";
            Sect.icon_banner = "iconWorldInfo";
            Sect.icon_creature = "iconBiomass";

            GeoRegion.meta_type = MetaTypeExtend.GeoRegion.Back();
            GeoRegion.localization_title = "GeoRegion";
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
            GeoRegion.option_1_editable = false;
            GeoRegion.option_2_editable = false;
            GeoRegion.option_2_color_editable = false;
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
            GeoRegion.option_1_count = () => 1;
            GeoRegion.option_2_count = () => 1;
            GeoRegion.title_locale = "customize_geo_region";
            GeoRegion.option_1_locale = "banner_design";
            GeoRegion.option_2_locale = "banner_emblem";
            GeoRegion.color_locale = "family_color";
            GeoRegion.icon_banner = "iconWorldInfo";
            GeoRegion.icon_creature = "iconBiomass";
        }
    }
}
