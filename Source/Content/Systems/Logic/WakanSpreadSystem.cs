using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class WakanSpreadSystem : BaseSystem
{
    private const int check_tile_count_per_frame = 1024;

    private int[] _check_tile_ids;

    private int _last_check_idx;

    private float[,] _map;

    protected override void OnUpdateGroup()
    {
        if (MapGenerator._tilesMap != null) return;
        if (_check_tile_ids == null || _check_tile_ids.Length != World.world.tilesList.Length)
        {
            RegenerateCheckIDs(true);
        }

        System.Diagnostics.Debug.Assert(_check_tile_ids != null, nameof(_check_tile_ids) + " != null");
        _map = WakanMap.I.map;
        for (int i = 0; i < check_tile_count_per_frame * Mathf.Sqrt(Config.timeScale); i++)
        {
            int check_idx = _last_check_idx + 1;
            if (check_idx >= _check_tile_ids.Length)
            {
                RegenerateCheckIDs(false);
                check_idx = 0;
            }

            CheckSingleTile(World.world.tilesList[_check_tile_ids[check_idx]]);
            _last_check_idx = check_idx;
        }
    }

    private void RegenerateCheckIDs(bool new_array)
    {
        if (new_array)
        {
            _check_tile_ids = new int[World.world.tilesList.Length];
            for (int i = 0; i < _check_tile_ids.Length; i++)
            {
                _check_tile_ids[i] = i;
            }
        }

        _check_tile_ids.Shuffle();
    }

    private void CheckSingleTile(WorldTile tile)
    {
        foreach (var neighbor in tile.neighbours)
        {
            var tile_v = Mathf.Max(0,     _map[tile.x, tile.y]);
            var neighbor_v = Mathf.Max(0, _map[neighbor.x, neighbor.y]);

            var delta = tile_v - neighbor_v;
            var flow = Mathf.Sign(delta) * Mathf.Abs(delta) * 0.25f;

            _map[tile.x, tile.y] = tile_v             - flow;
            _map[neighbor.x, neighbor.y] = neighbor_v + flow;
        }
    }
}