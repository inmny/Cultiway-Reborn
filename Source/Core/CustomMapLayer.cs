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
    private                 bool              all_dirty          = true;
    private                 float             force_update_timer = 1;
    private                 Color32[]         mirror_pixels;

    private bool need_update = true;

    private Color spr_color = Color.white;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    internal void SetAllDirty()
    {
        Monitor.Enter(lock_all_dirty);
        all_dirty = true;
        Monitor.Exit(lock_all_dirty);
    }

    [Hotfixable]
    public override void update(float pElapsed)
    {
        if (pixels == null)
            createTextureNew();

        if (sprRnd == null) sprRnd = GetComponent<SpriteRenderer>();

        if (ModClass.I.CustomMapModeManager.CurrMapMode == null)
        {
            Hide();
            return;
        }

        Show();

        spr_color.a = World.world.zone_calculator._night_multiplier * (MapBox.isRenderMiniMap()
            ? World.world.zone_calculator.minimap_opacity
            : Mathf.Clamp(ZoneCalculator.getCameraScaleZoom() * 0.3f, 0f, 0.7f));

        sprRnd.enabled = true;
        sprRnd.color = spr_color;

        force_update_timer -= pElapsed;
        if (force_update_timer <= 0)
        {
            force_update_timer = 0.2f;
            SetAllDirty();
        }

        if (!need_update) return;

        Monitor.Enter(lock_pixels);
        updatePixels();
        need_update = false;
        Monitor.Exit(lock_pixels);

        base.update(pElapsed);
    }

    internal void PreparePixels()
    {
        if (pixels        == null || !sprRnd.enabled) return;
        if (mirror_pixels == null || mirror_pixels.Length != pixels.Length) mirror_pixels = new Color32[pixels.Length];

        CustomMapModeAsset map_mode = ModClass.I.CustomMapModeManager.CurrMapMode;
        if (map_mode == null) return;

        Array.Copy(pixels, mirror_pixels, pixels.Length);

        var dirty = false;
        if (all_dirty)
        {
            dirty = true;
            Monitor.Enter(lock_all_dirty);
            all_dirty = false;
            Monitor.Exit(lock_all_dirty);

            ClearAll(mirror_pixels);
        }

        // Update mirror_pixels
        for (int i = 0; i < mirror_pixels.Length; i++)
        {
            int x = i % textureWidth;
            int y = i / textureHeight;
            map_mode.kernel_func(x, y, ref mirror_pixels[i]);
        }

        if (!dirty) return;

        Monitor.Enter(lock_pixels);
        (pixels, mirror_pixels) = (mirror_pixels, pixels);
        need_update = true;
        Monitor.Exit(lock_pixels);
    }

    private static void ClearAll(Color32[] pPixels)
    {
        for (int i = 0; i < pPixels.Length; i++)
            pPixels[i] = Color.clear;
        return;
        foreach (TileZone zone in World.world.zone_calculator.zones) ClearZone(pPixels, zone);
        _last_drawn_zones.Clear();
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