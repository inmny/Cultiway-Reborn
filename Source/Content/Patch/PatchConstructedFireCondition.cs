using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>接入原版地块的新点火，已有火焰仍由原版运行及结束。</summary>
internal static class PatchConstructedFireCondition
{
    [HarmonyPrefix, HarmonyPatch(typeof(WorldTile), nameof(WorldTile.startFire))]
    private static bool StartFire(WorldTile __instance, ref bool __result)
    {
        if (__instance.isOnFire() || !ConstructedAbilityService.PreventsNewFire(__instance)) return true;
        __result = false;
        return false;
    }
}
