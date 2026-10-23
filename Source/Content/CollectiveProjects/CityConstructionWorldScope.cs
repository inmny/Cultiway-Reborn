using System;
using Cultiway.Core.Skills;

namespace Cultiway.Content.CollectiveProjects;

/// <summary>把城市当前归属和农田保护接入普通构造，不替换法术形状或执行。</summary>
internal static class CityConstructionWorldScope
{
    /// <summary>准备阶段不缓存整座城市的旧边界，实际接触时读取当前城市和区域。</summary>
    /// <param name="cityId">发起城市的长期编号。</param>
    /// <param name="taskValid">工程和执行令牌此刻是否仍有效。</param>
    internal static ConstructionWorldScope Create(long cityId, Func<bool> taskValid)
    {
        return new ConstructionWorldScope(
            () => World.world.cities.get(cityId)?.isAlive() == true && taskValid(),
            (action, tile) =>
            {
                City city = World.world.cities.get(cityId);
                TileZone zone = tile.zone;
                if (city == null || zone.city != null && zone.city != city ||
                    !city.zones.Contains(zone) && !city.neighbour_zones.Contains(zone)) return false;
                if (action != "nature.grow") return true;
                // 原版可能在施放期间重新规划农田，不能继续使用受理时的旧保护集合。
                city.calculated_place_for_farms.checkAddRemove();
                city.calculated_farm_fields.checkAddRemove();
                return !tile.Type.farm_field && !city.calculated_place_for_farms.Contains(tile) &&
                       !city.calculated_farm_fields.Contains(tile);
            });
    }
}