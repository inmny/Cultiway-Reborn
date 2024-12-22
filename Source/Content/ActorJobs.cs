using Cultiway.Abstract;
using Cultiway.Content.Behaviours.Conditions;

namespace Cultiway.Content;

[Dependency(typeof(ActorTasks))]
public class ActorJobs : ExtendLibrary<ActorJob, ActorJobs>
{
    public static ActorJob XianCultivator      { get; private set; }
    public static ActorJob PlantXianCultivator { get; private set; }
    public static ActorJob HerbCollector { get; private set; }
    public static ActorJob ElixirCrafter { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ActorJob");
        XianCultivator.addTask(ActorTasks.DailyXianCultivate.id);
        XianCultivator.addCondition(new CondXianReadyLevelup(), false);
        XianCultivator.addTask(ActorTasks.LevelupXianCultivate.id);
        XianCultivator.addCondition(new CondXianReadyLevelup());
        XianCultivator.addTask(ActorTasks.EndJob.id);

        PlantXianCultivator.addTask(ActorTasks.DailyPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondXianReadyLevelup(), false);
        PlantXianCultivator.addTask(ActorTasks.LevelupPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondXianReadyLevelup());
        PlantXianCultivator.addTask(ActorTasks.EndJob.id);

        HerbCollector.addTask(ActorTasks.RandomMove.id);
        HerbCollector.addTask(ActorTasks.LookForHerbs.id);
        HerbCollector.addTask(ActorTasks.EndJob.id);

        ElixirCrafter.addTask(ActorTasks.CraftElixir.id);
        ElixirCrafter.addCondition(new CondHasJindan());
        ElixirCrafter.addTask(ActorTasks.EndJob.id);
    }
}