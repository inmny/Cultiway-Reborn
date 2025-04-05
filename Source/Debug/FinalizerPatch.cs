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
}