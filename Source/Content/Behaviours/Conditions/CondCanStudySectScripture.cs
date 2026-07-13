using Cultiway.Content.Sects;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondCanStudySectScripture : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return SectScriptureStudyPlanner.CanPlan(pActor);
    }
}
