using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>选择性给原版麦田的更新换算有效生长时间。</summary>
internal static class PatchCropGrowth
{
    [HarmonyPrefix, HarmonyPatch(typeof(Wheat), nameof(Wheat.update))]
    private static bool Grow(Wheat __instance, ref float pElapsed) =>
        CropGrowthPacing.Advance(__instance.building, ref pElapsed);

    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.startRemove))]
    private static void Remove(Building __instance) => CropGrowthPacing.Forget(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.makeRuins))]
    private static void Ruin(Building __instance) => CropGrowthPacing.Forget(__instance);
}
