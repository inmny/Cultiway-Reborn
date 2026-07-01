using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using Cultiway.Const;
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
            GeoRegion.map_mode = MetaTypeExtend.GeoRegion.Back();
            GeoRegion.option_id = CustomMapModeLibrary.GeoRegion.toggle_name;
            GeoRegion.power_option_zone_id = CustomMapModeLibrary.GeoRegion.toggle_name;
            GeoRegion.draw_zones = (_) => {};
            GeoRegion.check_cursor_highlight = (_, _, _) => {};
		    GeoRegion.check_tile_has_meta = CheckGeoRegionTileHasMeta;
		    GeoRegion.check_cursor_tooltip = CheckGeoRegionCursorTooltip;
            GeoRegion.tile_get_metaobject = (_, _) => GetGeoRegionUnderCursor();
            GeoRegion.tile_get_metaobject_0 = (_) => GetGeoRegionUnderCursor();
            GeoRegion.tile_get_metaobject_1 = (_) => GetGeoRegionUnderCursor();
            GeoRegion.tile_get_metaobject_2 = (_) => GetGeoRegionUnderCursor();
            GeoRegion.cursor_tooltip_action = ShowGeoRegionCursorTooltip;
            GeoRegion.click_action_zone = (tile, power) =>
            {
                if (tile == null) return false;
                var obj = GetGeoRegionForTile(tile);
                if (obj == null) return false;
                GeoRegion.selectAndInspect(obj);
                return true;
            };
		    GeoRegion.selected_tab_action_meta = new MetaTypeActionAsset(AssetManager.meta_type_library.defaultClickActionZone);
            GeoRegion.check_unit_has_meta = (Actor pActor) => pActor.current_tile.GetExtend().HasGeoRegion();
            GeoRegion.set_unit_set_meta_for_meta_for_window = delegate(Actor pActor)
            {
                I.SelectedGeoRegion = GetGeoRegionForTile(pActor.current_tile);
            };
            
            
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
            GeoRegion.stat_hover = (id, field) =>
            {
                var geoRegion = I.GeoRegions.get(id);
                if (geoRegion.isRekt()) return;
                Tooltip.show(field, Tooltips.GeoRegion.id, new TooltipData
                {
                    tip_name = id.ToString()
                });
            };
            GeoRegion.stat_click = (id, _) =>
            {
                var geoRegion = I.GeoRegions.get(id);
                if (geoRegion.isRekt()) return;
                GeoRegion.selectAndInspect(geoRegion, false, true, false);
            };
            GeoRegion.has_any = () => I.GeoRegions.Count > 0;
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
            GeoRegion.set_icon_for_cancel_button = true;
            GeoRegion.icon_list = "../../cultiway/icons/iconGeoRegionList";
            GeoRegion.icon_single_path = "../../cultiway/icons/iconGeoRegion";

            Sect.map_mode = MetaTypeExtend.Sect.Back();
            Sect.option_id = CustomMapModeLibrary.Sect.toggle_name;
            Sect.power_option_zone_id = CustomMapModeLibrary.Sect.toggle_name;
            Sect.force_zone_when_selected = true;
            Sect.has_dynamic_zones = true;
            Sect.dynamic_zone_option = 0;
            Sect.draw_zones = DrawSectZones;
            Sect.check_cursor_highlight = CheckSectCursorHighlight;
		    Sect.check_tile_has_meta = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkTileHasMetaDefault);
		    Sect.check_cursor_tooltip = new MetaZoneTooltipAction(AssetManager.meta_type_library.checkCursorTooltipDefault);
            Sect.tile_get_metaobject = (zone, _) => GetSectForZone(zone);
            Sect.tile_get_metaobject_0 = GetSectForZone;
            Sect.tile_get_metaobject_1 = GetSectForZone;
            Sect.tile_get_metaobject_2 = GetSectForZone;
            Sect.cursor_tooltip_action = ShowSectCursorTooltip;
            Sect.dynamic_zones = UpdateSectDynamicZones;
            Sect.window_name = Sect.id;
            Sect.window_action_clear = () => I.SelectedSect = null;
            Sect.selected_tab_action = () => ScrollWindow.showWindow(Sect.window_name);
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
            Sect.check_unit_has_meta = actor => GetSectForActor(actor) != null;
            Sect.set_unit_set_meta_for_meta_for_window = actor => I.SelectedSect = GetSectForActor(actor);
            Sect.click_action_zone = InspectSect;
            Sect.stat_hover = (id, field) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                Tooltip.show(field, Tooltips.Sect.id, new TooltipData()
                {
                    tip_name = id.ToString()
                });
            };
            Sect.stat_click = (id, _) =>
            {
                var sect = I.Sects.get(id);
                if (sect.isRekt()) return;
                Sect.selectAndInspect(sect, false, true, false);
            };
            Sect.set_icon_for_cancel_button = true;
            Sect.icon_list = "../../cultiway/icons/iconSectList";
            Sect.icon_single_path = "../../cultiway/icons/iconSect";
        }

        private static void DrawSectZones(MetaTypeAsset asset)
        {
            UpdateSectDynamicZones();

            ZoneCalculator zoneCalculator = World.world.zone_calculator;
            foreach (ZoneMetaData data in ZoneMetaDataVisualizer.zone_data_dict.Values)
            {
                if (data.meta_object is not Sect sect || sect.isRekt()) continue;
                if (data.zone == null) continue;

                zoneCalculator.drawBegin();
                zoneCalculator.drawGenericFluid(data, asset);
                zoneCalculator.drawEnd(data.zone);
            }
        }

        private static void UpdateSectDynamicZones()
        {
            List<Sect> sects = I.Sects.list;
            double worldTime = World.world.getCurWorldTime();
            for (int i = 0; i < sects.Count; i++)
            {
                Sect sect = sects[i];
                if (sect == null || sect.isRekt()) continue;

                List<TileZone> zones = sect.GetResidenceZones();
                for (int j = 0; j < zones.Count; j++)
                {
                    ZoneMetaDataVisualizer.countMetaZone(zones[j], sect, worldTime);
                }
            }
        }

        private static void CheckSectCursorHighlight(MetaTypeAsset asset, WorldTile tile, QuantumSpriteAsset quantumSprite)
        {
            Sect sect = GetSectForTile(tile);
            if (sect == null) return;

            using (ListPool<TileZone> zones = ZoneMetaDataVisualizer.getZonesWithMeta(sect))
            {
                QuantumSpriteLibrary.colorZones(quantumSprite, zones, quantumSprite.color);
            }
        }

        private static bool InspectSect(WorldTile tile, string power = null)
        {
            Sect sect = GetSectForTile(tile);
            if (sect == null) return false;

            Sect.selectAndInspect(sect, false, true, false);
            return true;
        }

        private static Sect GetSectForTile(WorldTile tile)
        {
            return tile == null ? null : GetSectForZone(tile.zone);
        }

        private static Sect GetSectForZone(TileZone zone)
        {
            if (zone == null) return null;

            ZoneMetaData data = ZoneMetaDataVisualizer.getZoneMetaData(zone);
            Sect sect = data.meta_object as Sect;
            return sect == null || sect.isRekt() ? null : sect;
        }

        private static Sect GetSectForActor(Actor actor)
        {
            if (actor == null || actor.isRekt()) return null;

            Sect sect = actor.GetExtend().sect;
            return sect == null || sect.isRekt() ? null : sect;
        }

        private static void ShowSectCursorTooltip(NanoObject meta)
        {
            Sect sect = meta as Sect;
            if (sect == null || sect.isRekt()) return;
            if (Tooltip.isShowingFor(sect)) return;

            Tooltip.hideTooltip(sect, true, Tooltips.Sect.id);
            Tooltip.show(sect, Tooltips.Sect.id, new TooltipData
            {
                tip_name = sect.id.ToString(),
                tooltip_scale = 0.7f,
                is_sim_tooltip = true
            });
        }

        private static bool CheckGeoRegionTileHasMeta(TileZone pZone, MetaTypeAsset pAsset, int pZoneOption)
        {
            return GetGeoRegionForTile(World.world.getMouseTilePosCachedFrame()) != null;
        }

        private static bool CheckGeoRegionCursorTooltip(TileZone pZone, MetaTypeAsset pAsset, int pZoneOption)
        {
            var geoRegion = GetGeoRegionForTile(World.world.getMouseTilePosCachedFrame());
            if (geoRegion == null) return false;

            ShowGeoRegionCursorTooltip(geoRegion);
            return true;
        }

        private static IMetaObject GetGeoRegionUnderCursor()
        {
            return GetGeoRegionForTile(World.world.getMouseTilePosCachedFrame());
        }

        private static Cultiway.Core.GeoRegion GetGeoRegionForTile(WorldTile tile)
        {
            if (tile == null) return null;

            var manager = ModClass.I?.CustomMapModeManager;
            var mapMode = manager?.CurrMapMode;
            return I.GeoRegions.GetGeoRegionForTile(tile, mapMode);
        }

        private static void ShowGeoRegionCursorTooltip(NanoObject pMeta)
        {
            var geoRegion = pMeta as Cultiway.Core.GeoRegion;
            if (geoRegion == null || geoRegion.isRekt()) return;
            if (Tooltip.isShowingFor(geoRegion)) return;

            Tooltip.hideTooltip(geoRegion, true, Tooltips.GeoRegion.id);
            Tooltip.show(geoRegion, Tooltips.GeoRegion.id, new TooltipData
            {
                tip_name = geoRegion.id.ToString(),
                tooltip_scale = 0.7f,
                is_sim_tooltip = true
            });
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
