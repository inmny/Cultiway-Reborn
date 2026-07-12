using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public sealed class CondShouldStudyMagicWeb : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return MagicLearningRules.ShouldStudy(pActor.GetExtend());
    }
}
