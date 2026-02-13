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
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                out_color.a = 0;
            }
        });
        GeoRegion = add(new CustomMapModeAsset()
        {
            id = "geo_region",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_layer",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var region = tile.GetGeoRegion();
                if (region != null)
                {
                    out_color = region.getColor().getColorMain32();
                    return;
                }
                out_color.a = 0;
            }
        });

        GeoRegionLandform = add(new CustomMapModeAsset()
        {
            id = "geo_region_landform",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_landform_layer",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var region = tile.GetGeoRegion(GeoRegionLayer.Landform);
                if (region != null)
                {
                    out_color = region.getColor().getColorMain32();
                    return;
                }
                out_color.a = 0;
            }
        });

        GeoRegionLandmass = add(new CustomMapModeAsset()
        {
            id = "geo_region_landmass",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_landmass_layer",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var region = tile.GetGeoRegion(GeoRegionLayer.Landmass);
                if (region != null)
                {
                    out_color = region.getColor().getColorMain32();
                    return;
                }
                out_color.a = 0;
            }
        });

        GeoRegionMorphology = add(new CustomMapModeAsset()
        {
            id = "geo_region_morphology",
            icon_path = "cultiway/icons/iconGeoRegion",
            toggle_name = "geo_region_morphology_layer",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var region = tile.GetGeoRegion(GeoRegionLayer.Strait) ??
                             tile.GetGeoRegion(GeoRegionLayer.Peninsula) ??
                             tile.GetGeoRegion(GeoRegionLayer.Archipelago);
                if (region != null)
                {
                    out_color = region.getColor().getColorMain32();
                    return;
                }
                out_color.a = 0;
            }
        });
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
            toggle_action = _ => ModClass.I.CustomMapModeManager.SetAllDirty()
        };
        AssetManager.powers.add(power);
        UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateToggleButton(pAsset.toggle_name, SpriteTextureLoader.getSprite(pAsset.icon_path)));
        return base.add(pAsset);
    }
}
