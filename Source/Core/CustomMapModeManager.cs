using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core;

public class CustomMapModeManager
{
    public CustomMapLayer MapLayer { get; private set; }
    public GeoRegion HoveredGeoRegion { get; private set; }
    private CustomMapModeAsset _cached_map_mode;
    private GeoRegion _selected_geo_region;

    public CustomMapModeAsset CurrMapMode => UpdateCurrentMapMode();

    public CustomMapModeAsset UpdateCurrentMapMode()
    {
        CustomMapModeAsset oldMapMode = _cached_map_mode;
        if (oldMapMode != null && PlayerConfig.optionBoolEnabled(oldMapMode.toggle_name))
        {
            return oldMapMode;
        }

        CustomMapModeAsset newMapMode = FindCurrentMapMode();
        if (oldMapMode != newMapMode)
        {
            _cached_map_mode = newMapMode;
            HoveredGeoRegion = null;
            _selected_geo_region = null;
            SetAllDirty();
        }

        return _cached_map_mode;
    }

    public void InvalidateCurrentMapMode()
    {
        _cached_map_mode = null;
        SetAllDirty();
    }

    private static CustomMapModeAsset FindCurrentMapMode()
    {
        var lib = ModClass.L.CustomMapModeLibrary;
        int len = lib.list.Count;
        for (int i = 0; i < len; i++)
        {
            CustomMapModeAsset mapMode = lib.list[i];
            if (PlayerConfig.optionBoolEnabled(mapMode.toggle_name))
            {
                return mapMode;
            }
        }

        return null;
    }

    public void Initialize()
    {
        GameObject custom_map_layer_obj =
            new("[layer]Energy Layer", typeof(CustomMapLayer), typeof(SpriteRenderer));
        custom_map_layer_obj.transform.SetParent(World.world.transform);
        custom_map_layer_obj.transform.localPosition = Vector3.zero;
        custom_map_layer_obj.transform.localScale = Vector3.one;
        custom_map_layer_obj.GetComponent<SpriteRenderer>().sortingOrder = 1;
        MapLayer = custom_map_layer_obj.GetComponent<CustomMapLayer>();
        World.world._map_layers.Add(MapLayer);

        StartUpdate();
    }

    public void UpdateInteractionState(CustomMapModeAsset mapMode)
    {
        GeoRegion oldHovered = HoveredGeoRegion;
        GeoRegion oldSelected = _selected_geo_region;
        HoveredGeoRegion = WorldboxGame.I.GeoRegions.ResolveGeoRegion(World.world.getMouseTilePosCachedFrame(), mapMode);
        _selected_geo_region = WorldboxGame.I.SelectedGeoRegion;

        if (oldHovered != HoveredGeoRegion || oldSelected != _selected_geo_region)
        {
            SetGeoRegionsDirty(mapMode, oldHovered, HoveredGeoRegion, oldSelected, _selected_geo_region);
        }
    }

    public void ApplyGeoRegionInteractionColor(GeoRegion region, ref Color32 color)
    {
        if (region == null || region.isRekt() || color.a == 0) return;

        if (region == _selected_geo_region)
        {
            color = ColorUtils.Blend(color, new Color32(255, 255, 255, 255), 0.35f);
            color.a = 255;
        }
        else if (region == HoveredGeoRegion)
        {
            color = ColorUtils.Blend(color, new Color32(255, 255, 255, 255), 0.22f);
            color.a = 230;
        }
    }

    private void StartUpdate()
    {
        var thread = new Thread(() =>
        {
            while (true)
                try
                {
                    MapLayer.WaitForDirty(500);
                    MapLayer.PreparePixels();
                }
                catch (Exception e)
                {
                    //is_running = false;
                    ModClass.LogWarning("游戏时间倍率过高");
                    ModClass.LogWarning($"[{e.GetType()}]: {e.Message}\n{e.StackTrace}");
                    //LogService.LogErrorConcurrent(e.StackTrace);
                }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    public void SetAllDirty()
    {
        MapLayer?.SetAllDirty();
    }

    public void SetTileDirty(WorldTile tile)
    {
        MapLayer?.SetTileDirty(tile);
    }

    private void SetGeoRegionsDirty(CustomMapModeAsset mapMode, params GeoRegion[] regions)
    {
        if (MapLayer == null || mapMode == null || regions == null) return;

        HashSet<GeoRegion> uniqueRegions = new();
        for (int i = 0; i < regions.Length; i++)
        {
            GeoRegion region = regions[i];
            if (!ShouldRefreshRegion(region, mapMode)) continue;
            if (!uniqueRegions.Add(region)) continue;

            MapLayer.SetTilesDirty(WorldboxGame.I.GeoRegions.EnumerateRegionTiles(region));
        }
    }

    private static bool ShouldRefreshRegion(GeoRegion region, CustomMapModeAsset mapMode)
    {
        if (region == null || region.isRekt() || region.data == null) return false;
        return mapMode.ContainsGeoRegionLayer(region.data.Layer);
    }
}
