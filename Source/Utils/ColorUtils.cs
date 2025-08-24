using UnityEngine;

namespace Cultiway.Utils;

public static class ColorUtils
{
    public static bool IsSameWith(this Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}