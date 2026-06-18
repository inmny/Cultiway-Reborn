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
    private readonly        object            lock_all_dirty     = new();
    private readonly        object            lock_pixels        = new();
    private readonly        AutoResetEvent    dirty_event        = new(true);
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
        Monitor.Enter(lock_all_dirty);
        all_dirty = true;
        Monitor.Exit(lock_all_dirty);
        dirty_event.Set();
    }

    internal void WaitForDirty(int pMillisecondsTimeout)
    {
        dirty_event.WaitOne(pMillisecondsTimeout);
    }

    private bool ConsumeAllDirty()
    {
        Monitor.Enter(lock_all_dirty);
        try
        {
            if (!all_dirty) return false;
            all_dirty = false;
            return true;
        }
        finally
        {
            Monitor.Exit(lock_all_dirty);
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

        Monitor.Enter(lock_pixels);
        updatePixels();
        need_update = false;
        Monitor.Exit(lock_pixels);

        base.update(pElapsed);
    }

    internal void PreparePixels()
    {
        if (pixels == null || sprRnd == null || !sprRnd.enabled) return;

        CustomMapModeAsset map_mode = ModClass.I.CustomMapModeManager.UpdateCurrentMapMode();
        if (map_mode == null) return;
        if (!CanPreparePixels()) return;
        if (!ConsumeAllDirty()) return;

        if (mirror_pixels == null || mirror_pixels.Length != pixels.Length) mirror_pixels = new Color32[pixels.Length];

        ClearAll(mirror_pixels);

        // Update mirror_pixels
        for (int i = 0; i < mirror_pixels.Length; i++)
        {
            int x = i % textureWidth;
            int y = i / textureWidth;
            map_mode.kernel_func(x, y, ref mirror_pixels[i]);
        }

        Monitor.Enter(lock_pixels);
        (pixels, mirror_pixels) = (mirror_pixels, pixels);
        need_update = true;
        Monitor.Exit(lock_pixels);
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

    private static bool ClearZone(Color32[] pPixels, TileZone pZone)
    {
        if (pZone.last_drawn_id == -1) return false;
        pZone.last_drawn_id = -1;
        pZone.last_drawn_hashcode = -1;
        foreach (WorldTile tile in pZone.tiles) pPixels[tile.data.tile_id] = Color.clear;

        return true;
    }
}
