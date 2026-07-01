using Cultiway.Const;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class CustomMapModeAsset : Asset
{
    public delegate void Kernel(WorldTile tile, ref Color32 out_color);

    public string icon_path;

    public Kernel kernel_func = (WorldTile tile, ref Color32 out_color) => { out_color.a = 0; };
    public GeoRegionLayer[] geo_region_layers;

    public string toggle_name;
    public MetaTypeExtend redirect_map_mode = MetaTypeExtend.None;
    public int default_int;
    public int max_value;
    public string[] locale_options_ids;
    public bool uses_meta_layer_button;

    public bool ContainsGeoRegionLayer(GeoRegionLayer layer)
    {
        if (geo_region_layers == null) return false;
        for (int i = 0; i < geo_region_layers.Length; i++)
        {
            if (geo_region_layers[i] == layer) return true;
        }

        return false;
    }
}
