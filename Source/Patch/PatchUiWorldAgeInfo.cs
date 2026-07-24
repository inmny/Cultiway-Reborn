using Cultiway.UI;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchUiWorldAgeInfo
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UiWorldAgeInfo), "Awake")]
    private static void AttachWorldTimeTooltip(UiWorldAgeInfo __instance)
    {
        WorldAgeClockTooltipAdapter.Attach(__instance);
    }

    public static void SpecialPatch()
    {
        UiWorldAgeInfo worldAgeInfo =
            Object.FindFirstObjectByType<UiWorldAgeInfo>(FindObjectsInactive.Include);
        if (worldAgeInfo != null)
        {
            WorldAgeClockTooltipAdapter.Attach(worldAgeInfo);
        }
    }
}
