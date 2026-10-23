using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasSkill : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var actor = pActor.GetExtend();
        return actor.all_skills.Count > 0 ||
               actor.E.TryGetComponent(out Cultiway.Content.Skills.ConstructedKnowledge known) && known.Ids.Length > 0;
    }
}