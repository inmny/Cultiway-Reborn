using System;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Debug;

internal static class FinalizerPatch
{
    [Hotfixable]
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Actor), nameof(Actor.checkCalibrateTargetPosition))]
    private static Exception Finalizer(Exception __exception, Actor __instance)
    {
        if (__exception != null)
        {
            ModClass.LogInfo($"{__instance.name}({__instance.id}): {__instance.tile_target==null}");
        }

        return __exception;
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.addBuilding), [typeof(BuildingAsset), typeof(WorldTile), typeof(bool), typeof(bool), typeof(BuildPlacingType)])]
    private static Exception BuildingManager_addBuilding_Finalizer(Exception __exception, BuildingAsset pAsset)
    {
        if (__exception != null)
        {
            ModClass.LogInfo($"BuildingManager_addBuilding_Finalizer: {pAsset.id}");
        }
        return __exception;
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(Book), nameof(Book.newBook))]
    private static Exception Book_newBook_Finalizer(Exception __exception, Book __instance, Actor pByActor)
    {
        if (__exception != null)
        {
            ModClass.LogInfo($"language==null? {pByActor.language == null}");
        }

        return __exception;
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(PowerTabController), nameof(PowerTabController.showWorldTipSelected))]
    private static Exception PowerTabController_showWorldTipSelected(Exception __exception, string pPowerTabId, bool pBar)
    {
        if (__exception != null)
        {
            var current_meta = SelectedObjects.getSelectedNanoObject();
            var asset = AssetManager.power_tab_library.get(pPowerTabId);
            ModClass.LogInfo($"PowerTabController_showWorldTipSelected: {current_meta==null} {asset==null}({pPowerTabId}) {asset.get_localized_worldtip==null}");
        }
        return __exception;
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(TabHistoryData), nameof(TabHistoryData.getNanoObject))]
    private static Exception TabHistoryData_getNanoObject(Exception __exception, TabHistoryData __instance)
    {
        if (__exception != null)
        {
            ModClass.LogInfo($"TabHistoryData_getNanoObject: {__instance.id}, {__instance.meta_type}");
        }
        return __exception;
    }
}