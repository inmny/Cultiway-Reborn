using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Core.Libraries;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core;

public class CustomMapModeManager
{
    private const float InteractionAnimationInterval = 0.06f;

    public CustomMapLayer MapLayer { get; private set; }
    public GeoRegion HoveredGeoRegion { get; private set; }
    public GeoRegion UiHoveredGeoRegion { get; private set; }
    private CustomMapModeAsset _cached_map_mode;
    private GeoRegion _selected_geo_region;
    private float _interaction_animation_next_refresh_time;
    private float _interaction_animation_pulse = 0.5f;

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
            UiHoveredGeoRegion = null;
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

    public void UpdateInteractionAnimation(CustomMapModeAsset mapMode)
    {
        if (mapMode?.geo_region_layers == null || mapMode.geo_region_layers.Length == 0 || !HasAnyInteractionRegion()) return;

        float time = Time.unscaledTime;
        if (time < _interaction_animation_next_refresh_time) return;

        _interaction_animation_next_refresh_time = time + InteractionAnimationInterval;
        _interaction_animation_pulse = 0.5f + Mathf.Sin(time * 7.5f) * 0.5f;
        SetGeoRegionsDirty(mapMode, UiHoveredGeoRegion, _selected_geo_region, HoveredGeoRegion);
    }

    public void SetUiHoveredGeoRegion(GeoRegion region)
    {
        if (ReferenceEquals(UiHoveredGeoRegion, region)) return;

        GeoRegion oldRegion = UiHoveredGeoRegion;
        UiHoveredGeoRegion = region;
        SetGeoRegionsDirty(UpdateCurrentMapMode(), oldRegion, UiHoveredGeoRegion);
    }

    public void ClearUiHoveredGeoRegion(GeoRegion region)
    {
        if (!ReferenceEquals(UiHoveredGeoRegion, region)) return;
        SetUiHoveredGeoRegion(null);
    }

    public bool TryGetForcedInteractionRegion(WorldTile tile, out GeoRegion region)
    {
        if (TryGetRegionOnTile(tile, UiHoveredGeoRegion, out region)) return true;
        if (TryGetRegionOnTile(tile, _selected_geo_region, out region)) return true;
        return false;
    }

    public void ApplyGeoRegionInteractionColor(GeoRegion region, WorldTile tile, ref Color32 color)
    {
        if (region == null || region.isRekt() || color.a == 0) return;

        bool boundary = IsBoundaryTile(tile, region);
        if (region == UiHoveredGeoRegion)
        {
            color = boundary
                ? PulseColor(new Color32(255, 170, 42, 255), new Color32(255, 246, 165, 255))
                : PulseBlend(color, new Color32(255, 241, 160, 255), 0.28f, 0.62f);
            color.a = 255;
        }
        else if (region == _selected_geo_region)
        {
            color = boundary
                ? PulseColor(new Color32(210, 220, 230, 255), new Color32(255, 255, 255, 255))
                : PulseBlend(color, new Color32(255, 255, 255, 255), 0.22f, 0.52f);
            color.a = 255;
        }
        else if (region == HoveredGeoRegion)
        {
            color = boundary
                ? PulseColor(new Color32(92, 198, 255, 225), new Color32(225, 250, 255, 255))
                : PulseBlend(color, new Color32(210, 242, 255, 255), 0.14f, 0.38f);
            color.a = boundary ? (byte)245 : (byte)230;
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
        thread.Name = "CultiwayMapModePixels";
        thread.Priority = System.Threading.ThreadPriority.BelowNormal;
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
            if (!CanRefreshRegion(region)) continue;
            if (!uniqueRegions.Add(region)) continue;

            MapLayer.SetTilesDirty(WorldboxGame.I.GeoRegions.EnumerateRegionTiles(region));
        }
    }

    private bool HasAnyInteractionRegion()
    {
        return CanRefreshRegion(UiHoveredGeoRegion) ||
               CanRefreshRegion(_selected_geo_region) ||
               CanRefreshRegion(HoveredGeoRegion);
    }

    private static bool CanRefreshRegion(GeoRegion region)
    {
        return region != null && !region.isRekt() && region.data != null;
    }

    private static bool TryGetRegionOnTile(WorldTile tile, GeoRegion candidate, out GeoRegion region)
    {
        region = null;
        if (tile == null || candidate == null || candidate.isRekt() || candidate.data == null) return false;

        if (!TryGetTileExtend(tile, out TileExtend tileExtend)) return false;
        GeoRegion current = tileExtend.GetGeoRegion(candidate.data.Layer);
        if (!ReferenceEquals(current, candidate)) return false;

        region = candidate;
        return true;
    }

    private Color32 PulseColor(Color32 dim, Color32 bright)
    {
        return ColorUtils.Blend(dim, bright, 0.25f + _interaction_animation_pulse * 0.75f);
    }

    private Color32 PulseBlend(Color32 color, Color32 target, float minAmount, float maxAmount)
    {
        float amount = Mathf.Lerp(minAmount, maxAmount, _interaction_animation_pulse);
        return ColorUtils.Blend(color, target, amount);
    }

    private static bool IsBoundaryTile(WorldTile tile, GeoRegion region)
    {
        if (tile == null || region?.data == null) return false;

        WorldTile[] neighbors = tile.neighbours;
        if (neighbors == null || neighbors.Length == 0) return true;

        GeoRegionLayer layer = region.data.Layer;
        for (int i = 0; i < neighbors.Length; i++)
        {
            WorldTile neighbor = neighbors[i];
            if (neighbor == null) return true;

            if (!TryGetTileExtend(neighbor, out TileExtend neighborExtend)) return true;

            GeoRegion neighborRegion = neighborExtend.GetGeoRegion(layer);
            if (!ReferenceEquals(neighborRegion, region)) return true;
        }

        return false;
    }

    private static bool TryGetTileExtend(WorldTile tile, out TileExtend tileExtend)
    {
        tileExtend = default;
        if (tile == null || tile.data == null) return false;
        if (ModClass.I?.TileExtendManager == null || !ModClass.I.TileExtendManager.Ready()) return false;

        int tileId = tile.data.tile_id;
        if ((uint)tileId >= (uint)World.world.tiles_list.Length) return false;

        tileExtend = ModClass.I.TileExtendManager.Get(tileId);
        return true;
    }
}
