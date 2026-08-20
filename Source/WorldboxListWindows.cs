using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.UI.Components;
using Cultiway.Content.UI.SpiritVeins;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class ListWindows : ExtendLibrary<ListWindowAsset, ListWindows>
    {
        public const string SpiritVeinListId = "Cultiway.SpiritVeinList";

        public static ListWindowAsset GeoRegionList { get; private set; }
        public static ListWindowAsset SectList { get; private set; }
        public static ListWindowAsset SpiritVeinList { get; private set; }

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

            SpiritVeinList.meta_type = MetaTypeExtend.SpiritVein.Back();
            SpiritVeinList.no_items_locale = "list_empty_spirit_veins";
            SpiritVeinList.art_path = "cultiway/illustrations/art_geo_regions";
            SpiritVeinList.icon_path = "cultiway/icons/iconSpiritVein";
            SpiritVeinList.set_list_component = t => t.AddComponent<SpiritVeinListComponent>();
        }
        protected override void PostInit(ListWindowAsset asset)
        {
            base.PostInit(asset);
            var lib = AssetManager.list_window_library;
            lib._dict.Add(asset.meta_type, asset);
        }
    }
}
