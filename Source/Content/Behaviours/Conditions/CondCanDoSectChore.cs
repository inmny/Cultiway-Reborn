using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 宗门杂务任务条件，只有符合杂务规则的宗门弟子才会继续执行。
/// </summary>
public class CondCanDoSectChore : BehaviourActorCondition
{
    /// <summary>
    /// 检查单位是否可以执行宗门杂务。
    /// </summary>
    public override bool check(Actor pActor)
    {
        return SectChoreRules.CanDoSectChore(pActor);
    }
}
