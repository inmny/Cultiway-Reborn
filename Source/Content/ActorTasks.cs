using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Behaviours.Apprentices;
using Cultiway.Content.Behaviours.Masters;

namespace Cultiway.Content;

public class ActorTasks : ExtendLibrary<BehaviourTaskActor, ActorTasks>
{
    public static BehaviourTaskActor DailyXianCultivate        { get; private set; }
    public static BehaviourTaskActor LevelupXianCultivate      { get; private set; }
    public static BehaviourTaskActor DailyPlantXianCultivate   { get; private set; }
    public static BehaviourTaskActor LevelupPlantXianCultivate { get; private set; }
    public static BehaviourTaskActor DailyWaterCultivate       { get; private set; }
    public static BehaviourTaskActor LevelupWaterCultivate     { get; private set; }
    public static BehaviourTaskActor LookForHerbs { get; private set; }
    public static BehaviourTaskActor CraftElixir  { get; private set; }
    public static BehaviourTaskActor FindNewElixir { get; private set; }
    public static BehaviourTaskActor CraftTalisman { get; private set; }
    public static BehaviourTaskActor CreateCultibook { get; private set; }
    public static BehaviourTaskActor ImproveCultibook { get; private set; }
    public static BehaviourTaskActor BuildSect { get; private set; }
    public static BehaviourTaskActor WriteCultibook { get; private set; }
    public static BehaviourTaskActor WriteElixirbook { get; private set; }
    public static BehaviourTaskActor WriteSkillbook { get; private set; }
    public static BehaviourTaskActor CallSourceSpawner { get; private set; }
    public static BehaviourTaskActor SwitchCultibook { get; private set; }
    public static BehaviourTaskActor TravelToCity { get; private set; }
    public static BehaviourTaskActor FindSectJob { get; private set; }
    public static BehaviourTaskActor EvaluateSectPersonnel { get; private set; }
    public static BehaviourTaskActor RecruitSectMember { get; private set; }
    public static BehaviourTaskActor StudySectScripture { get; private set; }
    public static BehaviourTaskActor DoSectChore { get; private set; }
    public static BehaviourTaskActor TryBuildSectBuilding { get; private set; }
    public static BehaviourTaskActor BuildSectBuilding { get; private set; }
    public static BehaviourTaskActor OrganizeSectScripture { get; private set; }
    public static BehaviourTaskActor LectureSectCultibook { get; private set; }
    
    // 师徒系统任务
    public static BehaviourTaskActor RecruitApprentice { get; private set; }
    public static BehaviourTaskActor TeachApprentice { get; private set; }
    public static BehaviourTaskActor SeekMaster { get; private set; }
    public static BehaviourTaskActor FollowMaster { get; private set; }
    
    [GetOnly("random_move")] public static BehaviourTaskActor RandomMove { get; private set; }

    [GetOnly("end_job")] public static BehaviourTaskActor EndJob { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        DailyXianCultivate.addBeh(new BehBuildingTargetHome());
        DailyXianCultivate.addBeh(new BehGetTargetBuildingMainTile());
        DailyXianCultivate.addBeh(new BehGoToTileTarget());
        DailyXianCultivate.addBeh(new BehStayInBuildingTarget());
        DailyXianCultivate.addBeh(new BehXianCultivate());
        DailyXianCultivate.addBeh(new BehExitBuilding());
        DailyXianCultivate.setIcon("cultiway/icons/iconCultivation");

        LevelupXianCultivate.addBeh(new BehBuildingTargetHome());
        LevelupXianCultivate.addBeh(new BehGetTargetBuildingMainTile());
        LevelupXianCultivate.addBeh(new BehGoToTileTarget());
        LevelupXianCultivate.addBeh(new BehStayInBuildingTarget());
        LevelupXianCultivate.addBeh(new BehXianLevelup());
        LevelupXianCultivate.addBeh(new BehExitBuilding());
        LevelupXianCultivate.setIcon("cultiway/icons/iconCultivation");

        DailyPlantXianCultivate.addBeh(new BehOutdoorCultivationWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        DailyPlantXianCultivate.addBeh(new BehPlantXianCultivate());
        DailyPlantXianCultivate.setIcon("cultiway/icons/iconCultivation");

        LevelupPlantXianCultivate.addBeh(new BehOutdoorCultivationWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        LevelupPlantXianCultivate.addBeh(new BehPlantXianLevelup());
        LevelupPlantXianCultivate.setIcon("cultiway/icons/iconCultivation");
        
        DailyWaterCultivate.addBeh(new BehFindWaterTile());
        DailyWaterCultivate.addBeh(new BehGoToTileTarget());
        DailyWaterCultivate.addBeh(new BehWaterCultivate());
        DailyWaterCultivate.setIcon("cultiway/icons/iconCultivation");

        LevelupWaterCultivate.addBeh(new BehFindWaterTile());
        LevelupWaterCultivate.addBeh(new BehGoToTileTarget());
        LevelupWaterCultivate.addBeh(new BehWaterCultivateLevelup());
        LevelupWaterCultivate.setIcon("cultiway/icons/iconCultivation");
        
        LookForHerbs.addBeh(new BehFindTargetForCollector());
        LookForHerbs.addBeh(new BehGoToActorTarget());
        LookForHerbs.addBeh(new BehHarvestHerb());

        // TODO: 设置图标
        CraftElixir.addBeh(new BehBuildingTargetHome());
        CraftElixir.addBeh(new BehGetTargetBuildingMainTile());
        CraftElixir.addBeh(new BehGoToTileTarget());
        CraftElixir.addBeh(new BehStayInBuildingTarget());
        CraftElixir.addBeh(new BehFindElixirToCraft());
        CraftElixir.addBeh(new BehCraftElixir());
        CraftElixir.addBeh(new BehExitBuilding());
        CraftElixir.setIcon("cultiway/icons/iconElixirCauldron");
        
        FindNewElixir.addBeh(new BehBuildingTargetHome());
        FindNewElixir.addBeh(new BehGetTargetBuildingMainTile());
        FindNewElixir.addBeh(new BehGoToTileTarget());
        FindNewElixir.addBeh(new BehStayInBuildingTarget());
        FindNewElixir.addBeh(new BehFindNewElixir());
        FindNewElixir.addBeh(new BehCraftElixir());
        FindNewElixir.addBeh(new BehExitBuilding());
        FindNewElixir.setIcon("cultiway/icons/iconElixirCauldron");
        
        CreateCultibook.addBeh(new BehBuildingTargetHome());
        CreateCultibook.addBeh(new BehGetTargetBuildingMainTile());
        CreateCultibook.addBeh(new BehGoToTileTarget());
        CreateCultibook.addBeh(new BehStayInBuildingTarget());
        CreateCultibook.addBeh(new BehPrepareCreateCultibook());
        CreateCultibook.addBeh(new BehCreateCultibook());
        CreateCultibook.addBeh(new BehExitBuilding());
        
        BuildSect.addBeh(new BehBuildSect());
        
        CraftTalisman.addBeh(new BehRandomWait(3 * TimeScales.SecPerMonth, 12 * TimeScales.SecPerMonth));
        CraftTalisman.addBeh(new BehCraftTalisman());
        
        WriteCultibook.addBeh(new BehBuildingTargetHome());
        WriteCultibook.addBeh(new BehGetTargetBuildingMainTile());
        WriteCultibook.addBeh(new BehGoToTileTarget());
        WriteCultibook.addBeh(new BehStayInBuildingTarget());
        WriteCultibook.addBeh(new BehWriteCultibook());
        WriteCultibook.addBeh(new BehExitBuilding());
        WriteCultibook.addBeh(new BehEndJob());
        WriteCultibook.setIcon("cultiway/icons/iconWriting");
        
        WriteElixirbook.addBeh(new BehBuildingTargetHome());
        WriteElixirbook.addBeh(new BehGetTargetBuildingMainTile());
        WriteElixirbook.addBeh(new BehGoToTileTarget());
        WriteElixirbook.addBeh(new BehStayInBuildingTarget());
        WriteElixirbook.addBeh(new BehWriteElixirRecipe());
        WriteElixirbook.addBeh(new BehExitBuilding());
        WriteElixirbook.addBeh(new BehEndJob());
        WriteElixirbook.setIcon("cultiway/icons/iconWriting");
        
        WriteSkillbook.addBeh(new BehBuildingTargetHome());
        WriteSkillbook.addBeh(new BehGetTargetBuildingMainTile());
        WriteSkillbook.addBeh(new BehGoToTileTarget());
        WriteSkillbook.addBeh(new BehStayInBuildingTarget());
        WriteSkillbook.addBeh(new BehWriteSkillbook());
        WriteSkillbook.addBeh(new BehExitBuilding());
        WriteSkillbook.addBeh(new BehEndJob());
        WriteSkillbook.setIcon("cultiway/icons/iconWriting");
        
        CallSourceSpawner.addBeh(new BehHoldSimpleCeremony());
        CallSourceSpawner.addBeh(new BehCallSourceSpawner());
        CallSourceSpawner.addBeh(new BehEndJob());
        
        SwitchCultibook.addBeh(new BehSwitchCultibook());
        SwitchCultibook.addBeh(new BehEndJob());
        SwitchCultibook.setIcon("cultiway/icons/iconCultivation");

        TravelToCity.addBeh(new BehTravelToCity());
        TravelToCity.addBeh(new BehGoToTileTarget());
        TravelToCity.addBeh(new BehRandomWait(5f, 15f));
        TravelToCity.addBeh(new BehEndJob());
        TravelToCity.setIcon("cultiway/icons/plots/iconTrainNet");

        FindSectJob.addBeh(new BehFindSectJob());
        FindSectJob.setIcon("ui/Icons/iconShowTasks");

        EvaluateSectPersonnel.addBeh(new BehEvaluateSectPersonnel());
        EvaluateSectPersonnel.addBeh(new BehEndJob());
        EvaluateSectPersonnel.setIcon("ui/icons/iconInterestingPeople");

        RecruitSectMember.addBeh(new BehRecruitSectMember());
        RecruitSectMember.addBeh(new BehEndJob());
        RecruitSectMember.setIcon("cultiway/icons/iconMasterApprentice");

        StudySectScripture.force_hand_tool = "book";
        StudySectScripture.cancellable_by_reproduction = true;
        StudySectScripture.cancellable_by_socialize = true;
        StudySectScripture.addBeh(new BehFindSectResidenceTile("StudyScriptureTarget"));
        StudySectScripture.addBeh(new BehGoToTileTarget());
        StudySectScripture.addBeh(new BehTryReadSectScripture());
        StudySectScripture.addBeh(new BehRandomWait(TimeScales.SecPerMonth, TimeScales.SecPerMonth * 3, true));
        StudySectScripture.addBeh(new BehFinishReading());
        StudySectScripture.addBeh(new BehEndJob());
        StudySectScripture.setIcon("ui/icons/iconBooks");

        DoSectChore.force_hand_tool = "hammer";
        DoSectChore.cancellable_by_reproduction = true;
        DoSectChore.cancellable_by_socialize = true;
        DoSectChore.addBeh(new BehFindSectChoreTile());
        DoSectChore.addBeh(new BehGoToTileTarget());
        DoSectChore.addBeh(new BehRandomWait(0.5f, 1.2f, true));
        DoSectChore.addBeh(new BehAngleAnimation(AngleAnimationTarget.Tile, "event:/SFX/CIVILIZATIONS/BuildRoad", 0.8f, 40f, true, true));
        DoSectChore.addBeh(new BehAngleAnimation(AngleAnimationTarget.Tile, "event:/SFX/CIVILIZATIONS/BuildRoad", 0.8f, 40f, true, true));
        DoSectChore.addBeh(new BehAngleAnimation(AngleAnimationTarget.Tile, "event:/SFX/CIVILIZATIONS/BuildRoad", 0f, 40f, true, true));
        DoSectChore.addBeh(new BehDoSectChore());
        DoSectChore.addBeh(new BehEndJob());
        DoSectChore.setIcon("ui/icons/iconBuildings");

        TryBuildSectBuilding.force_hand_tool = "hammer";
        TryBuildSectBuilding.cancellable_by_reproduction = true;
        TryBuildSectBuilding.cancellable_by_socialize = true;
        TryBuildSectBuilding.addBeh(new BehFindSectConstructionBuilding());
        TryBuildSectBuilding.addBeh(new BehSetNextTask(BuildSectBuilding.id, false, true));
        TryBuildSectBuilding.setIcon("ui/Icons/citizen_jobs/iconCitizenJobBuilder");

        BuildSectBuilding.force_hand_tool = "hammer";
        BuildSectBuilding.cancellable_by_reproduction = true;
        BuildSectBuilding.cancellable_by_socialize = true;
        BuildSectBuilding.addBeh(new BehCheckSectBuildingUnderConstruction());
        BuildSectBuilding.addBeh(new BehFindConstructionTile());
        BuildSectBuilding.addBeh(new BehGoToTileTarget());
        for (int i = 0; i < 5; i++)
        {
            BuildSectBuilding.addBeh(new BehCheckSectBuildingUnderConstruction());
            BuildSectBuilding.addBeh(new BehLookAtBuildingTarget());
            BuildSectBuilding.addBeh(new BehAngleAnimation(AngleAnimationTarget.Building, "event:/SFX/CIVILIZATIONS/BuildRoad", 0f, 40f, true, true));
            BuildSectBuilding.addBeh(new BehBuildSectTarget());
        }
        BuildSectBuilding.addBeh(new BehRestartTask());
        BuildSectBuilding.setIcon("ui/Icons/citizen_jobs/iconCitizenJobBuilder");

        OrganizeSectScripture.force_hand_tool = "book";
        OrganizeSectScripture.cancellable_by_reproduction = true;
        OrganizeSectScripture.cancellable_by_socialize = true;
        OrganizeSectScripture.addBeh(new BehFindSectResidenceTile("OrganizeScriptureTarget"));
        OrganizeSectScripture.addBeh(new BehGoToTileTarget());
        OrganizeSectScripture.addBeh(new BehRandomWait(TimeScales.SecPerMonth, TimeScales.SecPerMonth * 2, true));
        OrganizeSectScripture.addBeh(new BehAngleAnimation(AngleAnimationTarget.Tile, null, 0.8f, 25f, true, true));
        OrganizeSectScripture.addBeh(new BehOrganizeSectScripture());
        OrganizeSectScripture.addBeh(new BehEndJob());
        OrganizeSectScripture.setIcon("ui/icons/iconBooks");

        LectureSectCultibook.force_hand_tool = "book";
        LectureSectCultibook.cancellable_by_reproduction = true;
        LectureSectCultibook.cancellable_by_socialize = true;
        LectureSectCultibook.addBeh(new BehFindSectResidenceTile("SectLectureTarget"));
        LectureSectCultibook.addBeh(new BehGoToTileTarget());
        LectureSectCultibook.addBeh(new BehSpawnSlashTalkTowardTileTarget());
        LectureSectCultibook.addBeh(new BehRandomWait(TimeScales.SecPerMonth, TimeScales.SecPerMonth * 2, true));
        LectureSectCultibook.addBeh(new BehLectureSectCultibook());
        LectureSectCultibook.addBeh(new BehEndJob());
        LectureSectCultibook.setIcon("cultiway/icons/iconCultivation");
        
        ImproveCultibook.addBeh(new BehBuildingTargetHome());
        ImproveCultibook.addBeh(new BehGetTargetBuildingMainTile());
        ImproveCultibook.addBeh(new BehGoToTileTarget());
        ImproveCultibook.addBeh(new BehStayInBuildingTarget());
        ImproveCultibook.addBeh(new BehPrepareImproveCultibook());
        ImproveCultibook.addBeh(new BehImproveCultibook());
        ImproveCultibook.addBeh(new BehExitBuilding());
        ImproveCultibook.addBeh(new BehEndJob());
        ImproveCultibook.setIcon("cultiway/icons/iconCultivation");
        
        // 师徒系统任务
        RecruitApprentice.addBeh(new BehRecruitApprentice());
        RecruitApprentice.addBeh(new BehEndJob());
        RecruitApprentice.setIcon("cultiway/icons/iconMasterApprentice");
        
        TeachApprentice.addBeh(new BehTeachApprentice());
        TeachApprentice.addBeh(new BehEndJob());
        TeachApprentice.setIcon("cultiway/icons/iconMasterApprentice");
        
        SeekMaster.addBeh(new BehSeekMaster());
        SeekMaster.addBeh(new BehEndJob());
        SeekMaster.setIcon("cultiway/icons/iconMasterApprentice");
        
        FollowMaster.addBeh(new BehFollowMaster());
        FollowMaster.addBeh(new BehEndJob());
        FollowMaster.setIcon("cultiway/icons/iconMasterApprentice");
    }
    protected override void PostInit(BehaviourTaskActor asset)
    {
        base.PostInit(asset);
        if (!string.IsNullOrEmpty(asset.force_hand_tool))
        {
            asset.cached_hand_tool_asset = AssetManager.unit_hand_tools.get(asset.force_hand_tool);
        }
    }
}
