using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondCanRecruitSectMember : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return SectPersonnelEvaluator.CanRecruitExternalMember(pActor);
    }
}
