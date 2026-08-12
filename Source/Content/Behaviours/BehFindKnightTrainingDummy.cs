using ai.behaviours;
using Cultiway.Content.KnightCombat;

namespace Cultiway.Content.Behaviours;

/// <summary>将角色所属城市内已完工的训练假人设为操练目标。</summary>
public sealed class BehFindKnightTrainingDummy : BehCityActor
{
    public override BehResult execute(Actor pActor)
    {
        if (!KnightTrainingDummyService.TryFind(pActor, out Building trainingDummy)) return BehResult.Stop;

        pActor.beh_building_target = trainingDummy;
        pActor.beh_tile_target = null;
        return BehResult.Continue;
    }
}
