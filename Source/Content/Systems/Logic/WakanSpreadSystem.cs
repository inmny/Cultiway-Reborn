using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class WakanSpreadSystem : BaseSystem
{
    private const int check_tile_count_per_frame = 64;

    private int[] _check_tile_ids;

    private int _last_check_idx;

    private float[,] _map;
    private int total_tile_count;
    protected override void OnUpdateGroup()
    {
        if (MapGenerator._tilesMap != null) return;
        if (_check_tile_ids == null || _check_tile_ids.Length != World.world.tiles_list.Length)
        {
            RegenerateCheckIDs(true);
        }

        System.Diagnostics.Debug.Assert(_check_tile_ids != null, nameof(_check_tile_ids) + " != null");
        _map = WakanMap.I.map;
        for (int i = 0; i < check_tile_count_per_frame; i++)
        {
            int check_idx = _last_check_idx + 1;
            if (check_idx >= _check_tile_ids.Length)
            {
                RegenerateCheckIDs(false);
                check_idx = 0;
            }

            CheckSingleTile(World.world.tiles_list[_check_tile_ids[check_idx]]);
            _last_check_idx = check_idx;
        }
    }

    private void RegenerateCheckIDs(bool new_array)
    {
        if (new_array)
        {
            _check_tile_ids = new int[World.world.tiles_list.Length];
            for (int i = 0; i < _check_tile_ids.Length; i++)
            {
                _check_tile_ids[i] = i;
            }
        }

        _check_tile_ids.Shuffle();
        total_tile_count = _check_tile_ids.Length;
    }

    private void CheckSingleTile(WorldTile tile)
    {
        foreach (var neighbor in tile.neighbours)
        {
            var tile_v = Mathf.Max(0,     _map[tile.x, tile.y]);
            var neighbor_v = Mathf.Max(0, _map[neighbor.x, neighbor.y]);

            var delta = tile_v - neighbor_v;
            // ReSharper disable once PossibleLossOfFraction
            var flow = Mathf.Sign(delta) * Mathf.Abs(delta) *
                Mathf.Clamp(Mathf.Log10(total_tile_count / check_tile_count_per_frame) * 0.1f, 0, 1f);

            _map[tile.x, tile.y] = tile_v             - flow;
            _map[neighbor.x, neighbor.y] = neighbor_v + flow;
        }
    }
}