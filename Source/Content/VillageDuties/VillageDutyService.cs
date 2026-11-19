using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Cultiway.Utils.Extension;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageDuties;

/// <summary>月份只约束村庄例行任务，不修改原版年龄、文化与修行资格。</summary>
public static class VillageDutyService
{
    /// <summary>判断是否为四类人员统计中的本村智慧居民。</summary>
    /// <param name="actor">待查询的人物。</param>
    public static bool IsResident(Actor actor) => actor != null && actor.isAlive() && actor.hasCity() &&
                                                  actor.isSapient() && !actor.asset.is_boat && actor.asset != Actors.Train;

    /// <summary>管理身份优先，其次是修行体系，最后根据真实军籍区分普通居民。</summary>
    /// <param name="actor">已经确认属于村庄的智慧居民。</param>
    public static VillageResidentKind Classify(Actor actor)
    {
        if (actor.isKing() || actor.isCityLeader()) return VillageResidentKind.Manager;
        lock (EntityStoreLock.GlobalLock)
        {
            var extend = actor.GetExtend();
            if (extend.HasCultisys<Xian>() || extend.HasCultisys<Magic>() || extend.HasCultisys<Knight>())
                return VillageResidentKind.Special;
        }
        return actor.isWarrior() ? VillageResidentKind.Soldier : VillageResidentKind.Worker;
    }

    /// <summary>村庄确有敌情或明确出征命令时，军令可以打断士兵休息。</summary>
    /// <param name="city">待检查的村庄。</param>
    public static bool HasEmergency(City city) => city != null && (city.hasAttackZoneOrder() || city.isInDanger());

    /// <summary>当前是否为本村工作月，使用原版世界月历。</summary>
    /// <param name="city">采用本村月历的村庄。</param>
    public static bool IsWorkMonth(City city) => !KingdomPolicyService.Has(city, KingdomPolicyCard.WorkCalendar) || VillageMilitary.VillageMilitaryService.Get(city).Policy
        .IsWorkMonth(Date.getCurrentMonth());

    /// <summary>只阻止普通居民在休息月领取村庄生产岗位，特殊者和管理者沿用自主选择。</summary>
    /// <param name="actor">准备领取岗位的人物。</param>
    public static bool CanTakeCivilianJob(Actor actor)
    {
        if (IsResident(actor) && !KingdomPolicyService.CanProduce(actor)) return false;
        if (!IsResident(actor) || IsWorkMonth(actor.city)) return true;
        if (actor.isWarrior() && VillageMilitary.VillageMilitaryActivities.IsOrdered(actor.city)) return true;
        var kind = Classify(actor);
        return kind is VillageResidentKind.Manager or VillageResidentKind.Special;
    }

    /// <summary>日常勤务只包含普通现役士兵，明确军令则包含特殊军职者。</summary>
    /// <param name="actor">准备参加军事行动的人物。</param>
    /// <param name="ordered">是否有明确军令或实际敌情。</param>
    public static bool CanPerformMilitaryDuty(Actor actor, bool ordered)
    {
        if (!IsResident(actor) || !actor.isWarrior() || actor.isKing() || actor.isCityLeader()) return false;
        return ordered || Classify(actor) == VillageResidentKind.Soldier && IsWorkMonth(actor.city);
    }

    /// <summary>人物交战、登乘或由玩家直接操控时不强行改编和取消当前工作。</summary>
    /// <param name="actor">准备调整军籍或工作的人物。</param>
    public static bool CanChangeAssignment(Actor actor) => CanChangeAssignment(actor, false);

    /// <summary>抢收可取消非生活类室内事务，仍保留战斗、运输和玩家命令。</summary>
    /// <param name="actor">准备返岗的人物。</param>
    /// <param name="allowInsideBuilding">仅供抢收召回室内制作等非生活事务，其他入口仍等待人物出屋。</param>
    public static bool CanChangeAssignment(Actor actor, bool allowInsideBuilding) => actor.isAlive() && !actor.isFighting() &&
        !actor.is_inside_boat && (allowInsideBuilding || !actor.isInsideSomething()) && !ControllableUnit.isControllingUnit(actor) &&
        life.taxi.TaxiManager.getRequestForActor(actor) == null &&
        Cultiway.Core.Pathfinding.PortalManager.GetRequest(actor) == null && actor.ai.task?.flag_boat_related != true &&
        !ControlledTaskOrderService.TryGetActiveOrderId(actor.id, out _) &&
        actor.ai.job?.id != ActorJobs.CultivationProgression.id && actor.ai.task?.id != ActorTasks.CultivationProgression.id;
}
