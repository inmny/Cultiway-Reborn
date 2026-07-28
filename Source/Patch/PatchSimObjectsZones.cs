using System.Collections.Generic;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchSimObjectsZones
{
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
}
