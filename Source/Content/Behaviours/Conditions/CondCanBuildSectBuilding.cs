using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 宗门建造岗位条件，要求成员已经领取建造岗位且宗门存在工地。
/// </summary>
public class CondCanBuildSectBuilding : BehaviourActorCondition
{
    /// <summary>
    /// 检查成员能否继续执行宗门建造任务。
    /// </summary>
    public override bool check(Actor pActor)
    {
        return SectConstructionRules.CanBuildSectBuilding(pActor);
    }
}
