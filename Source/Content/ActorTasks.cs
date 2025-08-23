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
    public static BehaviourTaskActor BuildSect { get; private set; }
    public static BehaviourTaskActor WriteCultibook { get; private set; }
    public static BehaviourTaskActor WriteElixirbook { get; private set; }
    [GetOnly("random_move")] public static BehaviourTaskActor RandomMove { get; private set; }

    [GetOnly("end_job")] public static BehaviourTaskActor EndJob { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();
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

        DailyPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        DailyPlantXianCultivate.addBeh(new BehPlantXianCultivate());
        DailyPlantXianCultivate.setIcon("cultiway/icons/iconCultivation");

        LevelupPlantXianCultivate.addBeh(new BehRandomWait(TimeScales.SecPerYear, TimeScales.SecPerYear * 5));
        LevelupPlantXianCultivate.addBeh(new BehPlantXianLevelup());
        LevelupPlantXianCultivate.setIcon("cultiway/icons/iconCultivation");
        
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
        
        FindNewElixir.addBeh(new BehBuildingTargetHome());
        FindNewElixir.addBeh(new BehGetTargetBuildingMainTile());
        FindNewElixir.addBeh(new BehGoToTileTarget());
        FindNewElixir.addBeh(new BehStayInBuildingTarget());
        FindNewElixir.addBeh(new BehFindNewElixir());
        FindNewElixir.addBeh(new BehCraftElixir());
        FindNewElixir.addBeh(new BehExitBuilding());
        
        CreateCultibook.addBeh(new BehBuildingTargetHome());
        CreateCultibook.addBeh(new BehGetTargetBuildingMainTile());
        CreateCultibook.addBeh(new BehGoToTileTarget());
        CreateCultibook.addBeh(new BehStayInBuildingTarget());
        CreateCultibook.addBeh(new BehCreateCultibook());
        CreateCultibook.addBeh(new BehExitBuilding());
        
        BuildSect.addBeh(new BehBuildSect());
        
        CraftTalisman.addBeh(new BehRandomWait(3 * TimeScales.SecPerMonth, 12 * TimeScales.SecPerMonth));
        CraftTalisman.addBeh(new BehCraftTalisman());
    }
}