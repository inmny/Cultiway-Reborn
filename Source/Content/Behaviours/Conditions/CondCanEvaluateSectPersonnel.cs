using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondCanEvaluateSectPersonnel : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return SectPersonnelEvaluator.CanManageSectPersonnel(pActor);
    }
}
