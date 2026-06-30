using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 为宗门杂务选择一个可到达的驻地城市地块。
/// </summary>
public class BehFindSectChoreTile : BehaviourActionActor
{
    /// <summary>
    /// 优先在宗门驻地城市随机挑选同岛地块，找不到时回退到单位当前地块。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (!SectChoreRules.CanDoSectChore(pActor))
        {
            SectVerifyLog.Log("SectChoreTarget", $"actor={SectVerifyLog.Actor(pActor)} result=false reason=no_permission");
            return BehResult.Stop;
        }

        Sect sect = pActor.GetExtend().sect;
        City city = sect.GetHomeCity() ?? pActor.city;
        WorldTile target = FindCityTile(pActor, city) ?? pActor.current_tile;
        pActor.beh_tile_target = target;
        SectVerifyLog.Log("SectChoreTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} city={city?.name ?? "null"} tile={target?.pos.x ?? -1},{target?.pos.y ?? -1}");
        return BehResult.Continue;
    }

    private static WorldTile FindCityTile(Actor actor, City city)
    {
        if (city == null || !city.isAlive() || !city.hasZones()) return null;

        for (int i = 0; i < 8; i++)
        {
            TileZone zone = city.zones.GetRandom();
            if (zone == null || zone.tiles.Length == 0) continue;

            WorldTile tile = zone.tiles.GetRandom();
            if (tile != null && tile.isSameIsland(actor.current_tile))
            {
                return tile;
            }
        }

        WorldTile cityTile = city.getTile();
        return cityTile != null && cityTile.isSameIsland(actor.current_tile) ? cityTile : null;
    }
}
