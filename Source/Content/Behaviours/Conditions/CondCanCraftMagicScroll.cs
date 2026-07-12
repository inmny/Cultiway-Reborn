using Cultiway.Content.Behaviours;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 检查单位是否掌握至少一个当前能够支付制作成本的 mana 法术。
/// </summary>
public sealed class CondCanCraftMagicScroll : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return BehCraftMagicScroll.CanCraft(pActor.GetExtend());
    }
}
