using Cultiway.Content.Skills;
using SkillManager = Cultiway.Core.SkillLibV3.Manager;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>在原版受力和附身位移写回前应用真实屏障。</summary>
internal static class PatchConstructedGroundBarrier
{
    /// <summary>普通受力和附身移动尚未写回坐标前共用屏障边界。</summary>
    /// <param name="__instance">移动人物。</param><param name="__result">原版已通过地形检查的候选位置。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), "checkVelocityAgainstBlock")]
    private static void Force(Actor __instance, ref Vector2 __result)
    {
        if (!ConstructedActorImpulse.CheckingTerrain && SkillManager.Barriers?.HasGroundBarriers == true)
            __result = SkillManager.Barriers.RestrictGroundActor(__instance, __instance.current_position, __result);
    }
}
