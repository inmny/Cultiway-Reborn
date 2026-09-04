using Cultiway.Abstract;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Behaviours.Conditions;
using Cultiway.Core;
using Cultiway.Core.CollectiveProjects;
using Cultiway.Core.Coordination;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

[Dependency(typeof(ActorTasks), typeof(SectAffairs), typeof(CoordinationActivities))]
public partial class ActorJobs : ExtendLibrary<ActorJob, ActorJobs>
{
    public static ActorJob XianCultivator      { get; private set; }
    public static ActorJob PlantXianCultivator { get; private set; }
    public static ActorJob EnvironmentalCultivator { get; private set; }
    public static ActorJob MagicCultivator     { get; private set; }
    /// <summary>骑士和平期操练工作（积累斗气），斗气蓄满后主动执行进阶。</summary>
    public static ActorJob KnightCultivator    { get; private set; }

    /// <summary>元婴离体后强制执行的寻主工作。</summary>
    public static ActorJob YuanyingPossession { get; private set; }
    /// <summary>由通用进阶选择器主动分配、只执行一次当前候选进阶的工作。</summary>
    public static ActorJob CultivationProgression { get; private set; }
    /// <summary>角色自然换工作时认领并执行一个所属组织的常规集体工程。</summary>
    public static ActorJob CollectiveProject { get; private set; }
    /// <summary>角色接受自愿邀请后持续执行的通用协调行动工作。</summary>
    public static ActorJob CoordinatedActivity { get; private set; }
    public static ActorJob MagicWebResearcher  { get; private set; }
    public static ActorJob MagicScrollStudent  { get; private set; }
    public static ActorJob MagicSpellResearcher { get; private set; }
    public static ActorJob HerbCollector { get; private set; }
    public static ActorJob ElixirCrafter { get; private set; }
    public static ActorJob ElixirFinder { get; private set; }
    public static ActorJob TalismanCrafter { get; private set; }
    public static ActorJob MagicScrollCrafter { get; private set; }
    public static ActorJob ArtifactCrafter { get; private set; }
    public static ActorJob CultibookResearcher { get; private set; }
    public static ActorJob SectBuilder { get; private set; }
    public static ActorJob BookWriter { get; private set; }
    public static ActorJob SpawnedUnit { get; private set; }
    public static ActorJob SkavenGroup { get; private set; }
    public static ActorJob ChaosWarband { get; private set; }
    
    // 师徒系统工作
    public static ActorJob MasterDuty { get; private set; }
    public static ActorJob ApprenticeDuty { get; private set; }
    public static ActorJob SectDuty { get; private set; }
    public static ActorJob SectStudy { get; private set; }
    public static ActorJob SectAffair { get; private set; }
    public static ActorJob SectConstruction { get; private set; }
    public static ActorJob SectTreasure { get; private set; }
    
    [GetOnly("attacker")]
    public static ActorJob Attacker { get; private set; }
    [GetOnly("random_move")]
    public static ActorJob RandomMove { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ActorJob";
    protected override void OnInit()
    {
        InitYaoJobs();
        XianCultivator.addTask(ActorTasks.SwitchCultibook.id);
        XianCultivator.addCondition(new CondCanSwitchCultibook());
        XianCultivator.addTask(ActorTasks.DailyXianCultivate.id);
        XianCultivator.addCondition(new CondHasXian());
        XianCultivator.addCondition(new CondCanProgressCultivation(), false);
        XianCultivator.addTask(ActorTasks.CultivationProgression.id);
        XianCultivator.addCondition(new CondCanProgressCultivation());
        XianCultivator.addTask(ActorTasks.EndJob.id);

        PlantXianCultivator.addTask(ActorTasks.SwitchCultibook.id);
        PlantXianCultivator.addCondition(new CondCanSwitchCultibook());
        PlantXianCultivator.addTask(ActorTasks.DailyPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondHasXian());
        PlantXianCultivator.addCondition(new CondCanProgressCultivation(), false);
        PlantXianCultivator.addTask(ActorTasks.CultivationProgression.id);
        PlantXianCultivator.addCondition(new CondCanProgressCultivation());
        PlantXianCultivator.addTask(ActorTasks.EndJob.id);

        EnvironmentalCultivator.addTask(ActorTasks.SwitchCultibook.id);
        EnvironmentalCultivator.addCondition(new CondCanSwitchCultibook());
        EnvironmentalCultivator.addTask(ActorTasks.DailyEnvironmentalCultivate.id);
        EnvironmentalCultivator.addCondition(new CondHasXian());
        EnvironmentalCultivator.addCondition(new CondCanProgressCultivation(), false);
        EnvironmentalCultivator.addTask(ActorTasks.CultivationProgression.id);
        EnvironmentalCultivator.addCondition(new CondCanProgressCultivation());
        EnvironmentalCultivator.addTask(ActorTasks.EndJob.id);

        MagicCultivator.addTask(ActorTasks.DailyMagicMeditate.id);
        MagicCultivator.addCondition(new CondCanProgressCultivation(), false);
        MagicCultivator.addTask(ActorTasks.CultivationProgression.id);
        MagicCultivator.addCondition(new CondCanProgressCultivation());
        MagicCultivator.addTask(ActorTasks.EndJob.id);

        KnightCultivator.addTask(ActorTasks.DailyKnightTrain.id);
        KnightCultivator.addCondition(new CondHasKnightTrainingDummy());
        KnightCultivator.addCondition(new CondCanProgressCultivation(), false);
        KnightCultivator.addTask(ActorTasks.CultivationProgression.id);
        KnightCultivator.addCondition(new CondCanProgressCultivation());
        KnightCultivator.addTask(ActorTasks.EndJob.id);

        YuanyingPossession.addTask(ActorTasks.YuanyingPossession.id);
        YuanyingPossession.addTask(ActorTasks.EndJob.id);
        ActorJobSelectionRegistry.Register(TrySelectYuanyingPossessionJob, 10000);

        CultivationProgression.addTask(ActorTasks.CultivationProgression.id);
        CultivationProgression.addTask(ActorTasks.EndJob.id);
        ActorJobSelectionRegistry.Register(TrySelectCultivationProgressionJob, 1000);

        CollectiveProject.addTask(ActorTasks.ExecuteCollectiveProject.id);
        CollectiveProject.addTask(ActorTasks.EndJob.id);
        ActorJobSelectionRegistry.Register(TrySelectCollectiveProjectJob, 500);

        CoordinatedActivity.addTask(ActorTasks.CoordinatedActivity.id);
        CoordinatedActivity.addTask(ActorTasks.EndJob.id);
        CoordinatedActivityService.ConfigureRoutineJob(
            CoordinatedActivity.id,
            ActorTasks.CoordinatedActivity.id);
        ActorJobSelectionRegistry.Register(TryContinueCoordinatedActivityJob, 2000);
        ActorJobSelectionRegistry.Register(TrySelectCoordinatedActivityJob, 900);

        MagicWebResearcher.addTask(ActorTasks.StudyMagicWeb.id);
        MagicWebResearcher.addCondition(new CondShouldStudyMagicWeb());
        MagicWebResearcher.addTask(ActorTasks.EndJob.id);

        MagicScrollStudent.addTask(ActorTasks.StudyMagicScroll.id);
        MagicScrollStudent.addCondition(new CondCanStudyMagicScroll());
        MagicScrollStudent.addTask(ActorTasks.EndJob.id);

        MagicSpellResearcher.addTask(ActorTasks.ImproveMagicSpell.id);
        MagicSpellResearcher.addCondition(new CondCanImproveMagicSpell());
        MagicSpellResearcher.addTask(ActorTasks.EndJob.id);

        HerbCollector.addTask(ActorTasks.RandomMove.id);
        HerbCollector.addTask(ActorTasks.LookForHerbs.id);
        HerbCollector.addTask(ActorTasks.EndJob.id);

        ElixirCrafter.addTask(ActorTasks.CraftElixir.id);
        ElixirCrafter.addCondition(new CondHasJindan());
        ElixirCrafter.addCondition(new CondHasElixirRecipe());
        ElixirCrafter.addTask(ActorTasks.EndJob.id);
        
        ElixirFinder.addTask(ActorTasks.FindNewElixir.id);
        ElixirFinder.addCondition(new CondHasJindan());
        ElixirFinder.addTask(ActorTasks.EndJob.id);
        
        TalismanCrafter.addTask(ActorTasks.CraftTalisman.id);
        TalismanCrafter.addCondition(new CondHasXian());
        TalismanCrafter.addCondition(new CondHasXianBase());
        TalismanCrafter.addCondition(new CondHasEnoughWakan());
        TalismanCrafter.addTask(ActorTasks.EndJob.id);

        MagicScrollCrafter.addTask(ActorTasks.CraftMagicScroll.id);
        MagicScrollCrafter.addCondition(new CondCanCraftMagicScroll());
        MagicScrollCrafter.addTask(ActorTasks.EndJob.id);

        ArtifactCrafter.addTask(ActorTasks.CraftArtifact.id);
        ArtifactCrafter.addCondition(new CondHasYuanying());
        ArtifactCrafter.addTask(ActorTasks.EndJob.id);
        
        CultibookResearcher.addTask(ActorTasks.ImproveCultibook.id);
        CultibookResearcher.addCondition(new CondCanImproveCultibook());
        CultibookResearcher.addCondition(new CondHasCultibook());
        CultibookResearcher.addCondition(new CondHasYuanying());
        CultibookResearcher.addTask(ActorTasks.CreateCultibook.id);
        CultibookResearcher.addCondition(new CondHasCultibook(), false);
        CultibookResearcher.addCondition(new CondHasYuanying());
        CultibookResearcher.addTask(ActorTasks.EndJob.id);

        AddSequentialEqualChanceTasks(
            BookWriter,
            new EqualChanceTaskOption(ActorTasks.WriteCultibook.id, new CondHasCultibook()),
            new EqualChanceTaskOption(ActorTasks.WriteElixirbook.id, new CondHasElixirRecipe()),
            new EqualChanceTaskOption(ActorTasks.WriteSkillbook.id, new CondHasSkill()));
        BookWriter.addTask(ActorTasks.EndJob.id);
        
        SectBuilder.addTask(ActorTasks.BuildSect.id);
        SectBuilder.addCondition(new CondCanFoundSect());
        SectBuilder.addTask(ActorTasks.EndJob.id);

        SectDuty.addTask(ActorTasks.EvaluateSectPersonnel.id);
        SectDuty.addCondition(new CondCanEvaluateSectPersonnel());
        SectDuty.addCondition(new CondProb(0.35f));
        SectDuty.addTask(ActorTasks.RecruitSectMember.id);
        SectDuty.addCondition(new CondCanRecruitSectMember());
        SectDuty.addTask(ActorTasks.EndJob.id);

        SectStudy.addTask(ActorTasks.StudySectScripture.id);
        SectStudy.addCondition(new CondCanStudySectScripture());
        SectStudy.addTask(ActorTasks.EndJob.id);

        SectConstruction.addTask(ActorTasks.TryBuildSectBuilding.id);
        SectConstruction.addCondition(new CondCanBuildSectBuilding());
        SectConstruction.addTask(ActorTasks.EndJob.id);

        SectTreasure.addTask(ActorTasks.ContributeSectTreasure.id);
        SectTreasure.addCondition(new CondCanContributeSectTreasure());
        SectTreasure.addCondition(new CondProb(0.5f));
        SectTreasure.addTask(ActorTasks.ClaimSectTreasure.id);
        SectTreasure.addCondition(new CondCanClaimSectTreasure());
        SectTreasure.addTask(ActorTasks.EndJob.id);

        SectAffair.addTask(ActorTasks.LectureSectCultibook.id);
        SectAffair.addCondition(new CondCanDoSectAffair(SectAffairs.LectureCultibook.id));
        SectAffair.addTask(ActorTasks.OrganizeSectScripture.id);
        SectAffair.addCondition(new CondCanDoSectAffair(SectAffairs.OrganizeScripture.id));
        SectAffair.addTask(ActorTasks.DoSectChore.id);
        SectAffair.addCondition(new CondCanDoSectAffair(SectAffairs.Chore.id));
        SectAffair.addTask(ActorTasks.EndJob.id);
        
        
        Attacker.addTask(ActorTasks.DailyXianCultivate.id);
        Attacker.addCondition(new CondHasXian());
        Attacker.addCondition(new CondCanProgressCultivation(), false);
        Attacker.addCondition(new CondProb(0.4f));
        Attacker.addTask(ActorTasks.CultivationProgression.id);
        Attacker.addCondition(new CondCanProgressCultivation());
        
        SpawnedUnit.addTask(ActorTasks.RandomMove.id);
        SpawnedUnit.addTask(ActorTasks.CallSourceSpawner.id);
        SpawnedUnit.addCondition(new CondHasAliveSourceSpawner(), false);
        SpawnedUnit.addTask(ActorTasks.EndJob.id);

        SkavenGroup.addTask(ActorTasks.CoordinateSkavenPack.id);
        SkavenGroup.addTask(ActorTasks.RandomMove.id);
        SkavenGroup.addTask(ActorTasks.EndJob.id);

        ChaosWarband.addTask(ActorTasks.CoordinateChaosWarband.id);
        ChaosWarband.addTask(ActorTasks.RandomMove.id);
        ChaosWarband.addTask(ActorTasks.EndJob.id);
        
        // 师傅工作
        MasterDuty.addTask(ActorTasks.TeachApprentice.id);
        MasterDuty.addCondition(new CondHasApprentice());
        MasterDuty.addCondition(new CondApprenticeNeedTeaching());
        MasterDuty.addTask(ActorTasks.RecruitApprentice.id);
        MasterDuty.addCondition(new CondCanRecruit());
        MasterDuty.addTask(ActorTasks.EndJob.id);
        
        // 弟子工作
        ApprenticeDuty.addTask(ActorTasks.FollowMaster.id);
        ApprenticeDuty.addCondition(new CondHasMaster());
        ApprenticeDuty.addCondition(new CondMasterCultivating());
        ApprenticeDuty.addTask(ActorTasks.SeekMaster.id);
        ApprenticeDuty.addCondition(new CondNeedMaster());
        ApprenticeDuty.addCondition(new CondHasMaster(), false);
        ApprenticeDuty.addTask(ActorTasks.EndJob.id);
    }

    /// <summary>离体元婴始终抢占普通工作并继续唯一一次夺舍流程。</summary>
    private static bool TrySelectYuanyingPossessionJob(Actor actor, ref string jobId)
    {
        if (!YuanyingPossessionService.IsEscapedSoul(actor)) return false;
        jobId = YuanyingPossession.id;
        return true;
    }

    /// <summary>角色存在可调度进阶时抢占普通随机工作，并返回统一进阶工作标识。</summary>
    private static bool TrySelectCultivationProgressionJob(Actor actor, ref string jobId)
    {
        if (!ProgressionService.CanScheduleAny(actor.GetExtend())) return false;
        jobId = CultivationProgression.id;
        return true;
    }

    /// <summary>只在自然工作选择阶段认领常规工程，不主动打断角色当前任务。</summary>
    private static bool TrySelectCollectiveProjectJob(Actor actor, ref string jobId)
    {
        if (!CollectiveProjectService.TryAssignRoutineJob(actor.GetExtend(), out string selectedJobId))
            return false;
        jobId = selectedJobId;
        return true;
    }

    /// <summary>让已经承担职责的角色优先继续当前协调行动。</summary>
    private static bool TryContinueCoordinatedActivityJob(Actor actor, ref string jobId)
    {
        return CoordinatedActivityService.TryContinueAssignedJob(actor, ref jobId);
    }

    /// <summary>在自然工作选择阶段接受最高优先级的自愿协调邀请。</summary>
    private static bool TrySelectCoordinatedActivityJob(Actor actor, ref string jobId)
    {
        return CoordinatedActivityService.TryAcceptVoluntaryJob(actor, ref jobId);
    }

    private static void AddSequentialEqualChanceTasks(ActorJob job, params EqualChanceTaskOption[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            job.addTask(options[i].TaskId);
            job.addCondition(options[i].Condition);

            int remaining = options.Length - i;
            if (remaining > 1)
            {
                job.addCondition(new CondProb(1f / remaining));
            }
        }
    }

    private readonly struct EqualChanceTaskOption
    {
        public EqualChanceTaskOption(string taskId, BehaviourActorCondition condition)
        {
            TaskId = taskId;
            Condition = condition;
        }

        public string TaskId { get; }
        public BehaviourActorCondition Condition { get; }
    }
}
