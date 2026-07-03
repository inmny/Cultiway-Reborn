using Cultiway.Abstract;

namespace Cultiway.Content;

public class TerrainTraversalRules : ICanInit
{
    private const int MountainDamage = 1;
    private const int SummitDamage = 2;

    public void Init()
    {
        ConfigurePassableMountain(TileLibrary.mountains, 0.45f, MountainDamage);
        ConfigurePassableMountain(TileLibrary.summit, 0.35f, SummitDamage);
        ConfigurePassableMountain(TopTileLibrary.snow_block, 0.5f, MountainDamage);
        ConfigurePassableMountain(TopTileLibrary.snow_summit, 0.4f, SummitDamage);
    }

    private static void ConfigurePassableMountain(TileTypeBase tileType, float walkMultiplier, int damage)
    {
        if (tileType == null)
        {
            return;
        }

        tileType.block = false;
        tileType.block_height = 0f;
        tileType.layer_type = TileLayerType.Ground;
        tileType.ground = true;
        tileType.walk_multiplier = walkMultiplier;
        tileType.damage_units = damage > 0;
        if (damage > 0)
        {
            tileType.damage = damage;
        }
    }
}
