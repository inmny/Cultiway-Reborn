using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasCultibook : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().HasCultibook();
    }
}