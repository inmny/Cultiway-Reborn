using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.VillageMilitary;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageDuties;

/// <summary>农闲时短暂支援生产，始终保留农民归属和家庭田块。</summary>
public static partial class VillageOccupationService
{
    /// <summary>只存在于本世界运行期的临时职业，不写入永久职业归属。</summary>
    private static readonly Dictionary<long, string> supports = new();

    /// <summary>读取当前临时支援的职业，供农田和职业页面解释实际在岗差异。</summary>
    /// <param name="actorId">人物编号。</param>
    public static string TemporaryJob(long actorId) => supports.TryGetValue(actorId, out string job) ? job : null;

    /// <summary>当前职业中由农民临时支援的人数。</summary>
    /// <param name="city">岗位所属村庄。</param>
    /// <param name="job">需要展示的职业。</param>
    public static int TemporaryWorkers(City city, CitizenJobAsset job) => city.units.Count(a =>
        a.citizen_job == job && supports.TryGetValue(a.id, out string support) && support == job.id);

    /// <summary>只为实际支援者增加一个可领取工位，结束时立即归还。</summary>
    /// <param name="city">岗位所属村庄。</param>
    /// <param name="job">临时支援的职业。</param>
    /// <param name="delta">增加或归还的一个工位。</param>
    private static void AdjustSupportSlot(City city, CitizenJobAsset job, int delta)
    {
        city.jobs.jobs[job] = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)city.jobs.countCurrentJobs(job) + delta));
        city.jobs._total_tasks = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)city.jobs._total_tasks + delta));
    }

    /// <summary>按缺粮、缺木石和施工需求选择支援岗位，手动零人数仍禁止安排。</summary>
    /// <param name="actor">已经没有可执行农活的普通农民。</param>
    public static bool TrySeasonalSupport(Actor actor)
    {
        EnsureWorld();
        if (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.OccupationPlanning)) return false;
        if (!FamilyFarmService.CanSupportOtherWork(actor) || !plans.TryGetValue(actor.city.id, out var targets) ||
            !VillageDutyService.CanChangeAssignment(actor) || actor.isCarryingResources() || actor.isHungry()) return false;
        var data = VillageMilitaryService.Get(actor.city);
        if (!data.Occupations.TryGetValue(actor.id, out string role) || role != "farmer") return false;
        var candidates = Jobs.Where(j => j.id != "farmer" && targets[j.id] > 0 && j.should_be_assigned?.Invoke(actor) != false)
            .OrderByDescending(j => SupportPriority(actor.city, j.id)).ToArray();
        foreach (var job in candidates)
        {
            actor.endJob(); supports[actor.id] = job.id; AdjustSupportSlot(actor.city, job, 1);
            if (actor.city.checkCitizenJob(job, actor.city, actor)) return true;
            supports.Remove(actor.id); AdjustSupportSlot(actor.city, job, -1);
        }
        return false;
    }

    /// <summary>食物告急优先采集，其次补充木石和完成已有建设。</summary>
    /// <param name="city">提供库存和施工需求的村庄。</param>
    /// <param name="job">候选职业编号。</param>
    private static int SupportPriority(City city, string job) => job switch {
        "gatherer_bushes" or "gatherer_honey" or "hunter" => city.getTotalFood() < Math.Max(40, city.status.population * 2) ? 10 : 2,
        "woodcutter" => city.getResourcesAmount("wood") < Math.Max(30, city.status.population / 3) ? 9 : 2,
        "miner" or "miner_deposit" => city.getResourcesAmount("stone") < Math.Max(20, city.status.population / 4) ? 9 : 2,
        "builder" => city.hasBuildingToBuild() ? 8 : 0,
        _ => 1 };

    /// <summary>农时到来或休息时，只在安全动作结束、没有搬运材料时召回支援者。</summary>
    /// <param name="city">正在协调职业的村庄。</param>
    private static int RecallSupports(City city)
    {
        int changed = 0;
        foreach (long id in supports.Keys.ToArray())
        {
            if (changed >= 32) break;
            var actor = World.world.units.get(id);
            if (actor == null) { supports.Remove(id); continue; }
            if (actor.city != city || FamilyFarmService.CanSupportOtherWork(actor) ||
                !VillageDutyService.CanChangeAssignment(actor) || actor.timer_action > 0 || actor.isCarryingResources()) continue;
            actor.cancelAllBeh(); changed++;
        }
        return changed;
    }
}
