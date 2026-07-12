using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 检查魔法师当前是否持有可以继续研读或开始学习的魔法卷轴。
/// </summary>
public sealed class CondCanStudyMagicScroll : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return MagicScrollStudyService.ShouldStudy(pActor.GetExtend());
    }
}
