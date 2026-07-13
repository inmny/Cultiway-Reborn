using Cultiway.Content.Sects;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 判断成员当前是否有需要且能够负担的宗门库藏。
/// </summary>
public class CondCanClaimSectTreasure : BehaviourActorCondition
{
    /// <summary>
    /// 检查物品需求、访问权限和可用贡献。
    /// </summary>
    public override bool check(Actor pActor)
    {
        return SectTreasurePlanner.TryPickClaim(pActor, out _);
    }
}
