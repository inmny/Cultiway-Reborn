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
}