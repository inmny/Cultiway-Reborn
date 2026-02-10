using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway;

public partial class WorldboxGame
{
    public class MetaTypes : ExtendLibrary<MetaTypeAsset, MetaTypes>
    {
        public static MetaTypeAsset Sect { get; private set; }
        public static MetaTypeAsset GeoRegion { get; private set; }
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            GeoRegion.option_id = CustomMapModeLibrary.GeoRegion.toggle_name;
            GeoRegion.draw_zones = (_) => {};
            GeoRegion.check_cursor_highlight = (_, _, _) => {}; // TODO: GeoRegion所属tiles高亮
		    GeoRegion.check_tile_has_meta = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkTileHasMetaDefault);
		    GeoRegion.check_cursor_tooltip = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkCursorTooltipDefault);
            GeoRegion.tile_get_metaobject = (_, _) => null;
            GeoRegion.tile_get_metaobject_0 = (_) => null;
            GeoRegion.tile_get_metaobject_1 = (_) => null;
            GeoRegion.tile_get_metaobject_2 = (_) => null;
            GeoRegion.cursor_tooltip_action = (_) => {};
            
            
            GeoRegion.window_name = GeoRegion.id;
            GeoRegion.window_action_clear = () => I.SelectedGeoRegion = null;
            GeoRegion.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionUpdate = (data) =>
            {
                data.StoredObj[GeoRegion.id] = I.SelectedGeoRegion;
            };
            GeoRegion.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionRestore = (data) =>
            {
                I.SelectedGeoRegion = data.StoredObj[GeoRegion.id] as GeoRegion;
            };
            GeoRegion.get_selected = () => I.SelectedGeoRegion;
            GeoRegion.set_selected = (geoRegion) => I.SelectedGeoRegion = geoRegion as GeoRegion;
            GeoRegion.get_list = () => I.GeoRegions;
            GeoRegion.get = (id) => I.GeoRegions.get(id);
            GeoRegion.custom_sorted_list = () =>
            {
                var list = new ListPool<NanoObject>(64);
                foreach (var geoRegion in I.GeoRegions)
                {
                    if (geoRegion.isFavorite())
                        list.Add(geoRegion);
                }

                return list;
            };
            GeoRegion.power_tab_id = PowerTabs.SelectedGeoRegion.id;

            Sect.option_id = CustomMapModeLibrary.Sect.toggle_name;
            Sect.draw_zones = (_) => {};
            Sect.check_cursor_highlight = (_, _, _) => {}; // TODO: GeoRegion所属tiles高亮
		    Sect.check_tile_has_meta = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkTileHasMetaDefault);
		    Sect.check_cursor_tooltip = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkCursorTooltipDefault);
            Sect.tile_get_metaobject = (_, _) => null;
            Sect.tile_get_metaobject_0 = (_) => null;
            Sect.tile_get_metaobject_1 = (_) => null;
            Sect.tile_get_metaobject_2 = (_) => null;
            Sect.cursor_tooltip_action = (_) => {};
            Sect.window_name = Sect.id;
            Sect.window_action_clear = () => I.SelectedSect = null;
            Sect.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionUpdate = (data) =>
            {
                data.StoredObj[Sect.id] = I.SelectedSect;
            };
            Sect.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionRestore = (data) =>
            {
                I.SelectedSect = data.StoredObj[Sect.id] as Sect;
            };
            Sect.get_list = () => I.Sects;
            Sect.custom_sorted_list = () =>
            {
                var list = new ListPool<NanoObject>(64);
                foreach (var sect in I.Sects)
                {
                    if (sect.isFavorite())
                        list.Add(sect);
                }

                return list;
            };
            Sect.has_any = () => I.Sects.hasAny();
            Sect.get_selected = () => I.SelectedSect;
            Sect.set_selected = (sect) => I.SelectedSect = sect as Sect;
            Sect.get = (id) => I.Sects.get(id);
            Sect.stat_hover = (id, field) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                Tooltip.show(field, Tooltips.Sect.id, new TooltipData()
                {
                    tip_description = id.ToString()
                });
            };
            Sect.stat_click = (id, _) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                I.SelectedSect = sect;
                //ScrollWindow.showWindow();
            };
        }

        protected override void PostInit(MetaTypeAsset asset)
        {
            base.PostInit(asset);
            if (asset.decision_ids != null)
			{
				asset.decisions_assets = new DecisionAsset[asset.decision_ids.Length];
				for (int i = 0; i < asset.decision_ids.Length; i++)
				{
					string tDecisionID = asset.decision_ids[i];
					DecisionAsset tDecisionAsset = AssetManager.decisions_library.get(tDecisionID);
					asset.decisions_assets[i] = tDecisionAsset;
				}
			}
			if (!string.IsNullOrEmpty(asset.option_id))
			{
				asset.option_asset = AssetManager.options_library.get(asset.option_id);
			}
        }
    }
}