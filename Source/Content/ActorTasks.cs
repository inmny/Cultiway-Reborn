using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Behaviours;

namespace Cultiway.Content;

public class ActorTasks : ExtendLibrary<BehaviourTaskActor, ActorTasks>
{
    public static BehaviourTaskActor DailyXianCultivate        { get; private set; }
    public static BehaviourTaskActor LevelupXianCultivate      { get; private set; }
    public static BehaviourTaskActor DailyPlantXianCultivate   { get; private set; }
    public static BehaviourTaskActor LevelupPlantXianCultivate { get; private set; }
    public static BehaviourTaskActor LookForHerbs { get; private set; }
    public static BehaviourTaskActor CraftElixir  { get; private set; }
    public static BehaviourTaskActor FindNewElixir { get; private set; }
    public static BehaviourTaskActor CraftTalisman { get; private set; }
    public static BehaviourTaskActor CreateCultibook { get; private set; }
    public static BehaviourTaskActor ImproveCultibook { get; private set; }

    [GetOnly("random_move")] public static BehaviourTaskActor RandomMove { get; private set; }

    [GetOnly("end_job")] public static BehaviourTaskActor EndJob { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ActorTasks");
        DailyXianCultivate.addBeh(new BehCityFindBuilding("random_house_building"));
        DailyXianCultivate.addBeh(new BehFindRandomFrontBuildingTile());
        DailyXianCultivate.addBeh(new BehGoToTileTarget());
        DailyXianCultivate.addBeh(new BehStayInBuildingTarget());
        DailyXianCultivate.addBeh(new BehXianCultivate());
        DailyXianCultivate.addBeh(new BehExitBuilding());

        LevelupXianCultivate.addBeh(new BehCityFindBuilding("random_house_building"));
        LevelupXianCultivate.addBeh(new BehFindRandomFrontBuildingTile());
        LevelupXianCultivate.addBeh(new BehGoToTileTarget());
        LevelupXianCultivate.addBeh(new BehStayInBuildingTarget());
        LevelupXianCultivate.addBeh(new BehXianLevelup());
        LevelupXianCultivate.addBeh(new BehExitBuilding());

        DailyPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        DailyPlantXianCultivate.addBeh(new BehPlantXianCultivate());

        LevelupPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        LevelupPlantXianCultivate.addBeh(new BehPlantXianLevelup());

        LookForHerbs.addBeh(new BehFindTargetForCollector());
        LookForHerbs.addBeh(new BehGoToActorTarget());
        LookForHerbs.addBeh(new BehHarvestHerb());

        CraftElixir.addBeh(new BehCityFindBuilding("random_house_building"));
        CraftElixir.addBeh(new BehFindRandomFrontBuildingTile());
        CraftElixir.addBeh(new BehGoToTileTarget());
        CraftElixir.addBeh(new BehStayInBuildingTarget());
        CraftElixir.addBeh(new BehFindElixirToCraft());
        CraftElixir.addBeh(new BehCraftElixir());
        CraftElixir.addBeh(new BehExitBuilding());
        
        FindNewElixir.addBeh(new BehCityFindBuilding("random_house_building"));
        FindNewElixir.addBeh(new BehFindRandomFrontBuildingTile());
        FindNewElixir.addBeh(new BehGoToTileTarget());
        FindNewElixir.addBeh(new BehStayInBuildingTarget());
        FindNewElixir.addBeh(new BehFindNewElixir());
        FindNewElixir.addBeh(new BehCraftElixir());
        FindNewElixir.addBeh(new BehExitBuilding());
        
        CreateCultibook.addBeh(new BehCityFindBuilding("random_house_building"));
        CreateCultibook.addBeh(new BehFindRandomFrontBuildingTile());
        CreateCultibook.addBeh(new BehGoToTileTarget());
        CreateCultibook.addBeh(new BehStayInBuildingTarget());
        CreateCultibook.addBeh(new BehCreateCultibook());
        CreateCultibook.addBeh(new BehExitBuilding());
        
        CraftTalisman.addBeh(new BehRandomWait(3 * TimeScales.SecPerMonth, 12 * TimeScales.SecPerMonth));
        CraftTalisman.addBeh(new BehCraftTalisman());
    }
}