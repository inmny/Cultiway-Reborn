using ai.behaviours;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public sealed class BehFollowSkavenLeader : BehaviourActionActor
{
    private const int PatrolRadius = 16;

    private static readonly (int x, int y)[] FormationOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),           (1, 0),
        (-1, 1),  (0, 1),  (1, 1),
        (-2, 0),  (2, 0),  (0, -2), (0, 2), (-2, -2)
    };

    public override BehResult execute(Actor pObject)
    {
        var source = World.world.buildings.get(pObject.GetSourceSpawnerId());
        if (source == null || source.isRekt() || source.asset != Buildings.SkavenBlight ||
            !SkavenEvolution.TryGetLeader(pObject, out var leader))
        {
            return BehResult.Continue;
        }

        if (!SkavenEvolution.ShouldPatrol(pObject, source))
        {
            ReturnToNest(pObject, source);
            return BehResult.RepeatStep;
        }

        if (pObject.is_inside_building) pObject.exitBuilding();
        if (leader == pObject)
        {
            SkavenEvolution.UpdatePatrolCombatState(pObject, source);
            PatrolAroundNest(pObject, source);
            return BehResult.RepeatStep;
        }

        var offset = FormationOffsets[SkavenEvolution.GetOrAssignFormationSlot(pObject)];
        var target = World.world.GetTile(leader.current_tile.x + offset.x, leader.current_tile.y + offset.y);
        if (target == null || !target.isSameIsland(leader.current_tile))
        {
            target = leader.current_tile.getTileAroundThisOnSameIsland(pObject.current_tile, true);
        }
        if (target != null && pObject.current_tile != target)
        {
            pObject.goTo(target);
        }
        return BehResult.RepeatStep;
    }

    private static void ReturnToNest(Actor actor, Building source)
    {
        if (actor.is_inside_building && actor.inside_building == source) return;
        if (actor.is_inside_building) actor.exitBuilding();

        if (Toolbox.SquaredDistTile(actor.current_tile, source.current_tile) <= 2)
        {
            actor.stopMovement();
            actor.stayInBuilding(source);
        }
        else if (!actor.is_moving)
        {
            actor.goTo(source.current_tile);
        }
    }

    private static void PatrolAroundNest(Actor actor, Building source)
    {
        if (actor.has_attack_target || actor.attackedBy != null && !actor.attackedBy.isRekt() || actor.is_moving) return;

        for (var i = 0; i < 12; i++)
        {
            var x = source.current_tile.x + Randy.randomInt(-PatrolRadius, PatrolRadius + 1);
            var y = source.current_tile.y + Randy.randomInt(-PatrolRadius, PatrolRadius + 1);
            var target = World.world.GetTile(x, y);
            if (target == null || !target.isSameIsland(source.current_tile)) continue;
            actor.goTo(target);
            return;
        }
    }
}
