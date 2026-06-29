using Cultiway.Content.Extensions;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondCanStudySectScripture : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return SectScriptureStudyRules.CanStudySectScripture(pActor);
    }
}
