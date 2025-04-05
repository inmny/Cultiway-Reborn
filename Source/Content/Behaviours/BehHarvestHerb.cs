using ai.behaviours;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehHarvestHerb : BehaviourActionActor
{
    public override void create()
    {
        base.create();
        null_check_actor_target = true;
    }

    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (pActor.isInAttackRange(pActor.beh_actor_target)) pActor.tryToAttack(pActor.beh_actor_target);

        if (pActor.beh_actor_target.isAlive()) return BehResult.RestartTask;

        ModClass.LogInfo($"[{pActor.city?.name}] Harvested herb");
        return BehResult.Continue;
    }
}