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
            case $"Cultiway.{nameof(WorldboxGame.HistoryMetaDatas.GeoRegion)}":
                __result = __instance.GetAnyExtend<MapStats, MapStatsExtend>().IdGeoRegion++;
                return false;
        }

        return true;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(MapStats), nameof(MapStats.formatId))]
    private static bool formatId_prefix(MapStats __instance, string pType, long pID, ref string __result)
    {
        switch (pType)
        {
            case $"Cultiway.{nameof(WorldboxGame.HistoryMetaDatas.Sect)}":
                __result = $"sect_{pID}";
                return false;
            case $"Cultiway.{nameof(WorldboxGame.HistoryMetaDatas.GeoRegion)}":
                __result = $"geo_region_{pID}";
                return false;
        }
        return true;
    }
}