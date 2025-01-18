using ai.behaviours;
using HarmonyLib;

namespace Cultiway.Content.Patch;

internal static class PatchAboutConstraintSpirit
{
    [HarmonyPostfix, HarmonyPatch(typeof(CityBehProduceUnit), nameof(CityBehProduceUnit.findPossibleParents))]
    private static void CityBehProduceUnit_findPossibleParents_postfix(CityBehProduceUnit __instance)
    {
        __instance._possibleParents.RemoveAll(x => x.asset == Actors.ConstraintSpirit);
    }
}