using System.Collections.Generic;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Core.GeoLib.Systems;

public class ErosionSystem : BaseSystem
{
    private const int frame_per_year             = 60 * 5 * 12;
    private const int check_tile_count_per_frame = 1024;

    private const int max_decrease_once = 2;

    private readonly Dictionary<string, int> _available_tile_types = new Dictionary<string, int>()
    {
        { ST.deep_ocean, 0 },
        { ST.close_ocean, 1 },
        { ST.shallow_waters, 2 },
        { ST.sand, 3 },
        { ST.soil_low, 4 },
        { ST.soil_high, 5 },
        { ST.hills, 6 },
        { ST.mountains, 7 }
    };

    private int[] check_tile_ids;

    private int last_check_idx;

    private int total_tile_count;

    [Hotfixable]
    protected override void OnUpdateGroup()
    {
        if (MapGenerator._tilesMap != null) return;
        if (check_tile_ids == null || check_tile_ids.Length != World.world.tilesList.Length)
        {
            RegenerateCheckIDs(true);
        }

        System.Diagnostics.Debug.Assert(check_tile_ids != null, nameof(check_tile_ids) + " != null");
        for (int i = 0; i < check_tile_count_per_frame; i++)
        {
            int check_idx = last_check_idx + 1;
            if (check_idx >= check_tile_ids.Length)
            {
                RegenerateCheckIDs(false);
                check_idx = 0;
            }

            CheckSingleTile(World.world.tilesList[check_tile_ids[check_idx]]);
            last_check_idx = check_idx;
        }
    }

    private void RegenerateCheckIDs(bool new_array)
    {
        if (new_array)
        {
            check_tile_ids = new int[World.world.tilesList.Length];
            for (int i = 0; i < check_tile_ids.Length; i++)
            {
                check_tile_ids[i] = i;
            }
        }

        check_tile_ids.Shuffle();
        total_tile_count = check_tile_ids.Length;
    }

    [Hotfixable]
    private void CheckSingleTile(WorldTile tile)
    {
        if (!_available_tile_types.TryGetValue(tile.main_type.id, out var tile_level)) return;
        int exposion_count = 0;
        foreach (var neighbour in tile.neighbours)
        {
            if (!_available_tile_types.TryGetValue(neighbour.main_type.id, out var neighbour_level)) continue;
            if (neighbour_level >= tile_level) continue;

            exposion_count += tile_level - neighbour_level;
        }

        if (exposion_count > 0)
        {
            var prob = total_tile_count /
                       (check_tile_count_per_frame * frame_per_year * (10f / exposion_count));
            if (Toolbox.randomChance(prob))
            {
                int num = max_decrease_once;
                do
                {
                    MapAction.decreaseTile(tile);
                    num--;
                } while (!_available_tile_types.ContainsKey(tile.main_type.id) && num > 0);
            }
        }
    }
}