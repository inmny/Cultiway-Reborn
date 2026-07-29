using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>
/// 保留原版 beginChecksUnits 的清理与结束流程，
/// 仅将各管理器重复的万人扫描替换为融合后的紧凑成员表。
/// </summary>
internal static class PatchDirtyMetaActorIndex
{
    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(SubspeciesManager),
        "updateDirtyUnits")]
    private static bool updateSubspeciesUnits(
        SubspeciesManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(FamilyManager),
        "updateDirtyUnits")]
    private static bool updateFamilyUnits(
        FamilyManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ArmyManager),
        "updateDirtyUnits")]
    private static bool updateArmyUnits(
        ArmyManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(LanguageManager),
        "updateDirtyUnits")]
    private static bool updateLanguageUnits(
        LanguageManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ReligionManager),
        "updateDirtyUnits")]
    private static bool updateReligionUnits(
        ReligionManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(CityManager),
        "updateDirtyUnits")]
    private static bool updateCityUnits(
        CityManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ClanManager),
        "updateDirtyUnits")]
    private static bool updateClanUnits(
        ClanManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(KingdomManager),
        "updateDirtyUnits")]
    private static bool updateKingdomUnits(
        KingdomManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(WildKingdomsManager),
        "updateDirtyUnits")]
    private static bool updateWildKingdomUnits(
        WildKingdomsManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(CultureManager),
        "updateDirtyUnits")]
    private static bool updateCultureUnits(
        CultureManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(PlotManager),
        "updateDirtyUnits")]
    private static bool updatePlotUnits(
        PlotManager __instance)
    {
        return !DirtyMetaActorIndex.TryApply(
            __instance);
    }
}
