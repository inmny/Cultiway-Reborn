using System.Collections.Generic;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchSimObjectsZones
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SimObjectsZones), "recalc")]
    private static bool recalc_prefix(
        ref bool ____buildings_dirty,
        HashSet<MapChunk> ____dirty_building_chunks,
        List<WorldTile> ____to_clear_tiles)
    {
        bool handled =
            IncrementalSimObjectZoneUnits
            .TryRecalculate(
                ____buildings_dirty,
                ____dirty_building_chunks,
                ____to_clear_tiles);
        if (handled)
        {
            ____buildings_dirty = false;
        }

        return !handled;
    }

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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SimObjectsZones), nameof(SimObjectsZones.fullClear))]
    private static void fullClear_prefix()
    {
        IncrementalSimObjectZoneUnits.Invalidate();
    }
}
