using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasJindan : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().TryGetComponent(out Xian xian) && xian.CurrLevel >= 2;
    }
}