using System.Collections.Generic;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Core;

public class GeoRegionManager : MetaSystemManager<GeoRegion, GeoRegionData>
{
    public GeoRegionManager()
    {
        type_id = WorldboxGame.HistoryMetaDatas.GeoRegion.id;
    }
    public override void updateDirtyUnits()
    {
        if (!CanRefreshUnits()) return;

        List<Actor> units = World.world.units.units_only_alive;
        for (int i = 0; i < units.Count; i++)
        {
            Actor actor = units[i];
            WorldTile tile = actor.current_tile;
            if (tile == null) continue;

            foreach (GeoRegion geoRegion in tile.GetExtend().GetGeoRegions())
            {
                if (geoRegion.isRekt() || !geoRegion.isDirtyUnits()) continue;
                geoRegion.listUnit(actor);
            }
        }
    }

    public void SetDirtyUnitsForTile(WorldTile tile)
    {
        if (!CanRefreshUnits() || tile == null) return;

        foreach (GeoRegion geoRegion in tile.GetExtend().GetGeoRegions())
        {
            if (geoRegion.isRekt()) continue;
            setDirtyUnits(geoRegion);
        }
    }

    public void SetDirtyUnitsForTileChange(WorldTile oldTile, WorldTile newTile)
    {
        if (oldTile == newTile) return;
        if (!CanRefreshUnits()) return;

        TileExtend oldExtend = oldTile?.GetExtend();
        TileExtend newExtend = newTile?.GetExtend();

        if (oldExtend != null)
        {
            foreach (GeoRegion geoRegion in oldExtend.GetGeoRegions())
            {
                if (geoRegion.isRekt() || (newExtend != null && newExtend.HasGeoRegion(geoRegion))) continue;
                setDirtyUnits(geoRegion);
            }
        }

        if (newExtend != null)
        {
            foreach (GeoRegion geoRegion in newExtend.GetGeoRegions())
            {
                if (geoRegion.isRekt() || (oldExtend != null && oldExtend.HasGeoRegion(geoRegion))) continue;
                setDirtyUnits(geoRegion);
            }
        }
    }

    public GeoRegion ResolveGeoRegion(WorldTile tile, CustomMapModeAsset mapMode)
    {
        if (tile == null || mapMode == null) return null;
        if (!CanResolveTiles()) return null;

        GeoRegionLayer[] layers = mapMode.geo_region_layers;
        if (layers == null || layers.Length == 0) return null;

        TileExtend tileExtend = tile.GetExtend();
        for (int i = 0; i < layers.Length; i++)
        {
            GeoRegion geoRegion = tileExtend.GetGeoRegion(layers[i]);
            if (geoRegion != null && !geoRegion.isRekt()) return geoRegion;
        }

        return null;
    }

    public GeoRegion GetGeoRegionForTile(WorldTile tile, CustomMapModeAsset mapMode)
    {
        GeoRegion geoRegion = ResolveGeoRegion(tile, mapMode);
        if (geoRegion != null) return geoRegion;

        return GetPrimaryGeoRegionForTile(tile);
    }

    public GeoRegion GetPrimaryGeoRegionForTile(WorldTile tile)
    {
        if (tile == null) return null;
        if (!CanResolveTiles()) return null;

        return tile.GetExtend().GetGeoRegion();
    }

    private bool CanRefreshUnits()
    {
        return CanResolveTiles();
    }

    private bool CanResolveTiles()
    {
        return ModClass.I?.TileExtendManager != null && ModClass.I.TileExtendManager.Ready();
    }

    public override void addObject(GeoRegion pObject)
    {
        pObject.BaseSetup();
        base.addObject(pObject);
    }

    public GeoRegion BuildGeoRegion(Actor founder)
    {
        var geoRegion = newObject();
        geoRegion.Setup(founder);

        return geoRegion;
    }
}
