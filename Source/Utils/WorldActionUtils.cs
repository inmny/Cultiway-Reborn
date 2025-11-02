using strings;

namespace Cultiway.Utils;

public static class WorldActionUtils
{
    /// <summary>
    /// 获取生成生物的action
    /// </summary>
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
    /// <summary>
    /// 获取生成生物的action
    /// </summary>
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
    /// <summary>
    /// 获取范围落火的action
    /// </summary>
    /// <param name="radius">半径</param>
    /// <param name="height">火焰起始高度</param>
    public static WorldAction GetFallFireAction(float radius, float height = 3)
    {
        return (target, tile) =>
        {
            using var circle_offsets = ShapeUtils.CircleOffsets(tile.pos, radius);
            foreach (var offset in circle_offsets)
            {
                var current_tile = World.world.GetTile(tile.x + offset.x, tile.y + offset.y);
                if (current_tile != null)
                {
                    World.world.drop_manager.spawn(current_tile, S_Drop.fire, height);
                }
            }

            return true;
        };
    }
    /// <summary>
    /// 获取核弹的action
    /// </summary>
    public static WorldAction GetNukeAction()
    {
        return (target, tile) =>
        {
            DropsLibrary.action_atomic_bomb(tile, null);
            return true;
        };
    }
}