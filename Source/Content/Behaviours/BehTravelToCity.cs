using System.Linq;
using ai.behaviours;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 选择一个其它城市作为目标并设置行为目标地块
/// </summary>
public class BehTravelToCity : BehCityActor
{
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var currentCity = pActor.city;
        if (currentCity == null) return BehResult.Stop;

        // 如果已有目标且仍然有效，继续沿用
        if (pActor.beh_tile_target != null)
        {
            var targetCity = pActor.beh_tile_target.zone_city;
            if (targetCity != null && targetCity != currentCity && targetCity.isAlive())
            {
                return BehResult.Continue;
            }
        }

        var cities = World.world.cities.list;
        if (cities == null || cities.Count <= 1) return BehResult.Stop;

        WorldTile targetTile = null;
        // 尝试多次挑选有效目标
        for (int i = 0; i < 8 && targetTile == null; i++)
        {
            var candidate = cities[Randy.randomInt(0, cities.Count)];
            if (candidate == null || candidate == currentCity || !candidate.isAlive()) continue;
            if (candidate.kingdom == null || currentCity.kingdom == null) continue;
            if (!candidate.kingdom.isOpinionTowardsKingdomGood(currentCity.kingdom)) continue;

            var tile = candidate.getTile();
            if (tile == null) continue;

            // 优先同岛，避免无法到达
            if (!tile.isSameIsland(pActor.current_tile)) continue;

            targetTile = tile;
        }

        if (targetTile == null) return BehResult.Stop;

        pActor.beh_tile_target = targetTile;
        return BehResult.Continue;
    }
}

