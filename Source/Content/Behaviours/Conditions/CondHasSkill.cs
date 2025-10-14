using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasSkill : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().all_skills.Count > 0;
    }
}