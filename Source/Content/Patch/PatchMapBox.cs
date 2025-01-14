using HarmonyLib;

namespace Cultiway.Content.Patch;

internal static class PatchMapBox
{
    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.setMapSize))]
    private static void setMapSize_postfix()
    {
        WakanMap.I.Resize(MapBox.width, MapBox.height);
        DirtyWakanMap.I.Resize(MapBox.width, MapBox.height);
    }
}