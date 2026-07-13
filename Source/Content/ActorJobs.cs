using Cultiway.Abstract;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Behaviours.Conditions;

namespace Cultiway.Content;

[Dependency(typeof(ActorTasks), typeof(SectAffairs))]
public class ActorJobs : ExtendLibrary<ActorJob, ActorJobs>
{
    public static ActorJob XianCultivator      { get; private set; }
    public static ActorJob PlantXianCultivator { get; private set; }
    public static ActorJob WaterCultivator     { get; private set; }
    public static ActorJob MagicCultivator     { get; private set; }
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
        XianCultivator.addTask(ActorTasks.SwitchCultibook.id);
        XianCultivator.addCondition(new CondCanSwitchCultibook());
        XianCultivator.addTask(ActorTasks.DailyXianCultivate.id);
        XianCultivator.addCondition(new CondHasXian());
        XianCultivator.addCondition(new CondXianReadyLevelup(), false);
        XianCultivator.addTask(ActorTasks.LevelupXianCultivate.id);
        XianCultivator.addCondition(new CondXianReadyLevelup());
        XianCultivator.addTask(ActorTasks.EndJob.id);

        PlantXianCultivator.addTask(ActorTasks.SwitchCultibook.id);
        PlantXianCultivator.addCondition(new CondCanSwitchCultibook());
        PlantXianCultivator.addTask(ActorTasks.DailyPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondHasXian());
        PlantXianCultivator.addCondition(new CondXianReadyLevelup(), false);
        PlantXianCultivator.addTask(ActorTasks.LevelupPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondXianReadyLevelup());
        PlantXianCultivator.addTask(ActorTasks.EndJob.id);

        WaterCultivator.addTask(ActorTasks.SwitchCultibook.id);
        WaterCultivator.addCondition(new CondCanSwitchCultibook());
        WaterCultivator.addTask(ActorTasks.DailyWaterCultivate.id);
        WaterCultivator.addCondition(new CondHasXian());
        WaterCultivator.addCondition(new CondXianReadyLevelup(), false);
        WaterCultivator.addTask(ActorTasks.LevelupWaterCultivate.id);
        WaterCultivator.addCondition(new CondXianReadyLevelup());
        WaterCultivator.addTask(ActorTasks.EndJob.id);

        MagicCultivator.addTask(ActorTasks.DailyMagicMeditate.id);
        MagicCultivator.addTask(ActorTasks.EndJob.id);

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
        
        
        Attacker.addTask(ActorTasks.DailyXianCultivate.id);;
        Attacker.addCondition(new CondHasXian());
        Attacker.addCondition(new CondXianReadyLevelup(), false);;
        Attacker.addCondition(new CondProb(0.4f));
        Attacker.addTask(ActorTasks.LevelupXianCultivate.id);
        Attacker.addCondition(new CondXianReadyLevelup());
        
        SpawnedUnit.addTask(ActorTasks.RandomMove.id);
        SpawnedUnit.addTask(ActorTasks.CallSourceSpawner.id);
        SpawnedUnit.addCondition(new CondHasAliveSourceSpawner(), false);
        SpawnedUnit.addTask(ActorTasks.EndJob.id);
        
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
