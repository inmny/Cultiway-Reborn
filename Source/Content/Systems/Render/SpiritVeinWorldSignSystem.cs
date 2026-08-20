using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.MapModeVisuals;
using Cultiway.Content.SpiritVeins;
using Cultiway.Core;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>在普通地图上为主穴和高阶副穴显示克制的自然征兆。</summary>
internal sealed class SpiritVeinWorldSignSystem : BaseSystem, IWorldStateClearable
{
    private readonly List<WorldSignVisual> visuals = new();
    private SpiritVeinManager manager;
    private GameObject root;
    private int topologyRevision = -1;
    private int displayRevision = -1;

    protected override void OnUpdateGroup()
    {
        EnsureRoot();
        if (root == null) return;
        manager = WorldboxGame.I?.SpiritVeins;
        bool mapModeActive = MapModes.SpiritVein != null &&
                             PlayerConfig.optionBoolEnabled(MapModes.SpiritVein.toggle_name);
        bool show = manager?.IsReady == true && !mapModeActive && !MapBox.isRenderMiniMap();
        root.SetActive(show);
        if (!show) return;
        if (topologyRevision != manager.TopologyRevision)
        {
            Rebuild();
            topologyRevision = manager.TopologyRevision;
            displayRevision = -1;
        }
        if (displayRevision != manager.DisplayRevision)
        {
            RefreshColors();
            displayRevision = manager.DisplayRevision;
        }
        Animate();
    }

    void IWorldStateClearable.ClearWorldState()
    {
        visuals.Clear();
        topologyRevision = -1;
        displayRevision = -1;
        manager = null;
        if (root != null) UnityEngine.Object.Destroy(root);
        root = null;
    }

    private void EnsureRoot()
    {
        if (root != null || World.world == null) return;
        root = new GameObject("[layer] Spirit Ground Omens");
        root.transform.SetParent(World.world.transform, false);
    }

    private void Rebuild()
    {
        IReadOnlyList<GatheringGround> grounds = manager.Grounds;
        var visibleGrounds = new List<GatheringGround>();
        for (int i = 0; i < grounds.Count; i++)
        {
            GatheringGround ground = grounds[i];
            bool important = ground.Kind == GatheringGroundKind.Main ||
                             ground.Kind == GatheringGroundKind.Crossing ||
                             ground.Quality >= GatheringGroundQuality.Upper;
            if (important && ground.Kind != GatheringGroundKind.Remnant) visibleGrounds.Add(ground);
        }
        EnsureVisualCount(visibleGrounds.Count);
        for (int i = 0; i < visuals.Count; i++)
        {
            WorldSignVisual visual = visuals[i];
            if (i >= visibleGrounds.Count)
            {
                visual.Root.SetActive(false);
                continue;
            }
            GatheringGround ground = visibleGrounds[i];
            SpiritVeinEye eye = manager.GetEye(ground.EyeId);
            visual.Ground = ground;
            visual.Eye = eye;
            visual.Phase = Stable01(ground.CenterTileId, ground.Id * 29);
            int tileId = eye?.TileId ?? ground.CenterTileId;
            visual.Root.transform.localPosition = new Vector3(
                tileId % MapBox.width + 0.5f,
                tileId / MapBox.width + 0.5f,
                0f);
            visual.Pattern.sprite = SpiritVeinMapRenderer.ResolveManifestationSprite(
                eye?.Manifestation ?? SpiritEyeManifestation.EarthBreath);
            float qualityScale = 1.25f + (int)ground.Quality * 0.32f;
            if (ground.Kind == GatheringGroundKind.Main) qualityScale *= 1.15f;
            visual.BaseScale = Mathf.Clamp(qualityScale, 1.4f, 3.4f);
            visual.Mist.transform.localScale = new Vector3(visual.BaseScale * 1.4f, visual.BaseScale, 1f);
            visual.Pattern.transform.localScale = Vector3.one * visual.BaseScale;
            visual.Core.transform.localScale = Vector3.one * visual.BaseScale * 0.32f;
            visual.Root.SetActive(true);
        }
        RefreshColors();
    }

    private void EnsureVisualCount(int count)
    {
        while (visuals.Count < count)
        {
            GameObject visualRoot = new("SpiritGroundOmen_" + visuals.Count);
            visualRoot.transform.SetParent(root.transform, false);
            SpriteRenderer mist = CreateRenderer(
                visualRoot.transform,
                "Mist",
                SpiritVeinVisualResources.Mist,
                -9);
            SpriteRenderer pattern = CreateRenderer(
                visualRoot.transform,
                "Manifestation",
                SpiritVeinVisualResources.Breath,
                -8);
            SpriteRenderer core = CreateRenderer(
                visualRoot.transform,
                "Core",
                SpiritVeinVisualResources.Core,
                -7);
            visuals.Add(new WorldSignVisual(visualRoot, mist, pattern, core));
        }
    }

    private void RefreshColors()
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            WorldSignVisual visual = visuals[i];
            if (!visual.Root.activeSelf || visual.Ground == null) continue;
            SpiritVeinSection section = manager.GetSection(visual.Ground.SectionId);
            ElementComposition composition = visual.Eye?.Composition ?? section?.Composition ?? default;
            Color color = SpiritVeinMapRenderer.ResolveInkColor(composition, visual.Ground.Purity);
            float state = Mathf.Lerp(0.08f, 1f, visual.Ground.FillRatio) *
                          Mathf.Lerp(0.2f, 1f, visual.Ground.Purity);
            Color mist = Color.Lerp(color, Color.white, 0.28f);
            mist.a = 0.065f * state;
            visual.Mist.color = mist;
            Color pattern = Color.Lerp(color, Color.white, 0.18f);
            pattern.a = 0.14f * state;
            visual.Pattern.color = pattern;
            Color core = Color.Lerp(color, Color.white, 0.46f);
            core.a = 0.24f * state;
            visual.Core.color = core;
        }
    }

    private void Animate()
    {
        float time = Time.unscaledTime;
        for (int i = 0; i < visuals.Count; i++)
        {
            WorldSignVisual visual = visuals[i];
            if (!visual.Root.activeSelf || visual.Ground == null) continue;
            float phase = time + visual.Phase * 6.28f;
            SpiritEyeManifestation manifestation = visual.Eye?.Manifestation ?? SpiritEyeManifestation.EarthBreath;
            float breath = 1f + Mathf.Sin(phase * 0.55f) * 0.06f;
            visual.Mist.transform.localScale = new Vector3(
                visual.BaseScale * 1.4f * breath,
                visual.BaseScale / breath,
                1f);
            float speed = manifestation switch
            {
                SpiritEyeManifestation.WindEye => 5f,
                SpiritEyeManifestation.ChaosBreath => -3.5f,
                SpiritEyeManifestation.SpiritSpring => 1f,
                _ => 0.35f
            };
            visual.Pattern.transform.localRotation = Quaternion.Euler(0f, 0f, phase * speed);
            float pulse = manifestation == SpiritEyeManifestation.FireCave
                ? 0.88f + Mathf.Abs(Mathf.Sin(phase * 1.8f)) * 0.18f
                : 0.96f + Mathf.Sin(phase * 0.72f) * 0.06f;
            visual.Pattern.transform.localScale = Vector3.one * visual.BaseScale * pulse;
        }
    }

    private static SpriteRenderer CreateRenderer(
        Transform parent,
        string name,
        Sprite sprite,
        int order)
    {
        GameObject gameObject = new(name, typeof(SpriteRenderer));
        gameObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static float Stable01(int seed, int value)
    {
        unchecked
        {
            uint mixed = (uint)seed * 1664525u + (uint)value * 1013904223u;
            mixed ^= mixed >> 16;
            return (mixed & 0x00ffffffu) / 16777215f;
        }
    }

    private sealed class WorldSignVisual
    {
        internal WorldSignVisual(GameObject root, SpriteRenderer mist, SpriteRenderer pattern, SpriteRenderer core)
        {
            Root = root;
            Mist = mist;
            Pattern = pattern;
            Core = core;
        }
        internal GameObject Root { get; }
        internal SpriteRenderer Mist { get; }
        internal SpriteRenderer Pattern { get; }
        internal SpriteRenderer Core { get; }
        internal GatheringGround Ground { get; set; }
        internal SpiritVeinEye Eye { get; set; }
        internal float BaseScale { get; set; }
        internal float Phase { get; set; }
    }
}
