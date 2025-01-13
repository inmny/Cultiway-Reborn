using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasXian : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().HasCultisys<Xian>();
    }
}