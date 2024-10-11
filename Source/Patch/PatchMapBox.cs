using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchMapBox
{
    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void finishMakingWorld_postfix()
    {
        ModClass.I.TileExtendManager.FitNewWorld();
    }
}