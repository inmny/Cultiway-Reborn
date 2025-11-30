using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：可以收徒
/// </summary>
public class CondCanRecruit : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        return ae.CanRecruit();
    }
}

