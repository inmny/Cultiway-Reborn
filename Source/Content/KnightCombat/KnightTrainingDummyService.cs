using strings;

namespace Cultiway.Content.KnightCombat;

/// <summary>提供城市内可用于骑士操练的训练假人。</summary>
public static class KnightTrainingDummyService
{
    public static bool TryFind(Actor actor, out Building trainingDummy)
    {
        trainingDummy = null;
        if (actor?.city == null) return false;
        for (int i = 0; i < actor.city.buildings.Count; i++)
        {
            Building building = actor.city.buildings[i];
            if (building.asset.id != SB.training_dummy || !building.isUsable() || building.isUnderConstruction())
                continue;

            trainingDummy = building;
            return true;
        }

        return false;
    }
}
