using ai.behaviours;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.Core;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.Combat.Tactical;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>在原版已有岗位、军籍、击杀与保存入口应用村庄制度。</summary>
internal static class PatchVillageDuties
{
    /// <summary>击杀前初始化军功，已有击杀只在首次建立记录时导入。</summary>
    /// <param name="__instance">即将登记击杀的人物。</param>
    /// <param name="__state">此次击杀之前的军功。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.increaseKills))]
    private static void BeforeKill(Actor __instance, out long __state) => __state = MilitaryMeritService.Get(__instance);

    /// <summary>每笔原版个人击杀只在这里增加一次军功。</summary>
    /// <param name="__instance">原版登记击杀的人物。</param>
    /// <param name="__state">同次调用前保存的军功。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.increaseKills))]
    private static void AfterKill(Actor __instance, long __state) => MilitaryMeritService.RecordKill(__instance, __state);

    /// <summary>在岗位计数增加之前阻止休息人员领取普通村庄任务。</summary>
    /// <param name="pActor">准备领取岗位的人物。</param>
    /// <param name="pJobAsset">准备领取的原版生产职业。</param>
    /// <param name="__result">原版岗位领取结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.checkCitizenJob))]
    private static bool BeforeCivilianJob(Actor pActor, CitizenJobAsset pJobAsset, ref bool __result)
    {
        if (VillageOccupationService.CanTakeJob(pActor, pJobAsset)) return true;
        __result = false; return false;
    }

    /// <summary>提前跳过休息人员的工作决策，避免反复遍历全部岗位。</summary>
    /// <param name="pActor">准备选择村庄岗位的人物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.setCitizenJob))]
    private static bool BeforeFindJob(Actor pActor) => (VillageDutyService.CanTakeCivilianJob(pActor) || FamilyFarming.FamilyFarmService.CanSeasonalWork(pActor)) &&
        !VillageOccupationService.TryAssignFixedJob(pActor);

    /// <summary>原版清空工作引用之前归还岗位占用，不清除人物保留的职业归属。</summary>
    /// <param name="__instance">即将结束工作的人物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.endJob))]
    private static void ReleaseCitizenJob(Actor __instance) => VillageOccupationService.ReleaseJob(__instance);

    /// <summary>农务等持续工作在原版检查结束条件时释放岗位。</summary>
    /// <param name="pActor">已经执行到安全检查点的人物。</param>
    /// <param name="__result">当前行为的继续或停止结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(BehCheckEndCityActorJob), nameof(BehCheckEndCityActorJob.execute))]
    private static bool EndRestingWork(Actor pActor, ref BehResult __result)
    {
        if (pActor.citizen_job == null || VillageOccupationService.CanTakeJob(pActor, pActor.citizen_job)) return true;
        pActor.endJob(); __result = BehResult.Stop; return false;
    }

    /// <summary>普通休息人员不领取模组的例行集体工程，特殊者仍由自身活动决定。</summary>
    /// <param name="actor">准备领取例行工程的人物扩展。</param>
    /// <param name="jobId">原版工作选择使用的工作编号。</param>
    /// <param name="__result">是否实际取得工程工作。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(CollectiveProjectService), nameof(CollectiveProjectService.TryAssignRoutineJob))]
    private static bool BeforeCollectiveJob(ActorExtend actor, ref string jobId, ref bool __result)
    {
        if (VillageDutyService.CanTakeCivilianJob(actor.Base) && VillageOccupationService.CanTakeRoutineProject(actor.Base)) return true;
        jobId = null; __result = false; return false;
    }

    /// <summary>原版资格通过后，还必须有编制空缺并属于本轮选中的补员。</summary>
    /// <param name="__instance">正在征兵的村庄。</param>
    /// <param name="pActor">原版检查的候选人。</param>
    /// <param name="__result">原版资格检查结果。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.checkCanMakeWarrior))]
    private static void CheckRecruit(City __instance, Actor pActor, ref bool __result)
    { if (__result) __result = VillageMilitaryService.CanRecruit(__instance, pActor); }

    /// <summary>读档初始额度不是退役依据，等原版完成一次实际岗位计算。</summary>
    /// <param name="pCity">刚完成计算的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CityBehCheckCitizenTasks), nameof(CityBehCheckCitizenTasks.execute))]
    private static void JobsCalculated(City pCity)
    { VillageOccupationService.ApplyJobTargets(pCity); VillageMilitaryService.ArmyQuotaCalculated(pCity); }

    /// <summary>原版检查统领时沿用已建立的军功指挥链。</summary>
    /// <param name="__instance">正在检查统领的原版军队。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Army), nameof(Army.findCaptain))]
    private static bool SelectCaptain(Army __instance) => !VillageMilitaryService.EnsureCaptain(__instance);

    /// <summary>军队战斗指令继续使用原版 Army，同时通知协调移动。</summary>
    /// <param name="army">收到明确命令的原版军队。</param>
    /// <param name="directive">本次战术命令。</param>
    /// <param name="durationSeconds">命令持续的模拟秒数。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CombatWorldService), nameof(CombatWorldService.SetDirective))]
    private static void RecordArmyOrder(Army army, CombatDirective directive, float durationSeconds)
    { if (army != null) VillageMilitaryActivities.RecordOrder(army, directive, durationSeconds); }

    /// <summary>例行训练和跟队服从同一作息规则，运输收尾和即时战斗沿用原版。</summary>
    /// <param name="__instance">原版正在检查的决策。</param>
    /// <param name="pActor">准备采取行动的人物。</param>
    /// <param name="__result">当前决策是否允许启动。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(DecisionAsset), nameof(DecisionAsset.isPossible), typeof(Actor))]
    private static bool CheckRoutineDecision(DecisionAsset __instance, Actor pActor, ref bool __result)
    {
        bool routine = __instance.id is "warrior_train_with_dummy" or "warrior_army_captain_idle_walking_city" or
            "warrior_army_captain_waiting" or "warrior_army_leader_move_random" or "warrior_army_follow_leader" or
            "warrior_army_leader_move_to_attack_target";
        if (!routine || !VillageDutyService.IsResident(pActor) ||
            VillageDutyService.CanPerformMilitaryDuty(pActor, VillageMilitaryActivities.IsOrdered(pActor.city))) return true;
        __result = false; return false;
    }

    /// <summary>人物归属变化前保存旧村庄。</summary>
    /// <param name="__instance">即将迁移的人物。</param>
    /// <param name="__state">迁移前的村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.setCity))]
    private static void BeforeMove(Actor __instance, out City __state) => __state = __instance.city;

    /// <summary>只唤醒迁出与迁入的村庄。</summary>
    /// <param name="__instance">完成迁移的人物。</param>
    /// <param name="__state">此前保存的原村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.setCity))]
    private static void AfterMove(Actor __instance, City __state)
    { VillageMilitaryService.Invalidate(__state); VillageMilitaryService.Invalidate(__instance.city); }

    /// <summary>职业变化后同步人员和空缺状态。</summary>
    /// <param name="__instance">改变职业的人物。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.setProfession))]
    private static void ProfessionChanged(Actor __instance) => VillageMilitaryService.Invalidate(__instance.city);

    /// <summary>村庄毁灭时释放本村行动与名册，保留人物个人军功。</summary>
    /// <param name="__instance">正在消亡的村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.destroyCity))]
    private static void RemoveCity(City __instance) => VillageMilitaryService.Remove(__instance);

    /// <summary>易主时撤销上一国家的临时军令，月历保留，编制改用新国家设置。</summary>
    /// <param name="__instance">改变国家归属的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.setKingdom))]
    private static void ChangeKingdom(City __instance)
    { VillageMilitaryActivities.ClearOrders(__instance); VillageMilitaryService.Invalidate(__instance); }

    /// <summary>村庄保存时写入制度与下级名册。</summary>
    /// <param name="__instance">正在保存的原版村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.save))]
    private static void SaveCity(City __instance) => VillageMilitaryService.Save(__instance);
}
