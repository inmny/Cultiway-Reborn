using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public class MapModes : ExtendLibrary<CustomMapModeAsset, MapModes>
{
    public static CustomMapModeAsset Wakan { get; private set; }
    public static CustomMapModeAsset DirtyWakan { get; private set; }

    protected override bool AutoRegisterAssets() => false;

    protected override void OnInit()    
    {
        Wakan = Add(new CustomMapModeAsset()
        {
            id = nameof(Wakan),
            icon_path = "cultiway/icons/iconWakan",
            kernel_func = [Hotfixable](int x, int y, ref Color32 out_color) =>
            {
                var v = Mathf.Log10(WakanMap.I.map[x, y]);
                var p = 1 / (1 + Mathf.Exp(4f - v));
                out_color.r = (byte)(97  + (255 - 97)  * p); // 97->255
                out_color.g = (byte)(181 + (0   - 181) * p); // 181->0
                out_color.b = byte.MaxValue;                 // 255->255
                out_color.a = byte.MaxValue;
            }
        });
        DirtyWakan = Add(new CustomMapModeAsset()
        {
            id = nameof(DirtyWakan),
            icon_path = "cultiway/icons/iconWakan",
            kernel_func = [Hotfixable](int x, int y, ref Color32 out_color) =>
            {
                var v = Mathf.Log10(DirtyWakanMap.I.map[x, y]);
                var p = 1 / (1 + Mathf.Exp(4f - v));
                out_color.r = (byte)(127 * (1-p));
                out_color.g = (byte)(127 * (1-p));
                out_color.b = (byte)(127 * (1-p));
                out_color.a = byte.MaxValue;
            }
        });
    }
}