using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasSect : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().sect != null;
    }
}