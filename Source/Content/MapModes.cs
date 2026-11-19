using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.MapModeVisuals;
using Cultiway.Content.Territories;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public class MapModes : ExtendLibrary<CustomMapModeAsset, MapModes>
{
    public static CustomMapModeAsset Wakan { get; private set; }
    public static CustomMapModeAsset DirtyWakan { get; private set; }
    public static CustomMapModeAsset StrategicKingdom { get; private set; }
    public static CustomMapModeAsset SpiritVein { get; private set; }
    /// <summary>选中车站的铁路运营显示。</summary>
    public static CustomMapModeAsset Railway { get; private set; }
    /// <summary>玩家绘制的地区边界、名称与当前选区。</summary>
    public static CustomMapModeAsset Territories { get; private set; }

    protected override bool AutoRegisterAssets() => false;

    protected override void OnInit()    
    {
        Territories = Add(new CustomMapModeAsset
        {
            id = "custom_territories", icon_path = "ui/icons/iconCityZones",
            toggle_name = TerritoryMapOverlay.ToggleName, kernel_func = TerritoryMapOverlay.Render
        });
        Railway = Add(new CustomMapModeAsset
        {
            id = "railway", icon_path = "cultiway/icons/plots/iconTrainNet", toggle_name = "cultiway_railway_layer",
            kernel_func = RailwayMapOverlay.Render
        });
        Wakan = Add(new CustomMapModeAsset()
        {
            id = nameof(Wakan),
            icon_path = "cultiway/icons/iconWakan",
            toggle_name = "wakan_layer",
            kernel_func = [Hotfixable](WorldTile tile, ref Color32 out_color) =>
            {
                out_color = ResolveCleanWakanColor(tile.data.tile_id);
            }
        });
        DirtyWakan = Add(new CustomMapModeAsset()
        {
            id = nameof(DirtyWakan),
            icon_path = "cultiway/icons/iconWakan",
            toggle_name = "dirty_wakan_layer",
            kernel_func = [Hotfixable](WorldTile tile, ref Color32 out_color) =>
            {
                int tileId = tile.data.tile_id;
                var v = Mathf.Log10(WorldWakanService.GetDisplayDirty(tileId) + 1f);
                var p = 1 / (1 + Mathf.Exp(4f - v));
                out_color.r = (byte)(127 * (1-p));
                out_color.g = (byte)(127 * (1-p));
                out_color.b = (byte)(127 * (1-p));
                out_color.a = byte.MaxValue;
            }
        });
        SpiritVein = Add(new CustomMapModeAsset
        {
            id = "spirit_vein",
            icon_path = "cultiway/icons/iconSpiritVein",
            toggle_name = "spirit_vein_layer",
            redirect_map_mode = MetaTypeExtend.SpiritVein,
            renderer_factory = manager => new SpiritVeinMapRenderer(manager),
            uses_meta_layer_button = true,
            default_int = (int)SpiritVeinMapView.SpiritVeins,
            max_value = (int)SpiritVeinMapView.Overlay,
            locale_options_ids = new[]
            {
                "Cultiway.SpiritVein.MapMode.SpiritVeins",
                "Cultiway.SpiritVein.MapMode.Wakan",
                "Cultiway.SpiritVein.MapMode.Overlay"
            }
        });
        StrategicKingdom = Add(new CustomMapModeAsset
        {
            id = nameof(StrategicKingdom),
            icon_path = "ui/icons/iconKingdomZones",
            toggle_name = "cultiway_strategic_kingdom_layer",
            redirect_map_mode = MetaTypeExtend.Kingdom,
            renderer_factory = manager => new KingdomMapRenderer(manager)
        });
    }

    internal static SpiritVeinMapView GetSpiritVeinView()
    {
        OptionAsset option = SpiritVein == null
            ? null
            : AssetManager.options_library.get(SpiritVein.toggle_name);
        int value = option?.data?.intVal ?? (int)SpiritVeinMapView.SpiritVeins;
        return (SpiritVeinMapView)Mathf.Clamp(
            value,
            (int)SpiritVeinMapView.SpiritVeins,
            (int)SpiritVeinMapView.Overlay);
    }

    internal static Color32 ResolveCleanWakanColor(int tileId)
    {
        float value = Mathf.Log10(WorldWakanService.GetDisplayClean(tileId) + 1f);
        float weight = 1f / (1f + Mathf.Exp(4f - value));
        return new Color32(
            (byte)(97f + (255f - 97f) * weight),
            (byte)(181f * (1f - weight)),
            byte.MaxValue,
            byte.MaxValue);
    }

    protected override void PostInit(CustomMapModeAsset asset)
    {
        base.PostInit(asset);
        if (asset == SpiritVein)
        {
            WorldboxGame.MetaTypes.SpiritVein.option_asset = AssetManager.options_library.get(asset.toggle_name);
        }
    }
}
