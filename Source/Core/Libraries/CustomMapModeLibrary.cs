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
    private const string ToggleButtonPrefab = "map_kings_leaders";
    private const string TwoModeToggleButtonPrefab = "city_layer";
    private const string ThreeModeToggleButtonPrefab = "subspecies_layer";

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
            icon_path = "cultiway/icons/iconSect",
            toggle_name = "sect_layer",
            redirect_map_mode = MetaTypeExtend.Sect,
            uses_meta_layer_button = true,
            default_int = 0,
            max_value = 1,
            locale_options_ids = new[]
            {
                "Cultiway.Sect.MapMode.Residence",
                "Cultiway.Sect.MapMode.Members"
            },
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
            uses_meta_layer_button = true,
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
        tileExtend = default;
        if (worldTile == null) return false;
        if (ModClass.I?.TileExtendManager == null || !ModClass.I.TileExtendManager.Ready()) return false;

        int tileId = worldTile.data.tile_id;
        if (tileId < 0 || tileId >= World.world.tiles_list.Length) return false;

        tileExtend = ModClass.I.TileExtendManager.Get(tileId);
        return true;
    }

    public override CustomMapModeAsset add(CustomMapModeAsset pAsset)
    {
        EnsureMapModeOption(pAsset);
        CustomMapModeAsset asset = base.add(pAsset);
        GodPower power = new GodPower()
        {
            id = pAsset.toggle_name,
            name = pAsset.toggle_name,
            unselect_when_window = true,
            map_modes_switch = true,
            multi_toggle = pAsset.max_value > 0,
            toggle_name = pAsset.toggle_name,
            force_map_mode = pAsset.redirect_map_mode.Back()
        };
        power.toggle_action = pAsset.max_value > 0
            ? _ => ToggleMapModeZoneOption(asset, power)
            : _ => ToggleMapModeOption(asset, power);
        AssetManager.powers.add(power);
        if (!pAsset.uses_meta_layer_button)
        {
            UI.Manager.AddButton(TabButtonType.WORLD, CreateMapModeButton(pAsset));
        }

        return asset;
    }

    private static PowerButton CreateMapModeButton(CustomMapModeAsset asset)
    {
        PowerButton prefab = ResourcesFinder.FindResource<PowerButton>(GetMapModeButtonPrefabName(asset));
        bool wasActive = prefab.gameObject.activeSelf;
        if (wasActive)
        {
            prefab.gameObject.SetActive(false);
        }

        PowerButton button = UnityEngine.Object.Instantiate(prefab);
        if (wasActive)
        {
            prefab.gameObject.SetActive(true);
        }

        Sprite icon = SpriteTextureLoader.getSprite(asset.icon_path);
        button.name = asset.toggle_name;
        button.icon.sprite = icon;
        button.icon.overrideSprite = icon;
        button.open_window_id = null;
        button.type = PowerButtonType.Special;
        button.transform.localScale = Vector3.one;
        button.gameObject.SetActive(true);
        button.checkToggleIcon();
        return button;
    }

    public static string GetMapModeButtonPrefabName(CustomMapModeAsset asset)
    {
        if (asset.max_value <= 0) return ToggleButtonPrefab;
        return asset.max_value == 1 ? TwoModeToggleButtonPrefab : ThreeModeToggleButtonPrefab;
    }

    private static void ToggleMapModeZoneOption(CustomMapModeAsset asset, GodPower power)
    {
        OptionAsset option = AssetManager.options_library.get(asset.toggle_name);
        PlayerOptionData data = option.data;
        int direction = InputHelpers.GetMouseButtonUp(1) ? -1 : 1;
        if (data.boolVal)
        {
            data.intVal += direction;
            if (data.intVal > option.max_value)
            {
                data.intVal = 0;
                data.boolVal = false;
            }

            if (data.intVal < 0)
            {
                data.intVal = option.max_value;
            }
        }
        else
        {
            data.boolVal = true;
        }

        FinishMapModeToggle(asset, power, option, showOption: true);
    }

    private static void ToggleMapModeOption(CustomMapModeAsset asset, GodPower power)
    {
        OptionAsset option = AssetManager.options_library.get(asset.toggle_name);
        PlayerOptionData data = option.data;
        data.boolVal = !data.boolVal;
        FinishMapModeToggle(asset, power, option, showOption: false);
    }

    private static void FinishMapModeToggle(CustomMapModeAsset asset, GodPower power, OptionAsset option, bool showOption)
    {
        PlayerOptionData data = option.data;
        if (data.boolVal)
        {
            DisableAllOtherMapModes(power.id);
            string title = power.getTranslatedName();
            if (showOption)
            {
                title += " - " + option.getTranslatedOption();
            }

            WorldTip.instance.showToolbarText(title, power.getTranslatedDescription());
        }
        else
        {
            WorldTip.instance.startHide();
        }

        PlayerConfig.saveData();
        ModClass.I.CustomMapModeManager.InvalidateCurrentMapMode();
        if (asset.redirect_map_mode == MetaTypeExtend.Sect)
        {
            ZoneMetaDataVisualizer.clearAll();
        }
    }

    private static void DisableAllOtherMapModes(string activePowerId)
    {
        for (int i = 0; i < AssetManager.powers.list.Count; i++)
        {
            GodPower power = AssetManager.powers.list[i];
            if (!power.map_modes_switch || power.id == activePowerId) continue;
            PlayerConfig.dict[power.toggle_name].boolVal = false;
        }
    }

    private static void EnsureMapModeOption(CustomMapModeAsset asset)
    {
        if (AssetManager.options_library.get(asset.toggle_name) == null)
        {
            AssetManager.options_library.add(new OptionAsset
            {
                id = asset.toggle_name,
                default_int = asset.default_int,
                max_value = asset.max_value,
                multi_toggle = asset.max_value > 0,
                type = OptionType.Bool,
                locale_options_ids = asset.locale_options_ids
            });
        }

        if (PlayerConfig.dict != null && !PlayerConfig.dict.ContainsKey(asset.toggle_name))
        {
            PlayerConfig.dict.Add(asset.toggle_name, new PlayerOptionData(asset.toggle_name)
            {
                boolVal = false,
                intVal = asset.default_int
            });
        }
    }
}
