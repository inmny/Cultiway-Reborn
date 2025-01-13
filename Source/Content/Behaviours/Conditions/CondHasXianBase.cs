using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasXianBase : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().HasComponent<XianBase>();
    }
}