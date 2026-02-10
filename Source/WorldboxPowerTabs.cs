using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class PowerTabs : ExtendLibrary<PowerTabAsset, PowerTabs>
    {
        public static PowerTabAsset SelectedGeoRegion { get; private set; }
        public static PowerTabAsset SelectedSect { get; private set; }

        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            SelectedGeoRegion.meta_type = MetaTypeExtend.GeoRegion.Back();
            SelectedGeoRegion.window_id = MetaTypes.GeoRegion.id;
            SelectedGeoRegion.get_power_tab = () => null;
            SelectedGeoRegion.on_update_check_active = new PowerTabActionCheck(AssetManager.power_tab_library.defaultOnUpdateCheckActive);
            SelectedGeoRegion.on_main_tab_select = new PowerTabAction(AssetManager.power_tab_library.defaultMainTabSelect);
            SelectedGeoRegion.on_main_info_click = new PowerTabAction(AssetManager.power_tab_library.defaultMainInfoClick);
            SelectedGeoRegion.get_localized_worldtip = new PowerTabWorldtipAction(AssetManager.power_tab_library.getWorldTipTextMetaName);
        }
    }
}