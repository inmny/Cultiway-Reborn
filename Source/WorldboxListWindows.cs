using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class ListWindows : ExtendLibrary<ListWindowAsset, ListWindows>
    {
        public static ListWindowAsset GeoRegionList { get; private set; }
        public static ListWindowAsset SectList { get; private set; }

        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            GeoRegionList.meta_type = MetaTypeExtend.GeoRegion.Back();
            GeoRegionList.no_items_locale = "list_empty_geo_regions";
            GeoRegionList.art_path = "cultiway/illustrations/art_geo_regions";
            GeoRegionList.icon_path = "cultiway/icons/iconGeoRegionList";
            GeoRegionList.set_list_component = t => t.AddComponent<GeoRegionListComponent>();

            SectList.meta_type = MetaTypeExtend.Sect.Back();
            SectList.no_items_locale = "list_empty_sects";
            SectList.art_path = "cultiway/illustrations/art_geo_regions";
            SectList.icon_path = "cultiway/icons/iconSectList";
            SectList.set_list_component = t => t.AddComponent<SectListComponent>();
        }
        protected override void PostInit(ListWindowAsset asset)
        {
            base.PostInit(asset);
            var lib = AssetManager.list_window_library;
            lib._dict.Add(asset.meta_type, asset);
        }
    }
}
