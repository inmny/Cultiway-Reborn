using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：有师傅
/// </summary>
public class CondHasMaster : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        return ae.HasMaster();
    }
}

