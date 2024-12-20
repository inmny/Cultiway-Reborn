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

    [GetOnly("random_move")] public static BehaviourTaskActor RandomMove { get; private set; }

    [GetOnly("end_job")] public static BehaviourTaskActor EndJob { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ActorTasks");
        DailyXianCultivate.addBeh(new BehCityFindBuilding("random_house_building"));
        DailyXianCultivate.addBeh(new BehFindRandomFrontBuildingTile());
        DailyXianCultivate.addBeh(new BehGoToTileTarget());
        DailyXianCultivate.addBeh(new BehXianStayInBuildingAndCultivate());
        DailyXianCultivate.addBeh(new BehExitBuilding());

        LevelupXianCultivate.addBeh(new BehCityFindBuilding("random_house_building"));
        LevelupXianCultivate.addBeh(new BehFindRandomFrontBuildingTile());
        LevelupXianCultivate.addBeh(new BehGoToTileTarget());
        LevelupXianCultivate.addBeh(new BehXianStayInBuildingAndLevelup());
        LevelupXianCultivate.addBeh(new BehExitBuilding());

        DailyPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        DailyPlantXianCultivate.addBeh(new BehPlantXianCultivate());

        LevelupPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        LevelupPlantXianCultivate.addBeh(new BehPlantXianLevelup());

        LookForHerbs.addBeh(new BehFindTargetForCollector());
        LookForHerbs.addBeh(new BehGoToActorTarget());
        LookForHerbs.addBeh(new BehHarvestHerb());
    }
}