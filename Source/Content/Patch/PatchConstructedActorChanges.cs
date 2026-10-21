using Cultiway.Content.Skills;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>将接触回调内已经确认的原版人物变化交给共同作用筛选。</summary>
internal static class PatchConstructedActorChanges
{
    /// <summary>位置实际改变后通知，普通摇晃不会制造新的物理路径。</summary>
    /// <param name="__instance">已经更新的人物。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.updatePos))]
    private static void Position(Actor __instance) => ConstructedAbilityService.ObserveActorChange(__instance, false);

    /// <summary>尺寸实际改变后更新身体和空间范围。</summary>
    /// <param name="__instance">已经改变尺寸的人物。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.setActorScale))]
    private static void Scale(Actor __instance) => ConstructedAbilityService.ObserveActorChange(__instance, false);

    /// <summary>体质变化可改变身体条件，重新采集实际结果。</summary>
    /// <param name="__instance">已经完成体质更新的人物。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.updateStats))]
    private static void Stats(Actor __instance) => ConstructedAbilityService.ObserveActorChange(__instance, true);

    /// <summary>回调中新生人物立即参加余下共同时间。</summary>
    /// <param name="__result">原版已经登记的新生人物。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.createNewUnit))]
    private static void Created(Actor __result) => ConstructedAbilityService.ObserveActorChange(__result, true);
}
