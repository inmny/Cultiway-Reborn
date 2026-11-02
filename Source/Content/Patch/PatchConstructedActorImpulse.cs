using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>让新构造受力共用世界时间，避免原版下落与倍速受力重复推进。</summary>
internal static class PatchConstructedActorImpulse
{
    /// <summary>已受理的构造受力由共同轨迹推进。</summary>
    /// <param name="__instance">当前真实人物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), "updateVelocity")]
    private static bool Move(Actor __instance) => !ConstructedActorImpulse.Update(__instance);
}