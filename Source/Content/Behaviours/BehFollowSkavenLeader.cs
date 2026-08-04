using ai.behaviours;

namespace Cultiway.Content.Behaviours;

public sealed class BehFollowSkavenLeader : BehaviourActionActor
{
    private static readonly (int x, int y)[] FormationOffsets =
    {
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),           (1, 0),
        (-1, 1),  (0, 1),  (1, 1),
        (-2, 0),  (2, 0),  (0, -2), (0, 2), (-2, -2)
    };

    public override BehResult execute(Actor pObject)
    {
        if (!SkavenEvolution.TryGetLeader(pObject, out var leader) || leader == pObject)
        {
            return BehResult.Continue;
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
}
