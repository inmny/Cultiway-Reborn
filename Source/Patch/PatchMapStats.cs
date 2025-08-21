using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchMapStats
{
    [HarmonyPrefix, HarmonyPatch(typeof(MapStats), nameof(MapStats.getNextId))]
    private static bool getNextId_prefix(MapStats __instance, string pType, ref long __result)
    {
        switch (pType)
        {
            case $"Cultiway.{nameof(WorldboxGame.HistoryMetaDatas.Sect)}":
                __result = __instance.GetAnyExtend<MapStats, MapStatsExtend>().IdSect++;
                return false;
        }

        return true;
    }
}