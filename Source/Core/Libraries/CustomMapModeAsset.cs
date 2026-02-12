using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class CustomMapModeAsset : Asset
{
    public delegate void Kernel(int x, int y, ref Color32 out_color);

    public string icon_path;

    public Kernel kernel_func = (int x, int y, ref Color32 out_color) => { out_color.a = 0; };

    public string toggle_name;
}