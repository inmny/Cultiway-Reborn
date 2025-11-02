namespace Cultiway.Utils;

public static class WorldActionUtils
{
    public static WorldAction GetSpawnUnitAction(string asset_id, int count)
    {
        return (target, tile) =>
        {
            for (int i = 0; i < count; i++)
            {
                World.world.units.spawnNewUnit(asset_id, tile, true, pSpawnHeight: 0);
            }

            return true;
        };
    }
    public static WorldAction GetSpawnUnitAction(ActorAsset asset, int count)
    {
        return (target, tile) =>
        {
            for (int i = 0; i < count; i++)
            {
                World.world.units.spawnNewUnit(asset.id, tile, true, pSpawnHeight: 0);
            }

            return true;
        };
    }
}