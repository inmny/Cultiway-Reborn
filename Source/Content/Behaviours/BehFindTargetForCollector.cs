using ai.behaviours;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehFindTargetForCollector : BehCity
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
        world.getObjectsInChunks(collector.currentTile, 10, MapObjectType.Actor);
        for (var i = 0; i < world.temp_map_objects.Count; i++)
        {
            var actor = (Actor)world.temp_map_objects[i];
            if (isTargetOk(collector, actor) && actor.asset == Actors.Plant && actor.GetExtend().GetPowerLevel() >= min_level)
                temp_actors.Add(actor);
        }

        return Toolbox.getClosestActor(temp_actors, collector.currentTile);
    }

    private bool isTargetOk(Actor pActor, Actor pTarget)
    {
        return !(pTarget == pActor) && pActor.canAttackTarget(pTarget) &&
               pTarget.currentTile.isSameIsland(pActor.currentTile);
    }
}