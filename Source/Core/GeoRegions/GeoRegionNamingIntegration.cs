#if CHINESENAME
using System;
using Chinese_Name.Naming.Game;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.GeoRegions;

/// <summary>让中文名在需要时查询地块所属的地区。</summary>
internal static class GeoRegionNamingIntegration
{
    /// <summary>按一个原版城区的边长识别附近水域。</summary>
    private const int WaterDistance = 8;

    /// <summary>注册查询，在地图地区准备好后才会返回数据。</summary>
    internal static void Register() => GeographicNaming.RegisterRegionResolver(Resolve);

    /// <summary>读取所属地区的名字和用于命名的主要地貌。</summary>
    /// <param name="tile">当前地图上的地块。</param>
    private static NamingRegion Resolve(WorldTile tile)
    {
        var manager = WorldboxGame.I?.GeoRegions;
        var region = manager?.GetPrimaryGeoRegionForTile(tile);
        if (region == null || region.isRekt()) return null;
        return new NamingRegion(region.id, region.name, LandformAt(manager, ModClass.L.GeoRegionLibrary, tile));
    }

    /// <summary>按地貌、水边和陆块归属确定主要地貌。临河的峡谷按河谷处理。</summary>
    /// <param name="manager">当前世界的地区管理器。</param>
    /// <param name="library">地区分类库。</param>
    /// <param name="tile">要查询的地块。</param>
    private static string LandformAt(GeoRegionManager manager, GeoRegionLibrary library, WorldTile tile)
    {
        int tileId = tile.data.tile_id;
        string shape = manager.GetRegionForTile(tileId, GeoRegionLayer.Landform)?.data.CategoryId;
        if (shape == library.LandformMountain.id) return "mountain";
        if (shape == library.LandformBasin.id) return "basin";

        var (nearSea, nearRiver) = NearbyWater(manager, library, tile);
        if (shape == library.LandformCanyon.id) return nearRiver ? "river-valley" : "canyon";
        if (manager.GetRegionForTile(tileId, GeoRegionLayer.Archipelago) != null) return "archipelago";
        if (manager.GetRegionForTile(tileId, GeoRegionLayer.Landmass)?.data.CategoryId == library.LandmassIsland.id)
            return "island";
        if (nearSea) return "coast";
        if (manager.GetRegionForTile(tileId, GeoRegionLayer.Peninsula) != null) return "peninsula";
        if (nearRiver) return "river-valley";
        return shape == library.LandformPlain.id ? "plain" : null;
    }

    /// <summary>检查一个城区距离内是否有海或河流。</summary>
    /// <param name="manager">当前世界的地区管理器。</param>
    /// <param name="library">地区分类库。</param>
    /// <param name="tile">要查询的地块。</param>
    private static (bool Sea, bool River) NearbyWater(GeoRegionManager manager, GeoRegionLibrary library, WorldTile tile)
    {
        bool sea = false, river = false;
        for (int dx = -WaterDistance; dx <= WaterDistance; dx++)
        {
            int span = WaterDistance - Math.Abs(dx);
            for (int dy = -span; dy <= span; dy++)
            {
                var other = World.world.GetTile(tile.x + dx, tile.y + dy);
                if (other == null || other.Type.layer_type != TileLayerType.Ocean && !other.Type.ocean) continue;
                string category = manager.GetRegionForTile(other.data.tile_id, GeoRegionLayer.Primary)?.data.CategoryId;
                sea |= category == library.PrimarySea.id;
                river |= category == library.PrimaryRiver.id;
                if (sea && river) return (true, true);
            }
        }
        return (sea, river);
    }
}
#endif
