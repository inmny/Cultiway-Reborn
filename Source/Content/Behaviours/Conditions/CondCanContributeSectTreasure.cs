using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 判断成员当前是否有可贡献给宗门藏宝阁的物品。
/// </summary>
public class CondCanContributeSectTreasure : BehaviourActorCondition
{
    /// <summary>
    /// 检查贡献权限、库存保留量和宗门剩余容量。
    /// </summary>
    public override bool check(Actor pActor)
    {
        return SectTreasureRules.TryPickContributionItem(pActor, out _);
    }
}
