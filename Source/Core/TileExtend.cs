using System.Collections.Generic;

namespace Cultiway.Core;

/// <summary>
/// WorldTile 的轻量查询视图，不再为每个 tile 创建 ECS Entity。
/// </summary>
public readonly struct TileExtend
{
    private readonly int tileId;

    internal TileExtend(int tileId)
    {
        this.tileId = tileId;
    }

    public WorldTile Base => ModClass.I.TileExtendManager.GetTile(tileId);

    public GeoRegion GetGeoRegion()
    {
        return GetGeoRegion(GeoRegionLayer.Primary);
    }

    public GeoRegion GetGeoRegion(GeoRegionLayer layer)
    {
        return WorldboxGame.I.GeoRegions.GetRegionForTile(tileId, layer);
    }

    public IEnumerable<GeoRegion> GetGeoRegions(GeoRegionLayer layer)
    {
        GeoRegion region = GetGeoRegion(layer);
        if (region != null)
        {
            yield return region;
        }
    }

    public IEnumerable<GeoRegion> GetGeoRegions()
    {
        return WorldboxGame.I.GeoRegions.EnumerateRegionsForTile(tileId);
    }

    public bool HasGeoRegion(GeoRegion geoRegion)
    {
        return geoRegion != null && WorldboxGame.I.GeoRegions.TileHasRegion(tileId, geoRegion);
    }

    public bool HasGeoRegion()
    {
        return GetGeoRegion() != null;
    }
}
