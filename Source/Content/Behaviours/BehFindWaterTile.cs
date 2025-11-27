using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 寻找附近的水域地块
/// </summary>
public class BehFindWaterTile : BehaviourActionActor
{
    private static readonly List<WorldTile> temp_tiles = new();
    private static readonly List<MapRegion> temp_regions = new();
    private const int search_radius = 20; // 搜索半径

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        // 如果当前已经在水中，无需寻找
        if (pObject.current_tile.IsWater())
        {
            return BehResult.Continue;
        }

        // 在附近搜索水域

        var current_region = pObject.current_tile.region;
        if (current_region == null)
        {
            temp_tiles.Clear();
            var center = pObject.current_tile.pos;
            var world_min = new Vector2Int(0, 0);
            var world_max = new Vector2Int(MapBox.width - 1, MapBox.height - 1);

            for (int dx = -search_radius; dx <= search_radius; dx++)
            {
                for (int dy = -search_radius; dy <= search_radius; dy++)
                {
                    if (dx * dx + dy * dy > search_radius * search_radius) continue;

                    var check_pos = new Vector2Int(center.x + dx, center.y + dy);
                    if (check_pos.x < world_min.x || check_pos.x > world_max.x ||
                        check_pos.y < world_min.y || check_pos.y > world_max.y)
                    {
                        continue;
                    }

                    var tile = World.world.GetTileSimple(check_pos.x, check_pos.y);
                    if (tile != null && tile.IsWater())
                    {
                        temp_tiles.Add(tile);
                    }
                }
            }

            // 找到最近的水域地块
            if (temp_tiles.Count > 0)
            {
                var target = Toolbox.getClosestTile(temp_tiles, pObject.current_tile);
                pObject.beh_tile_target = target;
                return BehResult.Continue;
            }
        }
        else
        {
            if (current_region.type == TileLayerType.Ocean)
            {
                pObject.beh_tile_target = current_region.getRandomTile();
                return BehResult.Continue;
            }
            else
            {
                temp_regions.Clear();
                foreach (var neighbour_region in current_region.neighbours)
                {
                    if (neighbour_region.type == TileLayerType.Ocean)
                    {
                        temp_regions.Add(neighbour_region);
                    }
                }
                if (temp_regions.Count > 0)
                {
                    pObject.beh_tile_target = temp_regions.GetRandom().getRandomTile();
                    return BehResult.Continue;
                }
            }
        }
        // 未找到水域
        return BehResult.Continue;
    }
}

