using UnityEngine;

namespace Cultiway.Content;

internal static class WorldBehaviourActionWakanSpread
{
    private static int start_tile_id = 0;
    private static int update_num => (int)Mathf.Sqrt(MapBox.width * MapBox.height) * Config.MAP_BLOCK_SIZE;

    public static void Update()
    {
        var map = WakanMap.I.map;
        var num = update_num;
        for (int off = 0; off < num; off++)
        {
            var tile = World.world.tilesList[start_tile_id];

            foreach (var neighbor in tile.neighbours)
            {
                var tile_v = Mathf.Max(0,     map[tile.x, tile.y]);
                var neighbor_v = Mathf.Max(0, map[neighbor.x, neighbor.y]);

                var delta = tile_v - neighbor_v;
                var flow = Mathf.Sign(delta) * Mathf.Abs(delta) * 0.25f;

                map[tile.x, tile.y] = tile_v             - flow;
                map[neighbor.x, neighbor.y] = neighbor_v + flow;
            }

            start_tile_id = (start_tile_id + 1) % World.world.tilesList.Length;
        }
    }

    public static void Clear()
    {
        start_tile_id = 0;
    }
}