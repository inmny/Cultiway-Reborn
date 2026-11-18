using strings;

namespace Cultiway.Content.KnightCombat;

/// <summary>提供城市内可用于骑士操练的训练假人。</summary>
public static class KnightTrainingDummyService
{
    /// <summary>查找当前城市能使用的训练假人。</summary>
    /// <param name="actor">准备出发的人物。</param>
    /// <param name="trainingDummy">实际找到的已完工假人。</param>
    public static bool TryFind(Actor actor, out Building trainingDummy)
    {
        trainingDummy = null;
        if (actor?.city == null) return false;
        for (int i = 0; i < actor.city.buildings.Count; i++)
        {
            Building building = actor.city.buildings[i];
            if (!IsReady(building)) continue;

            trainingDummy = building;
            return true;
        }

        return false;
    }
    /// <summary>训练途中核对同一座已完工假人仍属于当前城市。</summary>
    /// <param name="actor">仍在训练的人物。</param>
    /// <param name="dummy">实际训练目标。</param>
    internal static bool IsUsable(Actor actor, Building dummy) => actor.city != null && IsReady(dummy) && dummy.city == actor.city;

    /// <summary>只接受实际可用的已完工训练假人。</summary>
    /// <param name="dummy">城市的实际建筑。</param>
    private static bool IsReady(Building dummy) => !dummy.isRekt() && dummy.asset.id == SB.training_dummy &&
        dummy.isUsable() && !dummy.isUnderConstruction();
}
