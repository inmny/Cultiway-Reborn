using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageMilitary;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageDuties;

/// <summary>计算普通工人的真实职业目标，并在工作月分批安排任务。</summary>
public static partial class VillageOccupationService
{
    /// <summary>各村已经计算的普通工人目标，不包含特殊者自主参与的人数。</summary>
    private static readonly Dictionary<long, Dictionary<string, int>> plans = new();
    /// <summary>人数计划只属于当前世界。</summary>
    private static MapStats planWorld;
    /// <summary>列出普通生产职业，军人仍由国家军制管理。</summary>
    public static IEnumerable<CitizenJobAsset> Jobs => AssetManager.citizen_job_library.list.Where(j => j.common_job && !j.only_leaders);

    /// <summary>换世界时丢弃人数计划，职业归属仍随各村存档恢复。</summary>
    private static void EnsureWorld()
    {
        if (ReferenceEquals(planWorld, World.world.map_stats)) return;
        plans.Clear(); supports.Clear(); planWorld = World.world.map_stats;
    }

    /// <summary>只给普通、具有劳动资格的本村工人建立职业归属。</summary>
    /// <param name="actor">待安排职业的人物。</param>
    private static bool IsWorker(Actor actor) => VillageDutyService.IsResident(actor) && actor.canWork() &&
        !actor.isWarrior() && VillageDutyService.Classify(actor) == VillageResidentKind.Worker;

    /// <summary>原版重新清点资源后，按实际劳动力和库存缺口重新计算生产人数。</summary>
    /// <param name="city">完成原版岗位检查的村庄。</param>
    public static void ApplyJobTargets(City city)
    {
        EnsureWorld();
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.OccupationPlanning)) { plans.Remove(city.id); return; }
        int workers = 0;
        var voluntary = new Dictionary<CitizenJobAsset, int>();
        var temporary = new Dictionary<CitizenJobAsset, int>();
        city.jobs.occupied.Clear();
        foreach (var actor in city.units)
        {
            bool worker = IsWorker(actor);
            if (worker) workers++;
            if (actor.citizen_job == null) continue;
            city.jobs.takeJob(actor.citizen_job);
            if (worker && supports.TryGetValue(actor.id, out string support) && actor.citizen_job.id == support)
            { temporary.TryGetValue(actor.citizen_job, out int helpers); temporary[actor.citizen_job] = helpers + 1; }
            if (!worker)
            {
                voluntary.TryGetValue(actor.citizen_job, out int count);
                voluntary[actor.citizen_job] = count + 1;
            }
        }
        var targets = VillageWorkforcePlanner.Create(city, workers, VillageMilitaryService.Get(city).Policy.OccupationTargets);
        plans[city.id] = targets;
        foreach (var job in Jobs)
        {
            voluntary.TryGetValue(job, out int extra);
            temporary.TryGetValue(job, out int helpers);
            int slots = (int)Math.Min(int.MaxValue, (long)targets[job.id] + extra + helpers);
            int previous = city.jobs.countCurrentJobs(job);
            city.jobs.jobs[job] = slots;
            city.jobs._total_tasks = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)city.jobs._total_tasks + slots - previous));
        }
    }

    /// <summary>读取普通工人目标，未完成首次岗位计算时显示现有岗位数。</summary>
    /// <param name="city">职业所属的村庄。</param>
    /// <param name="job">需要展示的职业。</param>
    public static int PlannedWorkers(City city, CitizenJobAsset job)
    {
        EnsureWorld();
        return plans.TryGetValue(city.id, out var targets) && targets.TryGetValue(job.id, out int count)
            ? count : city.jobs.countCurrentJobs(job);
    }

    /// <summary>工作结束时立即释放原版占用数，避免空岗位仍显示已被占满。</summary>
    /// <param name="actor">即将结束当前工作的人物。</param>
    public static void ReleaseJob(Actor actor)
    {
        if (actor.city != null && actor.citizen_job?.common_job == true) actor.city.jobs.freeJob(actor.citizen_job);
        if (!supports.TryGetValue(actor.id, out string support)) return;
        supports.Remove(actor.id);
        if (actor.city != null && actor.citizen_job?.id == support) AdjustSupportSlot(actor.city, actor.citizen_job, -1);
    }

    /// <summary>先保留现有员工，再补缺和派工，每轮最多调整三十二名普通工人。</summary>
    /// <param name="city">本轮处理的村庄。</param>
    /// <param name="data">月历、手动定员和职业归属。</param>
    public static void Reconcile(City city, VillageMilitaryData data)
    {
        EnsureWorld();
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.OccupationPlanning)) return;
        if (!data.ArmyQuotaReady || !plans.TryGetValue(city.id, out var targets)) return;
        var counts = new Dictionary<string, int>();
        int changes = RecallSupports(city);
        // 超额或失效归属在安全动作结束后释放，不打断战斗、运输和基本生活需求。
        foreach (var pair in data.Occupations.ToArray())
        {
            var actor = World.world.units.get(pair.Key);
            counts.TryGetValue(pair.Value, out int count);
            bool worker = actor?.city == city && IsWorker(actor);
            if (worker && targets.TryGetValue(pair.Value, out int wanted) && count < wanted)
            { counts[pair.Value] = count + 1; continue; }
            if (worker && (!VillageDutyService.CanChangeAssignment(actor) || actor.timer_action > 0 || changes >= 32))
            { counts[pair.Value] = count + 1; continue; }
            if (worker && actor.citizen_job?.id == pair.Value) actor.cancelAllBeh();
            data.Occupations.Remove(pair.Key); changes++;
        }
        // 先登记正在做本职工作的人，避免先派新人、再挤走原有员工。
        foreach (var actor in city.units)
        {
            if (changes >= 32) break;
            string id = actor.citizen_job?.id;
            if (id == null || data.Occupations.ContainsKey(actor.id) || !IsWorker(actor) || !targets.TryGetValue(id, out int wanted)) continue;
            counts.TryGetValue(id, out int count);
            if (count >= wanted) continue;
            data.Occupations.Add(actor.id, id); counts[id] = count + 1; changes++;
        }
        if (changes < 32 && targets.Any(p => !counts.TryGetValue(p.Key, out int count) ? p.Value > 0 : count < p.Value))
        {
            var candidates = city.units.Where(a => !data.Occupations.ContainsKey(a.id) && IsWorker(a) &&
                VillageDutyService.CanChangeAssignment(a) && a.timer_action <= 0).ToList();
            foreach (var job in Jobs.OrderByDescending(j => data.Policy.OccupationTargets.ContainsKey(j.id))
                         .ThenByDescending(j => targets[j.id]))
            {
                counts.TryGetValue(job.id, out int count);
                foreach (var actor in candidates)
                {
                    if (count >= targets[job.id] || changes >= 32) break;
                    if (data.Occupations.ContainsKey(actor.id) || job.should_be_assigned?.Invoke(actor) == false) continue;
                    if (actor.citizen_job != null && actor.citizen_job != job) actor.cancelAllBeh();
                    data.Occupations.Add(actor.id, job.id); counts[job.id] = ++count; changes++;
                    TryDispatch(actor);
                }
            }
        }
        // 工作月不再等待随机社交决策碰巧领工，空闲员工优先进入自己的生产任务。
        foreach (long id in data.Occupations.Keys)
        {
            if (changes >= 32) break;
            var actor = World.world.units.get(id);
            if (actor != null && TryDispatch(actor)) changes++;
        }
    }

    /// <summary>平时从闲散行为领工；抢收时召回社交、制作和较低优先的农活，保留基本生活与运输。</summary>
    /// <param name="actor">已经具有职业归属的人物。</param>
    private static bool TryDispatch(Actor actor)
    {
        bool farmer = IsWorker(actor) && VillageMilitaryService.Get(actor.city).Occupations.TryGetValue(actor.id, out string role) && role == "farmer";
        bool harvest = farmer && FamilyFarming.FamilyFarmService.HasHarvest(actor);
        string task = actor.ai.task?.id;
        bool indoorWork = harvest && task is "make_items" or "generate_loot";
        if (!VillageDutyService.CanChangeAssignment(actor, indoorWork)) return false;
        if (!VillageDutyService.CanTakeCivilianJob(actor) && !FamilyFarming.FamilyFarmService.CanSeasonalWork(actor)) return false;
        if (!harvest && (actor.timer_action > 0 || actor.citizen_job != null) ||
            actor.isHungry() && (!farmer || actor.city.hasAnyFood())) return false;
        string job = actor.ai.job?.id;
        if (job != null && job != "unit_citizen" && job != "decision" && (!harvest || job != "farmer")) return false;
        bool idle = task == null || task is "make_decision" or "city_idle_walking" or "random_move" or
            "singing" or "swearing" or "happy_laughing" or "try_to_read" or "socialize_try_to_start_near_bonfire";
        if (harvest && !actor.isCarryingResources())
        {
            string action = FamilyFarming.FamilyFarmService.CurrentAction(actor.id);
            idle |= task is "make_items" or "generate_loot" or "random_move_near_house" ||
                task?.StartsWith("socialize_", StringComparison.Ordinal) == true ||
                task == "try_to_eat_city_food" && !actor.city.hasAnyFood() ||
                actor.ai.task == ActorTasks.FamilyFarmWork && action is "Till" or "Plant" or "Clear" or "Fertilize";
        }
        if (!idle) return false;
        actor.cancelAllBeh(); actor.city.setCitizenJob(actor);
        return actor.citizen_job != null;
    }

    /// <summary>职业归属限制普通工人，特殊者和管理者仍可自主选择工作。</summary>
    /// <param name="actor">准备领取或继续工作的人物。</param>
    /// <param name="job">原版生产职业。</param>
    public static bool CanTakeJob(Actor actor, CitizenJobAsset job)
    {
        if (!VillageDutyService.CanTakeCivilianJob(actor) && (job != CitizenJobLibrary.farmer || !FamilyFarming.FamilyFarmService.CanSeasonalWork(actor))) return false;
        if (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.OccupationPlanning)) return true;
        if (!IsWorker(actor)) return true;
        EnsureWorld();
        if (supports.TryGetValue(actor.id, out string support)) return support == job.id && FamilyFarming.FamilyFarmService.CanSupportOtherWork(actor);
        if (!plans.TryGetValue(actor.city.id, out var targets)) return true;
        var data = VillageMilitaryService.Get(actor.city);
        if (!targets.TryGetValue(job.id, out int count) || count <= 0) return false;
        return data.Occupations.TryGetValue(actor.id, out string assigned) ? assigned == job.id : actor.citizen_job == job;
    }

    /// <summary>已经分配职业时直接尝试本职工作，保留原版任务条件和岗位计数。</summary>
    /// <param name="actor">准备领取岗位的人物。</param>
    public static bool TryAssignFixedJob(Actor actor)
    {
        if (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.OccupationPlanning)) return false;
        var data = VillageMilitaryService.Get(actor.city);
        if (!data.Occupations.TryGetValue(actor.id, out string id) || !IsWorker(actor)) return false;
        var job = AssetManager.citizen_job_library.get(id);
        if (job != null && CanTakeJob(actor, job)) actor.city.checkCitizenJob(job, actor.city, actor);
        return true;
    }

    /// <summary>保留本职人员，未安排职业者和特殊者仍可参与常规集体工程。</summary>
    /// <param name="actor">准备参与常规工程的人物。</param>
    public static bool CanTakeRoutineProject(Actor actor) => !KingdomPolicyService.Has(actor.city, KingdomPolicyCard.OccupationPlanning) || !VillageDutyService.IsResident(actor) ||
        !VillageMilitaryService.Get(actor.city).Occupations.ContainsKey(actor.id) || !IsWorker(actor);

    /// <summary>政策提交后重算原版岗位，不删除保存的手动定员。</summary>
    /// <param name="city">采用新制度的村庄。</param>
    public static void PolicyChanged(City city)
    {
        EnsureWorld(); plans.Remove(city.id);
        var data = VillageMilitaryService.Get(city);
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.OccupationPlanning))
        {
            foreach (long id in supports.Keys.Where(id => World.world.units.get(id)?.city == city).ToArray())
            {
                var actor = World.world.units.get(id);
                if (VillageDutyService.CanChangeAssignment(actor) && !actor.isCarryingResources()) actor.cancelAllBeh();
                else supports.Remove(id);
            }
            data.Occupations.Clear();
        }
        new ai.behaviours.CityBehCheckCitizenTasks().execute(city);
    }
}
