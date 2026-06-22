using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core;

public class CustomMapLayer : MapLayer
{
    private static readonly HashSet<TileZone> _drawn_zones       = new();
    private static readonly HashSet<TileZone> _last_drawn_zones  = new();
    private readonly        object            lock_dirty         = new();
    private readonly        object            lock_pixels        = new();
    private readonly        AutoResetEvent    dirty_event        = new(true);
    private readonly        HashSet<int>      dirty_tile_ids     = new();
    private                 bool              all_dirty          = true;
    private                 Color32[]         mirror_pixels;

    private bool need_update = true;

    private Color spr_color = Color.white;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        SetAllDirty();
    }

    internal void SetAllDirty()
    {
        lock (lock_dirty)
        {
            all_dirty = true;
            dirty_tile_ids.Clear();
        }

        dirty_event.Set();
    }

    internal void SetTileDirty(WorldTile tile)
    {
        if (tile == null) return;
        if (!TryGetTileId(tile, out int tileId)) return;

        bool hasDirty;
        lock (lock_dirty)
        {
            if (all_dirty) return;
            hasDirty = dirty_tile_ids.Add(tileId);
        }

        if (hasDirty) dirty_event.Set();
    }

    internal void SetTilesDirty(IEnumerable<WorldTile> tiles)
    {
        if (tiles == null) return;

        bool hasDirty = false;
        lock (lock_dirty)
        {
            if (all_dirty) return;
            foreach (WorldTile tile in tiles)
            {
                if (!TryGetTileId(tile, out int tileId)) continue;
                hasDirty |= dirty_tile_ids.Add(tileId);
            }
        }

        if (hasDirty) dirty_event.Set();
    }

    internal void WaitForDirty(int pMillisecondsTimeout)
    {
        dirty_event.WaitOne(pMillisecondsTimeout);
    }

    private bool ConsumeDirty(out bool rebuildAll, out int[] dirtyTileIds)
    {
        lock (lock_dirty)
        {
            if (all_dirty)
            {
                all_dirty = false;
                dirty_tile_ids.Clear();
                rebuildAll = true;
                dirtyTileIds = null;
                return true;
            }

            if (dirty_tile_ids.Count == 0)
            {
                rebuildAll = false;
                dirtyTileIds = null;
                return false;
            }

            dirtyTileIds = new int[dirty_tile_ids.Count];
            dirty_tile_ids.CopyTo(dirtyTileIds);
            dirty_tile_ids.Clear();
            rebuildAll = false;
            return true;
        }
    }

    [Hotfixable]
    public override void update(float pElapsed)
    {
        if (pixels == null)
            createTextureNew();

        if (sprRnd == null) sprRnd = GetComponent<SpriteRenderer>();

        var mapMode = ModClass.I.CustomMapModeManager.UpdateCurrentMapMode();
        if (mapMode == null)
        {
            Hide();
            return;
        }

        ModClass.I.CustomMapModeManager.UpdateInteractionState(mapMode);

        Show();

        spr_color.a = World.world.zone_calculator._night_multiplier * (MapBox.isRenderMiniMap()
            ? World.world.zone_calculator.minimap_opacity
            : Mathf.Clamp(ZoneCalculator.getCameraScaleZoom() * 0.3f, 0f, 0.7f));

        bool renderer_was_disabled = !sprRnd.enabled;
        sprRnd.enabled = true;
        sprRnd.color = spr_color;
        if (renderer_was_disabled) SetAllDirty();

        if (!need_update) return;

        lock (lock_pixels)
        {
            updatePixels();
            need_update = false;
        }

        base.update(pElapsed);
    }

    internal void PreparePixels()
    {
        if (pixels == null || sprRnd == null || !sprRnd.enabled) return;

        CustomMapModeAsset map_mode = ModClass.I.CustomMapModeManager.UpdateCurrentMapMode();
        if (map_mode == null) return;
        if (!CanPreparePixels()) return;
        if (!ConsumeDirty(out bool rebuildAll, out int[] dirtyTileIds)) return;

        WorldTile[] tiles = World.world.tiles_list;
        if (rebuildAll)
        {
            if (mirror_pixels == null || mirror_pixels.Length != pixels.Length) mirror_pixels = new Color32[pixels.Length];
            ClearAll(mirror_pixels);
            for (int i = 0; i < mirror_pixels.Length; i++)
            {
                RenderTile(map_mode, tiles[i], i, mirror_pixels);
            }

            lock (lock_pixels)
            {
                (pixels, mirror_pixels) = (mirror_pixels, pixels);
                need_update = true;
            }
        }
        else
        {
            lock (lock_pixels)
            {
                for (int i = 0; i < dirtyTileIds.Length; i++)
                {
                    int tileId = dirtyTileIds[i];
                    if (tileId < 0 || tileId >= pixels.Length || tileId >= tiles.Length) continue;
                    RenderTile(map_mode, tiles[tileId], tileId, pixels);
                }

                need_update = true;
            }
        }
    }

    private bool CanPreparePixels()
    {
        if (MapBox.width <= 0 || MapBox.height <= 0) return false;
        if (World.world?.tiles_list == null) return false;
        if (pixels.Length != World.world.tiles_list.Length) return false;
        if (ModClass.I?.TileExtendManager == null) return false;
        return ModClass.I.TileExtendManager.Ready();
    }

    private static void ClearAll(Color32[] pPixels)
    {
        for (int i = 0; i < pPixels.Length; i++)
            pPixels[i] = Color.clear;
        return;
    }

    private static void RenderTile(CustomMapModeAsset mapMode, WorldTile tile, int pixelIndex, Color32[] pPixels)
    {
        Color32 color = Color.clear;
        if (tile != null)
        {
            mapMode.kernel_func(tile, ref color);
        }

        pPixels[pixelIndex] = color;
    }

    private static bool TryGetTileId(WorldTile tile, out int tileId)
    {
        tileId = -1;
        if (tile == null || tile.data == null) return false;
        tileId = tile.data.tile_id;
        if (tileId < 0) return false;

        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId < tiles.Length;
    }

    private static bool ClearZone(Color32[] pPixels, TileZone pZone)
    {
        if (pZone.last_drawn_id == -1) return false;
        pZone.last_drawn_id = -1;
        pZone.last_drawn_hashcode = -1;
        foreach (WorldTile tile in pZone.tiles) pPixels[tile.data.tile_id] = Color.clear;

        return true;
    }
}
