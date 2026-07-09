using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondMagicReadyLevelup : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return Cultisyses.Magic.PreCheckUpgrade(pActor.GetExtend());
    }
}
