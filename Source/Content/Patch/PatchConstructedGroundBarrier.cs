using Cultiway.Content.Skills;
using SkillManager = Cultiway.Core.SkillLibV3.Manager;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>在原版人物实际走动及受力入口阻止穿墙，首轮也无需等待位置历史。</summary>
internal static class PatchConstructedGroundBarrier
{
    /// <summary>原版正常步行的每段真实起点。</summary>
    /// <param name="__instance">移动人物。</param><param name="__state">本段起点。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), "updateMovement")]
    private static void Begin(Actor __instance, out Vector2 __state) => __state = __instance.current_position;

    /// <summary>原版移动当段结束后立即恢复合法位置，并结束穿墙路径。</summary>
    /// <param name="__instance">移动人物。</param><param name="__state">本段起点。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), "updateMovement")]
    private static void End(Actor __instance, Vector2 __state)
    {
        if (SkillManager.Barriers?.HasGroundBarriers != true) return;
        Vector2 allowed = SkillManager.Barriers.RestrictGroundActor(__instance, __state, __instance.current_position);
        if (allowed == __instance.current_position) return;
        __instance.current_position = allowed;
        __instance.dirty_current_tile = true;
        __instance.stopMovement();
    }

    /// <summary>普通受力和附身移动尚未写回坐标前共用屏障边界。</summary>
    /// <param name="__instance">移动人物。</param><param name="__result">原版已通过地形检查的候选位置。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), "checkVelocityAgainstBlock")]
    private static void Force(Actor __instance, ref Vector2 __result)
    {
        if (!ConstructedActorImpulse.CheckingTerrain && SkillManager.Barriers?.HasGroundBarriers == true)
            __result = SkillManager.Barriers.RestrictGroundActor(__instance, __instance.current_position, __result);
    }
}
