using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

public class CondHasAliveSourceSpawner : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return base.check(pActor) && !World.world.buildings.get(pActor.GetSourceSpawnerId()).isRekt();
    }
}