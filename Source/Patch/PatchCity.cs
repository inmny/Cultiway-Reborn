using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchCity
{
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.Dispose))]
    private static void Dispose_prefix(City __instance)
    {
        var ce = __instance.GetExtend();
        ce.Dispose();
    }
}