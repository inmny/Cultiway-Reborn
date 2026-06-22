using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class CustomMapModeLibrary : AssetLibrary<CustomMapModeAsset>
{
    public static CustomMapModeAsset Sect { get; private set; }
    public static CustomMapModeAsset GeoRegion { get; private set; }
    public static CustomMapModeAsset GeoRegionLandform { get; private set; }
    public static CustomMapModeAsset GeoRegionLandmass { get; private set; }
    public static CustomMapModeAsset GeoRegionMorphology { get; private set; }
    public override void init()
    {
        Sect = add(new CustomMapModeAsset()
        {
            id = "sect",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "sect_layer",
            kernel_func = (WorldTile worldTile, ref Color32 out_color) =>
            {
                out_color.a = 0;
            }
        });
        GeoRegion = add(new CustomMapModeAsset()
        {
            id = "geo_region",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_layer",
            redirect_map_mode = MetaTypeExtend.GeoRegion,
            geo_region_layers = new[] { GeoRegionLayer.Primary },
            kernel_func = (WorldTile worldTile, ref Color32 out_color) =>
            {
                RenderGeoRegionLayer(worldTile, GeoRegionLayer.Primary, ref out_color);
            }
        });

        GeoRegionLandform = add(new CustomMapModeAsset()
        {
            id = "geo_region_landform",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_landform_layer",
            redirect_map_mode = MetaTypeExtend.GeoRegion,
            geo_region_layers = new[] { GeoRegionLayer.Landform },
            kernel_func = (WorldTile worldTile, ref Color32 out_color) =>
            {
                RenderGeoRegionLayer(worldTile, GeoRegionLayer.Landform, ref out_color);
            }
        });

        GeoRegionLandmass = add(new CustomMapModeAsset()
        {
            id = "geo_region_landmass",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_landmass_layer",
            redirect_map_mode = MetaTypeExtend.GeoRegion,
            geo_region_layers = new[] { GeoRegionLayer.Landmass },
            kernel_func = (WorldTile worldTile, ref Color32 out_color) =>
            {
                RenderGeoRegionLayer(worldTile, GeoRegionLayer.Landmass, ref out_color);
            }
        });

        GeoRegionMorphology = add(new CustomMapModeAsset()
        {
            id = "geo_region_morphology",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_morphology_layer",
            redirect_map_mode = MetaTypeExtend.GeoRegion,
            geo_region_layers = new[] { GeoRegionLayer.Strait, GeoRegionLayer.Peninsula, GeoRegionLayer.Archipelago },
            kernel_func = (WorldTile worldTile, ref Color32 out_color) =>
            {
                RenderGeoRegionMorphology(worldTile, ref out_color);
            }
        });
    }

    private static void RenderGeoRegionLayer(WorldTile worldTile, GeoRegionLayer layer, ref Color32 outColor)
    {
        if (ModClass.I.CustomMapModeManager.TryGetForcedInteractionRegion(worldTile, out GeoRegion forcedRegion))
        {
            RenderGeoRegion(worldTile, forcedRegion, ref outColor);
            return;
        }

        if (!TryGetTileExtend(worldTile, out TileExtend tile))
        {
            outColor.a = 0;
            return;
        }

        GeoRegion region = tile.GetGeoRegion(layer);
        RenderGeoRegion(worldTile, region, ref outColor);
    }

    private static void RenderGeoRegionMorphology(WorldTile worldTile, ref Color32 outColor)
    {
        if (ModClass.I.CustomMapModeManager.TryGetForcedInteractionRegion(worldTile, out GeoRegion forcedRegion))
        {
            RenderGeoRegion(worldTile, forcedRegion, ref outColor);
            return;
        }

        if (!TryGetTileExtend(worldTile, out TileExtend tile))
        {
            outColor.a = 0;
            return;
        }

        GeoRegion region = tile.GetGeoRegion(GeoRegionLayer.Strait) ??
                           tile.GetGeoRegion(GeoRegionLayer.Peninsula) ??
                           tile.GetGeoRegion(GeoRegionLayer.Archipelago);
        RenderGeoRegion(worldTile, region, ref outColor);
    }

    private static void RenderGeoRegion(WorldTile worldTile, GeoRegion region, ref Color32 outColor)
    {
        if (region == null)
        {
            outColor.a = 0;
            return;
        }

        outColor = region.getColor().getColorMain32();
        ModClass.I.CustomMapModeManager.ApplyGeoRegionInteractionColor(region, worldTile, ref outColor);
    }

    private static bool TryGetTileExtend(WorldTile worldTile, out TileExtend tileExtend)
    {
        tileExtend = null;
        if (worldTile == null) return false;
        if (ModClass.I?.TileExtendManager == null || !ModClass.I.TileExtendManager.Ready()) return false;

        int tileId = worldTile.data.tile_id;
        if (tileId < 0 || tileId >= World.world.tiles_list.Length) return false;

        tileExtend = ModClass.I.TileExtendManager.Get(tileId);
        return tileExtend != null;
    }

    public override CustomMapModeAsset add(CustomMapModeAsset pAsset)
    {
        GodPower power = new GodPower()
        {
            id = pAsset.toggle_name,
            name = pAsset.toggle_name,
            unselect_when_window = true,
            map_modes_switch = true,
            toggle_name = pAsset.toggle_name,
            force_map_mode = pAsset.redirect_map_mode.Back(),
            toggle_action = _ => ModClass.I.CustomMapModeManager.InvalidateCurrentMapMode()
        };
        AssetManager.powers.add(power);
        UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateToggleButton(pAsset.toggle_name, SpriteTextureLoader.getSprite(pAsset.icon_path)));
        return base.add(pAsset);
    }
}
