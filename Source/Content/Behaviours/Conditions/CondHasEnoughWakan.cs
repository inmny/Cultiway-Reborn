using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasEnoughWakan(float percent = 0.01f) : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        ref var xian = ref pActor.GetExtend().GetCultisys<Xian>();
        var max_wakan = pActor.stats[BaseStatses.MaxWakan.id];
        if (xian.wakan < max_wakan * percent)
        {
            return false;
        }

        return true;
    }
}