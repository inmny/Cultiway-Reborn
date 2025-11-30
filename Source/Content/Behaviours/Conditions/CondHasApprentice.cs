using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：有弟子
/// </summary>
public class CondHasApprentice : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        var apprentices = ae.GetApprentices();
        return apprentices.Count > 0;
    }
}

