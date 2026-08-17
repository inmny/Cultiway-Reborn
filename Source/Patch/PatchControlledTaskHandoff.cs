using Cultiway.Core.ControlledTasks;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>任务交接期间只抑制目标角色的原版失控退出效果。</summary>
internal static class PatchControlledTaskHandoff
{
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.applyRandomForce))]
    private static bool applyRandomForce_prefix(Actor __instance)
    {
        return !ControlledTaskHandoffScope.SuppressesReleaseEffect(__instance);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.makeStunned))]
    private static bool makeStunned_prefix(Actor __instance)
    {
        return !ControlledTaskHandoffScope.SuppressesReleaseEffect(__instance);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.makeConfused))]
    private static bool makeConfused_prefix(Actor __instance)
    {
        return !ControlledTaskHandoffScope.SuppressesReleaseEffect(__instance);
    }
}
