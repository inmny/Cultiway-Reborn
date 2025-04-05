using ai.behaviours;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehFindTargetForCollector : BehCityActor
{
    public override BehResult execute(Actor pActor)
    {
        if (pActor.beh_actor_target != null && isTargetOk(pActor, pActor.beh_actor_target.a)) return BehResult.Continue;

        pActor.beh_actor_target = GetClosestPlantActor(pActor, 3);
        if (pActor.beh_actor_target != null) return BehResult.Continue;

        return BehResult.Stop;
    }

    private Actor GetClosestPlantActor(Actor collector, int min_level = 2)
    {
        temp_actors.Clear();
        foreach (var actor in Finder.getUnitsFromChunk(collector.current_tile, 3))
        {
            if (isTargetOk(collector, actor) && actor.asset == Actors.Plant &&
                actor.GetExtend().GetPowerLevel() >= min_level &&
                collector.GetExtend().GetPowerLevel() >= actor.GetExtend().GetPowerLevel())
                temp_actors.Add(actor);
        }

        return Toolbox.getClosestActor(temp_actors, collector.current_tile);
    }

    private bool isTargetOk(Actor pActor, Actor pTarget)
    {
        return !(pTarget == pActor) && pActor.canAttackTarget(pTarget) &&
               pTarget.current_tile.isSameIsland(pActor.current_tile);
    }
}