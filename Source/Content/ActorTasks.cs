using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.Behaviours;

namespace Cultiway.Content;

public class ActorTasks : ExtendLibrary<BehaviourTaskActor, ActorTasks>
{
    public static BehaviourTaskActor DailyXianCultivate   { get; private set; }
    public static BehaviourTaskActor LevelupXianCultivate { get; private set; }

    protected override void OnInit()
    {
        DailyXianCultivate = Add(new BehaviourTaskActor()
        {
            id = nameof(DailyXianCultivate)
        });
        t.addBeh(new BehCityFindBuilding("random_house_building"));
        t.addBeh(new BehFindRandomFrontBuildingTile());
        t.addBeh(new BehGoToTileTarget());
        t.addBeh(new BehXianStayInBuildingAndCultivate());
        t.addBeh(new BehExitBuilding());
        LevelupXianCultivate = Add(new BehaviourTaskActor()
        {
            id = nameof(LevelupXianCultivate)
        });
        t.addBeh(new BehCityFindBuilding("random_house_building"));
        t.addBeh(new BehFindRandomFrontBuildingTile());
        t.addBeh(new BehGoToTileTarget());
        t.addBeh(new BehXianStayInBuildingAndLevelup());
        t.addBeh(new BehExitBuilding());
    }
}