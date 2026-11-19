using System.Linq;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageDuties;

/// <summary>居民优先回登记住房；流浪救助只提供临时空房，不改动住房归属。</summary>
public static class VillageHomeService
{
    /// <summary>查找本人住房，或救助政策允许借宿的本城空房。</summary>
    /// <param name="actor">已经准备休息的居民。</param>
    public static Building GetSleepHome(Actor actor)
    {
        var home = actor.getHomeBuilding();
        if (home != null) return home.current_tile.isSameIsland(actor.current_tile) && !home.current_tile.isOnFire() ? home : null;
        if (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.HomelessRelief)) return null;
        if (IsEmptyShelter(actor, actor.inside_building)) return actor.inside_building;
        return actor.city.buildings.Where(b => IsEmptyShelter(actor, b))
            .OrderBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile)).FirstOrDefault();
    }

    /// <summary>借宿只能使用同城同岛、完工可用且没有登记住户的安全住宅。</summary>
    /// <param name="actor">没有登记住房的居民。</param>
    /// <param name="building">准备临时过夜的房屋。</param>
    public static bool IsEmptyShelter(Actor actor, Building building) => building != null && building.city == actor.city &&
        building.asset.hasHousingSlots() && !building.hasResidents() && building.isUsable() && !building.isUnderConstruction() &&
        !building.isAbandoned() && !building.current_tile.isOnFire() && building.current_tile.isSameIsland(actor.current_tile);
}
