using Cultiway.Abstract;
using ai.behaviours;
using Cultiway.Content.Behaviours;
using Cultiway.Content.Behaviours.Conditions;

namespace Cultiway.Content;

/// <summary>妖兽玩法的工作任务：进阶、吞食精华、灵气休整与巡行。</summary>
public partial class ActorTasks
{
    /// <summary>推进妖修候选过渡的专用任务。</summary>
    public static BehaviourTaskActor YaoProgression { get; private set; }
    /// <summary>寻路到精华附近并吞食的任务。</summary>
    public static BehaviourTaskActor YaoDevour { get; private set; }
    /// <summary>在灵气地点休整恢复妖力的任务。</summary>
    public static BehaviourTaskActor YaoMeditate { get; private set; }

    private static void InitYaoTasks()
    {
        YaoProgression.addBeh(new BehYaoProgression());
        YaoProgression.addBeh(new BehEndJob());
        YaoProgression.setIcon("cultiway/icons/iconCultivation");

        YaoDevour.addBeh(new BehYaoFindDeposit());
        YaoDevour.addBeh(new BehGoToTileTarget());
        YaoDevour.addBeh(new BehYaoDevour());
        YaoDevour.addBeh(new BehEndJob());
        YaoDevour.setIcon("cultiway/icons/iconCultivation");

        YaoMeditate.addBeh(new BehYaoMeditate());
        YaoMeditate.addBeh(new BehEndJob());
        YaoMeditate.setIcon("cultiway/icons/iconCultivation");
    }
}
