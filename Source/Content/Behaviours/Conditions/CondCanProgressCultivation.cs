using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
///     检查角色当前是否有可立即执行或可启动准备阶段的修炼进阶。
/// </summary>
public sealed class CondCanProgressCultivation : BehaviourActorCondition
{
    /// <summary>角色存在已满足条件或可启动准备阶段的候选进阶时返回 true。</summary>
    public override bool check(Actor pActor)
    {
        return ProgressionService.CanScheduleAny(pActor.GetExtend());
    }
}
