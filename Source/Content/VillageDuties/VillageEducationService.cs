using System.Linq;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.KingdomPolicies;
using Cultiway.Content.VillageMilitary;
using Cultiway.Core;

namespace Cultiway.Content.VillageDuties;

/// <summary>将儿童课程接入现有任务选择、移动、室内停留和书籍阅读流程。</summary>
[Dependency(typeof(ActorJobs))]
internal sealed class VillageEducationContent : ICanInit
{
    /// <summary>注册一次阅读任务，生产和已有职责优先于选课。</summary>
    public void Init()
    {
        var task = AssetManager.tasks_actor.add(new BehaviourTaskActor {
            id = VillageEducationService.TaskId, locale_key = "cultiway_school_study", force_hand_tool = "book" });
        task.setIcon("ui/Icons/iconBooks");
        task.addBeh(new BehPrepareChildEducation());
        task.addBeh(new BehGetTargetBuildingMainTile());
        task.addBeh(new BehGoToTileTarget());
        task.addBeh(new BehBeginChildEducation());
        task.addBeh(new BehFinishChildEducation());
        task.addBeh(new BehExitBuilding());
        var job = AssetManager.job_actor.add(new ActorJob { id = VillageEducationService.JobId });
        job.addTask(task.id); job.addTask("end_job");
        ActorJobSelectionRegistry.Register(VillageEducationService.TrySelect, 450);
    }
}

/// <summary>国家教育只安排未成年人空闲时间，不挤占童工、修炼、运输或玩家任务。</summary>
public static class VillageEducationService
{
    /// <summary>原版工作系统中的课程编号。</summary>
    public const string JobId = "Cultiway.ChildEducation";
    /// <summary>真实前往图书馆并阅读的任务编号。</summary>
    public const string TaskId = "Cultiway.ChildEducationStudy";

    /// <summary>当前是否已有生产职责，包括当月固定岗位与农时任务。</summary>
    /// <param name="actor">准备参加课程的孩童。</param>
    public static bool HasProductionDuty(Actor actor)
    {
        if (actor.citizen_job != null || actor.isCarryingResources() || actor.isWarrior()) return true;
        if (!KingdomPolicyService.CanProduce(actor)) return false;
        if (!VillageDutyService.CanTakeCivilianJob(actor) && !FamilyFarmService.CanSeasonalWork(actor)) return false;
        var data = VillageMilitaryService.Get(actor.city);
        if (data.Occupations.ContainsKey(actor.id)) return true;
        if (KingdomPolicyService.Has(actor.city, KingdomPolicyCard.OccupationPlanning)) return false;
        return VillageOccupationService.Jobs.Any(job => actor.city.jobs.hasJob(job) &&
            VillageOccupationService.CanTakeJob(actor, job) && job.should_be_assigned?.Invoke(actor) != false);
    }

    /// <summary>有年龄资格且目前空闲的孩童才接受课程，不改变生产年龄门槛。</summary>
    /// <param name="actor">需要判断的本村居民。</param>
    public static bool CanAttend(Actor actor)
    {
        if (!VillageDutyService.IsResident(actor) || actor.isAdult() || actor.isEgg() || actor.isHungry() ||
            actor.timer_action > 0 || !VillageDutyService.CanChangeAssignment(actor) || HasProductionDuty(actor)) return false;
        var policy = KingdomPolicyService.Get(actor.city.kingdom);
        return policy.Has(KingdomPolicyCard.Education) && actor.getAge() >= policy.SchoolAdmissionAge;
    }

    /// <summary>只从本村选择已完工、同岛并且入口安全的图书馆。</summary>
    /// <param name="actor">需要实际步行到场的学生。</param>
    public static Building FindLibrary(Actor actor) => actor.city.buildings.Where(b => b.asset.type == "type_library" &&
        b.isUsable() && !b.isUnderConstruction() && !b.isAbandoned() && !b.current_tile.isOnFire() &&
        b.current_tile.isSameIsland(actor.current_tile)).OrderBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile)).FirstOrDefault();

    /// <summary>原版刚结束工作时安排课程，不在更新循环中强制中断任务。</summary>
    /// <param name="actor">自然选择下一项工作的人物。</param>
    /// <param name="jobId">已经选出的原版工作或新课程。</param>
    public static bool TrySelect(Actor actor, ref string jobId)
    {
        if (!CanAttend(actor) || jobId != null && jobId != "unit_citizen" && jobId != "decision" &&
            !actor.asset.job_baby.Contains(jobId)) return false;
        if (!actor.city.hasBooksToRead(actor) || FindLibrary(actor) == null) return false;
        jobId = JobId; return true;
    }

    /// <summary>出发前再次检查年龄、职责、藏书和目标，避免走到失效学校。</summary>
    /// <param name="actor">开始课程的人物。</param>
    public static bool Prepare(Actor actor)
    {
        if (!CanAttend(actor) || !actor.city.hasBooksToRead(actor)) return false;
        var library = FindLibrary(actor); if (library == null) return false;
        actor.beh_building_target = library; actor.beh_tile_target = library.current_tile; return true;
    }

    /// <summary>到馆后借取真实城市藏书，并在馆内阅读一轮。</summary>
    /// <param name="actor">已经走到图书馆的孩童。</param>
    public static bool BeginReading(Actor actor)
    {
        var library = actor.beh_building_target;
        if (!CanAttend(actor) || !ValidLibrary(actor, library) ||
            Toolbox.SquaredDistTile(actor.current_tile, library.current_tile) > 9 ||
            new BehTryToRead().execute(actor) != BehResult.Continue) return false;
        actor.stayInBuilding(library); actor.timer_action = Randy.randomFloat(20f, 40f); return true;
    }

    /// <summary>完成一次真实阅读；功法、丹书和技能书沿用原有阅读结算及资格。</summary>
    /// <param name="actor">已完成馆内阅读等待的人物。</param>
    public static bool FinishReading(Actor actor)
    {
        bool valid = actor.city != null && ValidLibrary(actor, actor.beh_building_target) && actor.inside_building == actor.beh_building_target &&
            actor.beh_book_target?.isAlive() == true;
        bool completed = valid && new BehFinishReading().execute(actor) == BehResult.Continue;
        actor.beh_book_target = null; return completed;
    }

    /// <summary>失去图书馆或迁村后不再结算旧课程。</summary>
    /// <param name="actor">当前学生。</param>
    /// <param name="library">出发时选择的图书馆。</param>
    private static bool ValidLibrary(Actor actor, Building library) => library != null && library.city == actor.city &&
        library.asset.type == "type_library" && library.isUsable() && !library.isUnderConstruction() && !library.isAbandoned();
}

/// <summary>保留实际图书馆位置作为本轮移动目标。</summary>
internal sealed class BehPrepareChildEducation : BehCityActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor actor) => VillageEducationService.Prepare(actor) ? BehResult.Continue : BehResult.Stop;
}

/// <summary>课程开始前保持生产优先，成功后进入原版室内停留。</summary>
internal sealed class BehBeginChildEducation : BehCityActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor actor) => VillageEducationService.BeginReading(actor) ? BehResult.Continue : BehResult.Stop;
}

/// <summary>完成阅读后始终进入正常出馆步骤，失效课程不发放收益。</summary>
internal sealed class BehFinishChildEducation : BehCityActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor actor) { VillageEducationService.FinishReading(actor); return BehResult.Continue; }
}
