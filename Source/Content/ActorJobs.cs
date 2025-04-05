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
    public static ActorJob ElixirFinder { get; private set; }
    public static ActorJob TalismanCrafter { get; private set; }
    public static ActorJob CultibookResearcher { get; private set; }
    [GetOnly("attacker")]
    public static ActorJob Attacker { get; private set; }
    [GetOnly("random_move")]
    public static ActorJob RandomMove { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ActorJob");
        XianCultivator.addTask(ActorTasks.DailyXianCultivate.id);
        XianCultivator.addCondition(new CondHasXian());
        XianCultivator.addCondition(new CondXianReadyLevelup(), false);
        XianCultivator.addTask(ActorTasks.LevelupXianCultivate.id);
        XianCultivator.addCondition(new CondXianReadyLevelup());
        XianCultivator.addTask(ActorTasks.EndJob.id);

        PlantXianCultivator.addTask(ActorTasks.DailyPlantXianCultivate.id);
        PlantXianCultivator.addCondition(new CondHasXian());
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
        
        ElixirFinder.addTask(ActorTasks.FindNewElixir.id);
        ElixirFinder.addCondition(new CondHasJindan());
        ElixirFinder.addTask(ActorTasks.EndJob.id);
        
        TalismanCrafter.addTask(ActorTasks.CraftTalisman.id);
        TalismanCrafter.addCondition(new CondHasXian());
        TalismanCrafter.addCondition(new CondHasXianBase());
        TalismanCrafter.addCondition(new CondHasEnoughWakan());
        TalismanCrafter.addTask(ActorTasks.EndJob.id);
        
        CultibookResearcher.addTask(ActorTasks.ImproveCultibook.id);
        CultibookResearcher.addCondition(new CondHasCultibook());
        CultibookResearcher.addCondition(new CondHasYuanying());
        CultibookResearcher.addTask(ActorTasks.CreateCultibook.id);
        CultibookResearcher.addCondition(new CondHasCultibook(), false);
        CultibookResearcher.addCondition(new CondHasYuanying());
        CultibookResearcher.addTask(ActorTasks.EndJob.id);
        
        
        Attacker.addTask(ActorTasks.DailyXianCultivate.id);;
        Attacker.addCondition(new CondHasXian());
        Attacker.addCondition(new CondXianReadyLevelup(), false);;
        Attacker.addCondition(new CondProb(0.4f));
        Attacker.addTask(ActorTasks.LevelupXianCultivate.id);
        Attacker.addCondition(new CondXianReadyLevelup());
    }
}