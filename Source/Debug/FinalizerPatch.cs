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
            ModClass.LogInfo($"{__instance.tile_target==null}");
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