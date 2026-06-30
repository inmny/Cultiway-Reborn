using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 宗门事务任务条件，按事务资产 id 判断当前单位是否可以继续执行。
/// </summary>
public class CondCanDoSectAffair : BehaviourActorCondition
{
    private readonly string _affairId;

    /// <summary>
    /// 创建指定宗门事务的执行条件。
    /// </summary>
    public CondCanDoSectAffair(string affairId)
    {
        _affairId = affairId;
    }

    /// <summary>
    /// 检查单位是否满足该宗门事务的资产规则。
    /// </summary>
    public override bool check(Actor pActor)
    {
        return SectAffairRules.CanDoSectAffair(pActor, SectAffairRules.GetAffair(_affairId));
    }
}
