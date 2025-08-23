using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasElixirRecipe : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return pActor.GetExtend().HasMaster<ElixirAsset>();
    }
}