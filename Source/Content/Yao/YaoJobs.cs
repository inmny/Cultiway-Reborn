using Cultiway.Abstract;
using Cultiway.Content.Behaviours.Conditions;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

/// <summary>妖兽日常工作的选择器与任务编排。</summary>
public partial class ActorJobs
{
    /// <summary>妖兽日常工作：按优先级执行进阶、吞食、休整与巡行。</summary>
    public static ActorJob YaoLife { get; private set; }

    private static void InitYaoJobs()
    {
        YaoLife.addTask(ActorTasks.YaoProgression.id);
        YaoLife.addCondition(new CondHasYao());
        YaoLife.addCondition(new CondYaoCanProgress());

        YaoLife.addTask(ActorTasks.YaoDevour.id);
        YaoLife.addCondition(new CondHasYao());

        YaoLife.addTask(ActorTasks.YaoMeditate.id);
        YaoLife.addCondition(new CondYaoPowerLow());

        YaoLife.addTask(ActorTasks.RandomMove.id);
        YaoLife.addTask(ActorTasks.EndJob.id);
        ActorJobSelectionRegistry.Register(TrySelectYaoJob, 800);
    }

    /// <summary>已经启灵的妖兽全部转入妖兽日常工作。</summary>
    private static bool TrySelectYaoJob(Actor actor, ref string jobId)
    {
        if (!actor.GetExtend().HasCultisys<Yao>()) return false;
        jobId = YaoLife.id;
        return true;
    }
}
