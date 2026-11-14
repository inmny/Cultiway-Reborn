using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实地块只在主线程采集，修改一格时复用未变化的只读数据块。</summary>
internal static class PropagationGroundMap
{
    /// <summary>本世界已经发布的陆地连通，不保存游戏对象。</summary>
    private static AbilityHeightField land;

    /// <summary>首次需要才建立，普通能力不维护额外地图。</summary>
    /// <param name="terrain">共同实际地表。</param>
    internal static PropagationGround Capture(AbilityHeightField terrain)
    {
        if (land == null)
        {
            var values = new float[MapBox.width * MapBox.height];
            for (int y = 0; y < MapBox.height; y++)
            for (int x = 0; x < MapBox.width; x++) values[x + y * MapBox.width] = World.world.GetTileSimple(x, y).Type.liquid ? 0f : 1f;
            land = new AbilityHeightField(MapBox.width, MapBox.height, new Vector2(MapBox.width, MapBox.height), values);
        }
        return new PropagationGround(land, terrain);
    }

    /// <summary>原版地块类型实际提交之后更新，不在计算线程查询世界。</summary>
    /// <param name="tile">已经变化的实际地块。</param>
    internal static void Changed(WorldTile tile)
    {
        if (land == null) return;
        land = land.WithRegion(new Rect(tile.pos.x, tile.pos.y, 1, 1), (_, _) => tile.Type.liquid ? 0f : 1f);
    }

    /// <summary>换世界后不复用同编号旧地块。</summary>
    internal static void Clear() => land = null;
}
