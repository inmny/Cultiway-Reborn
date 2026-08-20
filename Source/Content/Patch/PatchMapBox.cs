using HarmonyLib;

namespace Cultiway.Content.Patch;

internal static class PatchMapBox
{
    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.setMapSize))]
    private static void setMapSize_postfix()
    {
        WorldWakanService.InitializeWorld(MapBox.width, MapBox.height);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
    private static void clearWorld_prefix()
    {
        WorldWakanService.ClearWorld();
    }
}
