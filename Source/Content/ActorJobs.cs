using Cultiway.Abstract;
using Cultiway.Content.Behaviours.Conditions;

namespace Cultiway.Content;

[Dependency(typeof(ActorTasks))]
public class ActorJobs : ExtendLibrary<ActorJob, ActorJobs>
{
    public static ActorJob XianCultivator { get; private set; }

    protected override void OnInit()
    {
        XianCultivator = Add(new ActorJob()
        {
            id = nameof(XianCultivator)
        });
        t.addTask(ActorTasks.DailyXianCultivate.id);
        t.addCondition(new CondXianReadyLevelup(), false);
        t.addTask(ActorTasks.LevelupXianCultivate.id);
        t.addCondition(new CondXianReadyLevelup());
        t.addTask("end_job");
    }
}