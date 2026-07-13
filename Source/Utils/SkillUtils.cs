using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Utils;

public static class SkillUtils
{
    public static IEnumerable<BaseSimObject> IterEnemyInSphere(Vector2 pos, float radius, BaseSimObject attacker = null,
        Kingdom attackKingdom = null)
    {
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width-1, MapBox.height-1);
        
        var lb = pos - radius * Vector2.one;
        var rt = pos + radius * Vector2.one;
            
        Vector2Int lb_fixed = Vector2Int.FloorToInt(lb);
        Vector2Int rt_fixed = Vector2Int.CeilToInt(rt);

        lb_fixed.Clamp(world_min, world_max);
        rt_fixed.Clamp(world_min, world_max);


        var pos_x = pos.x;
        var pos_y = pos.y;

        for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
        for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
        {
            if (((pos_x - x)*(pos_x-x) + (pos_y-y)*(pos_y-y)) >= (radius + 1)*(radius+1)) continue;
            WorldTile tile = World.world.GetTileSimple(x, y);
            var building = tile.building;
            var attackerKingdom = attackKingdom ?? attacker?.kingdom;
            if (building != null && building.isAlive() && (attackerKingdom?.isEnemy(building.kingdom) ?? true ))
            {
                if (building != attacker)
                    yield return building;
            }
            for (var i = 0; i < tile._units.Count; i++)
            {
                Actor obj = tile._units[i];
                if (obj == attacker) continue;
                    
                    
                var enemy = attackerKingdom?.isEnemy(obj.kingdom) ?? true;
                if (!enemy) continue;
                yield return obj;
            }
        }
    }
}
