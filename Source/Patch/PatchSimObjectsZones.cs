using System.Collections.Generic;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchSimObjectsZones
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SimObjectsZones), "clearTileUnits")]
    private static bool clearTileUnits_prefix(
        List<WorldTile> ____to_clear_tiles)
    {
        return !ParallelSimObjectZoneUnits
            .TryClearTileUnits(
                ____to_clear_tiles);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SimObjectsZones), "clearChunkObjects")]
    private static bool clearChunkObjects_prefix(
        bool pForceClearBuildings)
    {
        return !ParallelSimObjectZoneUnits
            .TryClearChunkObjects(
                pForceClearBuildings);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(IslandsCalculator), "recalcActors")]
    private static bool recalcActors_prefix(
        IslandsCalculator __instance)
    {
        return !ParallelSimObjectZoneUnits
            .TryDeferIslandRebuild(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SimObjectsZones), "checkUnits")]
    private static bool checkUnits_prefix(
        List<WorldTile> ____to_clear_tiles)
    {
        return !ParallelSimObjectZoneUnits.TryRebuild(
            ____to_clear_tiles);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SimObjectsZones), "checkUnits")]
    private static void checkUnits_postfix()
    {
        ParallelSimObjectZoneUnits
            .NotifyUnitMembershipRebuilt();
    }
}
