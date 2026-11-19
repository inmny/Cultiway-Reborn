using Cultiway.Content.VillageDuties;
using Cultiway.Content.KingdomPolicies;
using HarmonyLib;
using ai.behaviours;

namespace Cultiway.Content.Patch;

/// <summary>正常睡眠回本人住房，救助政策允许无房居民借宿空房。</summary>
internal static class PatchVillageSleep
{
    /// <summary>睡觉决策不再要求人物已经处于本城边界内。</summary>
    /// <param name="__instance">原版睡觉地点选择。</param>
    /// <param name="pObject">准备睡觉的居民。</param>
    /// <param name="__result">切换到原版回家睡眠任务的结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehDecideWhereToSleep), nameof(BehDecideWhereToSleep.execute))]
    private static bool ChooseHome(BehDecideWhereToSleep __instance, Actor pObject, ref BehResult __result)
    {
        if (!VillageDutyService.IsResident(pObject) || pObject.is_inside_boat || pObject.isFighting() ||
            VillageHomeService.GetSleepHome(pObject) == null) return true;
        __result = __instance.forceTask(pObject, "sleep_inside", pClean: true, pForceAction: true); return false;
    }

    /// <summary>只在睡眠任务中把无房者引向救助空房，其他回家任务不变。</summary>
    /// <param name="__instance">原版住房目标选择。</param>
    /// <param name="pActor">准备返回睡眠地点的居民。</param>
    /// <param name="__result">继续走向空房或转去露宿。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehBuildingTargetHome), nameof(BehBuildingTargetHome.execute))]
    private static bool SelectShelter(BehBuildingTargetHome __instance, Actor pActor, ref BehResult __result)
    {
        if (!VillageDutyService.IsResident(pActor) || pActor.ai.task?.id != "sleep_inside" || pActor.hasHouse()) return true;
        var shelter = VillageHomeService.GetSleepHome(pActor);
        if (shelter == null) __result = __instance.forceTask(pActor, "sleep_outside", pClean: true, pForceAction: true);
        else { pActor.beh_building_target = shelter; __result = BehResult.Continue; }
        return false;
    }

    /// <summary>到达时重新检查住房，途中被占用或政策关闭时重新选择睡眠地点。</summary>
    /// <param name="__instance">原版进入目标建筑。</param>
    /// <param name="pActor">刚到达睡眠地点的居民。</param>
    /// <param name="__result">进入房屋或重新选择睡眠任务。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehStayInBuildingTarget), nameof(BehStayInBuildingTarget.execute))]
    private static bool CheckHomeAtDoor(BehStayInBuildingTarget __instance, Actor pActor, ref BehResult __result)
    {
        if (!VillageDutyService.IsResident(pActor) || pActor.ai.task?.id != "sleep_inside") return true;
        var home = pActor.getHomeBuilding();
        bool valid = home != null ? home == pActor.beh_building_target :
            KingdomPolicyService.Has(pActor.city, KingdomPolicyCard.HomelessRelief) && VillageHomeService.IsEmptyShelter(pActor, pActor.beh_building_target);
        if (valid) return true;
        __result = __instance.forceTask(pActor, VillageHomeService.GetSleepHome(pActor) == null ? "sleep_outside" : "sleep_inside",
            pClean: true, pForceAction: true); return false;
    }

    /// <summary>走路过程中住房变化或已经选中露宿时，在真正入睡前重新回本人房屋。</summary>
    /// <param name="__instance">原版入睡行为。</param>
    /// <param name="pActor">即将进入睡眠状态的人物。</param>
    /// <param name="__result">继续睡眠或转去回家的结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehTrySleep), nameof(BehTrySleep.execute))]
    private static bool EnterOwnHome(BehTrySleep __instance, Actor pActor, ref BehResult __result)
    {
        if (!VillageDutyService.IsResident(pActor) || pActor.is_inside_boat || pActor.isFighting()) return true;
        var home = VillageHomeService.GetSleepHome(pActor);
        if (home == null) return true;
        if (pActor.inside_building == home)
        {
            if (pActor.hasHouse()) return true;
            pActor.makeSleep(__instance.getWaitTimer(pActor)); __result = BehResult.Continue; return false;
        }
        __result = __instance.forceTask(pActor, "sleep_inside", pClean: true, pForceAction: true); return false;
    }
}
