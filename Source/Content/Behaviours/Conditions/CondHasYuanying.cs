using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasYuanying : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().TryGetComponent(out Xian xian) && xian.CurrLevel >= XianLevels.Yuanying;
    }
}