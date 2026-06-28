using Cultiway.Content;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondCanFoundSect : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return SectRules.CanFoundSect(pActor);
    }
}
