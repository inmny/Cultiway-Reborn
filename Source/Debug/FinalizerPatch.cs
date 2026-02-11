using System;
using System.Collections.Generic;
using System.Reflection;
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
    [HarmonyFinalizer, HarmonyPatch(typeof(MultiBannerPool), nameof(MultiBannerPool.getNext))]
    private static Exception MultiBannerPool_getNext(Exception __exception, MultiBannerPool __instance, NanoObject pObject)
    {
        if (__exception != null)
        {
            var meta_asset = AssetManager.meta_customization_library.get(pObject.getType());
            ModClass.LogInfo($"MultiBannerPool_getNext: {__instance._pool_banners == null}, {meta_asset==null}, {meta_asset?.get_banner==null}");
        }
        return __exception;
    }
    
    [HarmonyPatch]
    public static class WindowMetaElementGenericPatch
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var type_base = typeof(WindowMetaElement<,>);
            var assembly = type_base.Assembly;
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract) continue;
                var base_type = type.BaseType;
                while (base_type != null)
                {
                    if (base_type.IsGenericType && base_type.GetGenericTypeDefinition() == type_base)
                    {
                        var method = AccessTools.Method(type, "OnEnable");
                        if (method == null) method = AccessTools.Method(base_type, "OnEnable");
                        if (method != null) yield return method;
                        break;
                    }
                    base_type = base_type.BaseType;
                }
            }
        }

        [HarmonyFinalizer]
        public static Exception Finalizer(Exception __exception, WindowMetaElementBase __instance)
        {
            if (__exception != null)
            {
                ModClass.LogInfo($"WindowMetaElement_OnEnable: {__instance?.name ?? "null"}");
            }
            return null;
        }
    }
}