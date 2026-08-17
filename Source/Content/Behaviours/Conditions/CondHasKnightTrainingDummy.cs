using Cultiway.Content.KnightCombat;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>条件：所属城市有可用训练假人。</summary>
public sealed class CondHasKnightTrainingDummy : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return KnightTrainingDummyService.TryFind(pActor, out _);
    }
}
