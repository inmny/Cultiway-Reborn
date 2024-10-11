using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondXianReadyLevelup : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return Cultisyses.Xian.PreCheckUpgrade(pActor.GetExtend());
    }
}