using System;
using System.Collections.Generic;
using Cultiway.Content.SpiritVeins;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.MapModeVisuals;

/// <summary>以连续水墨气场、行气墨丝和自然灵眼绘制风水龙脉，并处理脉域悬停。</summary>
internal sealed class SpiritVeinMapRenderer : ICustomMapModeRenderer
{
    private const int WakanOrder = -4;
    private const int WashOrder = -2;
    private const int FieldOrder = 0;
    private const int SourceMistOrder = 1;
    private const int WispOrder = 2;
    private const int SourceBreathOrder = 3;
    private const int GroundMistOrder = 4;
    private const int GroundPatternOrder = 5;
    private const int EyeOrder = 6;
    private readonly GameObject root;
    private readonly GameObject veinRoot;
    private readonly SpriteRenderer wakanRenderer;
    private readonly SpriteRenderer washRenderer;
    private readonly SpriteRenderer fieldRenderer;
    private readonly List<int> activeTileIds = new();
    private readonly List<SourceVisual> sourceVisuals = new();
    private readonly List<WispVisual> wisps = new();
    private readonly List<GroundVisual> groundVisuals = new();
    private SpiritVeinManager manager;
    private SpiritVeinFieldSnapshot field;
    private Texture2D wakanTexture;
    private Sprite wakanSprite;
    private Color32[] wakanPixels = Array.Empty<Color32>();
    private int wakanWidth;
    private int wakanHeight;
    private int wakanDisplayRevision = -1;
    private Texture2D fieldTexture;
    private Sprite fieldSprite;
    private Color32[] fieldPixels = Array.Empty<Color32>();
    private bool visible;
    private bool topologyDirty = true;
    private int topologyRevision = -1;
    private int displayRevision = -1;
    private object hoveredObject;
    private string hoveredType = string.Empty;
    private int hoveredRevision = -1;

    internal SpiritVeinMapRenderer(CustomMapModeManager _)
    {
        root = new GameObject("[layer] Feng Shui Dragon Veins");
        root.transform.SetParent(World.world.transform, false);
        wakanRenderer = CreateSpriteRenderer(root.transform, "WakanDistribution", null, WakanOrder);
        veinRoot = new GameObject("DragonVeins");
        veinRoot.transform.SetParent(root.transform, false);
        washRenderer = CreateSpriteRenderer(veinRoot.transform, "InkWash", SpiritVeinVisualResources.Solid, WashOrder);
        washRenderer.color = new Color(0.12f, 0.14f, 0.13f, 0.28f);
        fieldRenderer = CreateSpriteRenderer(veinRoot.transform, "VeinDomains", null, FieldOrder);
        root.SetActive(false);
    }

    public void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        root.SetActive(value);
        if (value)
        {
            topologyDirty = true;
            topologyRevision = -1;
            displayRevision = -1;
            wakanDisplayRevision = -1;
        }
        else
        {
            HideTooltip();
        }
    }

    public void Update(float elapsed)
    {
        if (!visible) return;

        SpiritVeinMapView view = MapModes.GetSpiritVeinView();
        UpdateWakanLayer(view);

        manager = WorldboxGame.I?.SpiritVeins;
        bool showVeins = view != SpiritVeinMapView.Wakan && manager?.IsReady == true;
        veinRoot.SetActive(showVeins);
        if (!showVeins)
        {
            HideTooltip();
            return;
        }

        if (topologyDirty || topologyRevision != manager.TopologyRevision)
        {
            RebuildTopology();
            topologyRevision = manager.TopologyRevision;
            displayRevision = -1;
        }
        if (displayRevision != manager.DisplayRevision)
        {
            RefreshFieldColors();
            RefreshSourceColors();
            RefreshGroundColors();
            displayRevision = manager.DisplayRevision;
        }
        UpdateSourceAnimation();
        UpdateWisps(elapsed);
        UpdateGroundAnimation();
        UpdateLevelOfDetail();
        UpdateHover();
    }

    public void SetAllDirty()
    {
        topologyDirty = true;
    }

    public void ClearWorld()
    {
        topologyDirty = true;
        topologyRevision = -1;
        displayRevision = -1;
        wakanDisplayRevision = -1;
        manager = null;
        field = null;
        activeTileIds.Clear();
        fieldPixels = Array.Empty<Color32>();
        wakanPixels = Array.Empty<Color32>();
        ReleaseFieldTexture();
        ReleaseWakanTexture();
        SetPoolActive(sourceVisuals, false);
        SetPoolActive(wisps, false);
        SetPoolActive(groundVisuals, false);
        HideTooltip();
    }

    private void UpdateWakanLayer(SpiritVeinMapView view)
    {
        bool showWakan = view == SpiritVeinMapView.Wakan || view == SpiritVeinMapView.Overlay;
        if (!showWakan || !WorldWakanService.IsInitialized)
        {
            wakanRenderer.gameObject.SetActive(false);
            return;
        }

        int width = WorldWakanService.Width;
        int height = WorldWakanService.Height;
        if (wakanTexture == null || wakanWidth != width || wakanHeight != height)
        {
            BuildWakanTexture(width, height);
        }
        if (wakanTexture == null) return;

        wakanRenderer.gameObject.SetActive(true);
        if (wakanDisplayRevision != WorldWakanService.DisplayRevision)
        {
            RefreshWakanColors();
            wakanDisplayRevision = WorldWakanService.DisplayRevision;
        }

        float opacity = 0.7f;
        if (World.world?.zone_calculator != null)
        {
            opacity = World.world.zone_calculator._night_multiplier * (MapBox.isRenderMiniMap()
                ? World.world.zone_calculator.minimap_opacity
                : Mathf.Clamp(ZoneCalculator.getCameraScaleZoom() * 0.3f, 0f, 0.7f));
        }
        wakanRenderer.color = new Color(1f, 1f, 1f, opacity);
    }

    private void BuildWakanTexture(int width, int height)
    {
        ReleaseWakanTexture();
        if (width <= 0 || height <= 0) return;

        wakanWidth = width;
        wakanHeight = height;
        wakanTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Cultiway_WakanDistribution",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        wakanPixels = new Color32[width * height];
        wakanSprite = Sprite.Create(
            wakanTexture,
            new Rect(0f, 0f, width, height),
            Vector2.zero,
            1f,
            0,
            SpriteMeshType.FullRect);
        wakanSprite.name = "Cultiway_WakanDistributionSprite";
        wakanSprite.hideFlags = HideFlags.DontSave;
        wakanRenderer.sprite = wakanSprite;
        wakanRenderer.transform.localPosition = Vector3.zero;
        wakanRenderer.transform.localScale = Vector3.one;
        wakanDisplayRevision = -1;
    }

    private void RefreshWakanColors()
    {
        if (wakanTexture == null || wakanPixels.Length != wakanWidth * wakanHeight) return;
        for (int tileId = 0; tileId < wakanPixels.Length; tileId++)
        {
            wakanPixels[tileId] = MapModes.ResolveCleanWakanColor(tileId);
        }
        wakanTexture.SetPixels32(wakanPixels);
        wakanTexture.Apply(false, false);
    }

    private void RebuildTopology()
    {
        topologyDirty = false;
        field = manager.CreateFieldSnapshot();
        activeTileIds.Clear();
        if (field.Width <= 0 || field.Height <= 0)
        {
            washRenderer.gameObject.SetActive(false);
            fieldRenderer.gameObject.SetActive(false);
            ReleaseFieldTexture();
            SetPoolActive(sourceVisuals, false);
            SetPoolActive(wisps, false);
            SetPoolActive(groundVisuals, false);
            return;
        }

        for (int tileId = 0; tileId < field.PrimaryVeinByTile.Length; tileId++)
        {
            if (field.PrimaryVeinByTile[tileId] >= 0 && field.FieldStrength[tileId] > 0f)
                activeTileIds.Add(tileId);
        }
        washRenderer.gameObject.SetActive(true);
        fieldRenderer.gameObject.SetActive(true);
        BuildFieldTexture();
        RebuildSourceVisuals();
        RebuildWisps();
        RebuildGroundVisuals();
        washRenderer.transform.localPosition = new Vector3(field.Width * 0.5f, field.Height * 0.5f, 0f);
        washRenderer.transform.localScale = new Vector3(field.Width, field.Height, 1f);
    }

    private void BuildFieldTexture()
    {
        ReleaseFieldTexture();
        fieldTexture = new Texture2D(field.Width, field.Height, TextureFormat.RGBA32, false, false)
        {
            name = "Cultiway_DragonVeinInkField",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        fieldPixels = new Color32[field.Width * field.Height];
        fieldSprite = Sprite.Create(
            fieldTexture,
            new Rect(0f, 0f, field.Width, field.Height),
            Vector2.zero,
            1f,
            0,
            SpriteMeshType.FullRect);
        fieldSprite.name = "Cultiway_DragonVeinInkFieldSprite";
        fieldSprite.hideFlags = HideFlags.DontSave;
        fieldRenderer.sprite = fieldSprite;
        fieldRenderer.transform.localPosition = Vector3.zero;
        fieldRenderer.transform.localScale = Vector3.one;
        RefreshFieldColors();
    }

    private void RefreshFieldColors()
    {
        if (fieldTexture == null || fieldPixels.Length != field.Width * field.Height) return;
        Array.Clear(fieldPixels, 0, fieldPixels.Length);
        for (int i = 0; i < activeTileIds.Count; i++)
        {
            int tileId = activeTileIds[i];
            SpiritVeinSection section = manager.GetSection(field.SectionByTile[tileId]);
            if (section == null) continue;
            GatheringGround ground = manager.GetGround(field.GroundByTile[tileId]);
            Color color = ResolveInkColor(section.Composition, section.Purity);
            float fill = Mathf.Lerp(0.16f, 1f, section.FillRatio);
            float patency = Mathf.Lerp(0.25f, 1f, section.Patency);
            float alpha = field.FieldStrength[tileId] *
                          Mathf.Lerp(0.24f, 0.62f, field.Convergence[tileId]) *
                          fill * patency;
            if (ground != null)
            {
                alpha += field.Convergence[tileId] * (0.08f + (int)ground.Quality * 0.025f);
                color = Color.Lerp(color, new Color(0.94f, 0.96f, 0.85f), 0.18f);
            }
            if (section.Status == VeinSectionStatus.Blocked)
                color = Color.Lerp(color, new Color(0.32f, 0.3f, 0.27f), 0.52f);
            color.a = Mathf.Clamp01(alpha);
            fieldPixels[tileId] = color;
        }
        fieldTexture.SetPixels32(fieldPixels);
        fieldTexture.Apply(false, false);
    }

    private void RebuildSourceVisuals()
    {
        IReadOnlyList<SpiritVein> veins = manager.Veins;
        EnsureSourceVisualCount(veins.Count);
        for (int i = 0; i < sourceVisuals.Count; i++)
        {
            SourceVisual visual = sourceVisuals[i];
            if (i >= veins.Count)
            {
                visual.Root.SetActive(false);
                continue;
            }
            SpiritVein vein = veins[i];
            visual.Vein = vein;
            visual.SectionId = vein.SourceCenterTileId >= 0 && vein.SourceCenterTileId < field.SectionByTile.Length
                ? field.SectionByTile[vein.SourceCenterTileId]
                : -1;
            visual.Phase = Stable01(vein.SourceCenterTileId, vein.Id * 31);
            visual.BaseDiameter = Mathf.Clamp(
                Mathf.Sqrt(Mathf.Max(1, vein.SourceTileIds.Length) / Mathf.PI) * 2f,
                5f,
                16f);
            visual.Root.transform.localPosition = TileCenter(vein.SourceCenterTileId, field.Width);
            visual.Root.SetActive(true);
        }
        RefreshSourceColors();
    }

    private void EnsureSourceVisualCount(int count)
    {
        while (sourceVisuals.Count < count)
        {
            GameObject visualRoot = new("AncestorSource_" + sourceVisuals.Count);
            visualRoot.transform.SetParent(veinRoot.transform, false);
            SpriteRenderer mist = CreateSpriteRenderer(
                visualRoot.transform,
                "SourceMist",
                SpiritVeinVisualResources.Mist,
                SourceMistOrder);
            SpriteRenderer breath = CreateSpriteRenderer(
                visualRoot.transform,
                "SourceBreath",
                SpiritVeinVisualResources.Breath,
                SourceBreathOrder);
            sourceVisuals.Add(new SourceVisual(visualRoot, mist, breath));
        }
    }

    private void RefreshSourceColors()
    {
        for (int i = 0; i < sourceVisuals.Count; i++)
        {
            SourceVisual visual = sourceVisuals[i];
            if (!visual.Root.activeSelf || visual.Vein == null) continue;
            SpiritVeinSection section = manager.GetSection(visual.SectionId);
            float purity = section?.Purity ?? 1f;
            float fill = section?.FillRatio ?? 0.5f;
            Color color = ResolveInkColor(visual.Vein.Composition, purity);
            Color mist = Color.Lerp(color, new Color(0.9f, 0.94f, 0.85f), 0.3f);
            mist.a = Mathf.Lerp(0.04f, 0.12f, fill) * Mathf.Lerp(0.35f, 1f, purity);
            visual.Mist.color = mist;
            Color breath = Color.Lerp(color, Color.white, 0.42f);
            breath.a = Mathf.Lerp(0.07f, 0.22f, fill) * Mathf.Lerp(0.3f, 1f, purity);
            visual.Breath.color = breath;
        }
    }

    private void UpdateSourceAnimation()
    {
        float time = Time.unscaledTime;
        for (int i = 0; i < sourceVisuals.Count; i++)
        {
            SourceVisual visual = sourceVisuals[i];
            if (!visual.Root.activeSelf || visual.Vein == null) continue;
            float phase = time * 0.42f + visual.Phase * 6.28f;
            float breath = 1f + Mathf.Sin(phase) * 0.09f;
            visual.Mist.transform.localScale = new Vector3(
                visual.BaseDiameter * breath,
                visual.BaseDiameter * 0.82f / breath,
                1f);
            visual.Breath.transform.localScale = new Vector3(
                visual.BaseDiameter * 0.48f / breath,
                visual.BaseDiameter * 0.72f * breath,
                1f);
            visual.Breath.transform.localPosition = new Vector3(0f, Mathf.Sin(phase) * 0.35f, 0f);
        }
    }

    private void RebuildWisps()
    {
        var candidates = new List<int>();
        for (int i = 0; i < activeTileIds.Count; i++)
        {
            int tileId = activeTileIds[i];
            if (field.FieldStrength[tileId] < 0.24f) continue;
            float magnitude = Mathf.Abs(field.FlowX[tileId]) + Mathf.Abs(field.FlowY[tileId]);
            if (magnitude < 0.1f) continue;
            candidates.Add(tileId);
        }
        int desired = Mathf.Min(candidates.Count, Mathf.Clamp(candidates.Count / 20, 70, 340));
        EnsureWispCount(desired);
        int stride = Mathf.Max(1, candidates.Count / Mathf.Max(1, desired));
        for (int i = 0; i < wisps.Count; i++)
        {
            WispVisual visual = wisps[i];
            if (i >= desired)
            {
                visual.Renderer.gameObject.SetActive(false);
                continue;
            }
            int candidateIndex = Mathf.Min(candidates.Count - 1, i * stride + i % stride);
            int seedTile = candidates[candidateIndex];
            visual.SeedTileId = seedTile;
            visual.VeinId = field.PrimaryVeinByTile[seedTile];
            visual.Phase = Stable01(seedTile, i + 17);
            visual.Speed = Mathf.Lerp(0.42f, 1.15f, Stable01(seedTile, i + 73));
            ResetWisp(visual);
            visual.Renderer.gameObject.SetActive(true);
        }
    }

    private void EnsureWispCount(int count)
    {
        while (wisps.Count < count)
        {
            SpriteRenderer renderer = CreateSpriteRenderer(
                veinRoot.transform,
                "FlowWisp_" + wisps.Count,
                SpiritVeinVisualResources.Wisp,
                WispOrder);
            wisps.Add(new WispVisual(renderer));
        }
    }

    private void ResetWisp(WispVisual visual)
    {
        int tileId = visual.SeedTileId;
        float jitterX = Stable01(tileId, 101 + Mathf.RoundToInt(visual.Phase * 100f)) - 0.5f;
        float jitterY = Stable01(tileId, 211 + Mathf.RoundToInt(visual.Phase * 100f)) - 0.5f;
        visual.Position = TileCenter(tileId, field.Width) + new Vector3(jitterX, jitterY, 0f) * 0.7f;
        visual.Life = Mathf.Lerp(2.4f, 6.2f, visual.Phase);
        visual.Renderer.transform.localPosition = visual.Position;
    }

    private void UpdateWisps(float elapsed)
    {
        if (field == null || field.Width <= 0) return;
        float delta = Mathf.Clamp(elapsed, 0f, 0.1f);
        float time = Time.unscaledTime;
        for (int i = 0; i < wisps.Count; i++)
        {
            WispVisual visual = wisps[i];
            if (!visual.Renderer.gameObject.activeSelf) continue;
            int x = Mathf.FloorToInt(visual.Position.x);
            int y = Mathf.FloorToInt(visual.Position.y);
            int tileId = x >= 0 && x < field.Width && y >= 0 && y < field.Height
                ? y * field.Width + x
                : -1;
            visual.Life -= delta;
            if (tileId < 0 || visual.Life <= 0f || field.PrimaryVeinByTile[tileId] != visual.VeinId ||
                field.FieldStrength[tileId] < SpiritVeinSettings.FieldMinimumStrength)
            {
                ResetWisp(visual);
                continue;
            }
            SpiritVeinSection section = manager.GetSection(field.SectionByTile[tileId]);
            float directionX = field.FlowX[tileId];
            float directionY = field.FlowY[tileId];
            float purity = section?.Purity ?? 1f;
            if (purity < 0.5f)
            {
                int reversePeriod = Mathf.FloorToInt(time * 0.22f);
                float reverseChance = (0.5f - purity) * 1.25f;
                if (Stable01(visual.SeedTileId, reversePeriod + i * 43) < reverseChance)
                {
                    directionX = -directionX;
                    directionY = -directionY;
                }
            }
            float patency = section?.Patency ?? 1f;
            float speed = visual.Speed *
                          Mathf.Lerp(0.55f, 1.35f, field.FieldStrength[tileId]) *
                          Mathf.Lerp(0.12f, 1f, patency);
            visual.Position += new Vector3(directionX, directionY, 0f) * speed * delta;
            visual.Renderer.transform.localPosition = visual.Position;
            visual.Renderer.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(directionY, directionX) * Mathf.Rad2Deg);
            Color color = section == null
                ? new Color(0.9f, 0.94f, 0.82f)
                : ResolveInkColor(section.Composition, section.Purity);
            float pulse = 0.72f + Mathf.Sin(time * 1.4f + visual.Phase * 6.28f) * 0.2f;
            color = Color.Lerp(color, Color.white, 0.42f);
            color.a = field.FieldStrength[tileId] * pulse * (section == null ? 0.5f : Mathf.Lerp(0.18f, 0.72f, section.FillRatio));
            visual.Renderer.color = color;
            float scale = Mathf.Lerp(0.68f, 1.35f, field.FieldStrength[tileId]);
            visual.Renderer.transform.localScale = new Vector3(scale, scale * 0.72f, 1f);
        }
    }

    private void RebuildGroundVisuals()
    {
        IReadOnlyList<GatheringGround> grounds = manager.Grounds;
        EnsureGroundVisualCount(grounds.Count);
        for (int i = 0; i < groundVisuals.Count; i++)
        {
            GroundVisual visual = groundVisuals[i];
            if (i >= grounds.Count)
            {
                visual.Root.SetActive(false);
                continue;
            }
            GatheringGround ground = grounds[i];
            SpiritVeinEye eye = manager.GetEye(ground.EyeId);
            visual.Ground = ground;
            visual.Eye = eye;
            visual.Phase = Stable01(ground.CenterTileId, ground.Id * 17);
            visual.Root.transform.localPosition = TileCenter(eye?.TileId ?? ground.CenterTileId, field.Width);
            visual.Pattern.sprite = ResolveManifestationSprite(eye?.Manifestation ?? SpiritEyeManifestation.EarthBreath);
            float diameter = Mathf.Clamp(Mathf.Sqrt(ground.TileIds.Length / Mathf.PI) * 2f, 3.8f, 18f);
            visual.BaseDiameter = diameter;
            visual.Mist.transform.localScale = new Vector3(diameter, diameter * 0.82f, 1f);
            visual.Pattern.transform.localScale = new Vector3(diameter * 0.62f, diameter * 0.62f, 1f);
            visual.Core.transform.localScale = Vector3.one * Mathf.Clamp(diameter * 0.18f, 0.65f, 2.2f);
            visual.Root.SetActive(true);
        }
        RefreshGroundColors();
    }

    private void EnsureGroundVisualCount(int count)
    {
        while (groundVisuals.Count < count)
        {
            GameObject visualRoot = new("GatheringGround_" + groundVisuals.Count);
            visualRoot.transform.SetParent(veinRoot.transform, false);
            SpriteRenderer mist = CreateSpriteRenderer(
                visualRoot.transform,
                "Mist",
                SpiritVeinVisualResources.Mist,
                GroundMistOrder);
            SpriteRenderer pattern = CreateSpriteRenderer(
                visualRoot.transform,
                "Manifestation",
                SpiritVeinVisualResources.Spiral,
                GroundPatternOrder);
            SpriteRenderer core = CreateSpriteRenderer(
                visualRoot.transform,
                "EyeBreath",
                SpiritVeinVisualResources.Core,
                EyeOrder);
            groundVisuals.Add(new GroundVisual(visualRoot, mist, pattern, core));
        }
    }

    private void RefreshGroundColors()
    {
        for (int i = 0; i < groundVisuals.Count; i++)
        {
            GroundVisual visual = groundVisuals[i];
            if (!visual.Root.activeSelf || visual.Ground == null) continue;
            SpiritVeinSection section = manager.GetSection(visual.Ground.SectionId);
            ElementComposition composition = visual.Eye?.Composition ?? section?.Composition ?? default;
            float purity = visual.Ground.Purity;
            Color color = ResolveInkColor(composition, purity);
            float state = Mathf.Lerp(0.18f, 1f, visual.Ground.FillRatio);
            Color mist = Color.Lerp(color, new Color(0.9f, 0.94f, 0.85f), 0.34f);
            mist.a = (0.13f + (int)visual.Ground.Quality * 0.025f) * state;
            visual.Mist.color = mist;
            Color pattern = Color.Lerp(color, Color.white, 0.28f);
            pattern.a = (0.22f + visual.Ground.Convergence * 0.2f) * state;
            visual.Pattern.color = pattern;
            Color core = Color.Lerp(color, Color.white, 0.55f);
            core.a = Mathf.Lerp(0.2f, 0.82f, state) * Mathf.Lerp(0.35f, 1f, purity);
            visual.Core.color = core;
        }
    }

    private void UpdateGroundAnimation()
    {
        float time = Time.unscaledTime;
        for (int i = 0; i < groundVisuals.Count; i++)
        {
            GroundVisual visual = groundVisuals[i];
            if (!visual.Root.activeSelf || visual.Ground == null) continue;
            SpiritEyeManifestation manifestation = visual.Eye?.Manifestation ?? SpiritEyeManifestation.EarthBreath;
            float phase = time + visual.Phase * 6.28f;
            float breath = 1f + Mathf.Sin(phase * 0.62f) * 0.07f;
            visual.Mist.transform.localScale = new Vector3(
                visual.BaseDiameter * breath,
                visual.BaseDiameter * 0.82f / breath,
                1f);
            float rotationSpeed = manifestation switch
            {
                SpiritEyeManifestation.WindEye => 9f,
                SpiritEyeManifestation.ChaosBreath => -6f,
                SpiritEyeManifestation.SpiritSpring => 2f,
                _ => 0.7f
            };
            visual.Pattern.transform.localRotation = Quaternion.Euler(0f, 0f, phase * rotationSpeed);
            float patternPulse = manifestation == SpiritEyeManifestation.SpiritSpring
                ? 0.85f + Mathf.Repeat(phase * 0.12f, 1f) * 0.35f
                : 0.95f + Mathf.Sin(phase * 0.9f) * 0.08f;
            visual.Pattern.transform.localScale = Vector3.one * visual.BaseDiameter * 0.62f * patternPulse;
            float corePulse = manifestation == SpiritEyeManifestation.FireCave
                ? 0.75f + Mathf.Abs(Mathf.Sin(phase * 2.2f)) * 0.36f
                : 0.92f + Mathf.Sin(phase * 1.1f) * 0.12f;
            visual.Core.transform.localScale = Vector3.one * Mathf.Clamp(visual.BaseDiameter * 0.18f, 0.65f, 2.2f) * corePulse;
            visual.Core.transform.localPosition = manifestation == SpiritEyeManifestation.EarthBreath
                ? new Vector3(0f, Mathf.Sin(phase * 0.8f) * 0.28f, 0f)
                : Vector3.zero;
        }
    }

    private void UpdateLevelOfDetail()
    {
        float size = World.world?.camera == null ? 20f : World.world.camera.orthographicSize;
        for (int i = 0; i < sourceVisuals.Count; i++)
        {
            sourceVisuals[i].Mist.enabled = true;
            sourceVisuals[i].Breath.enabled = size < 92f;
        }
        bool showWisps = size < 78f;
        int wispStep = size > 46f ? 3 : size > 28f ? 2 : 1;
        for (int i = 0; i < wisps.Count; i++)
            wisps[i].Renderer.enabled = showWisps && i % wispStep == 0;
        for (int i = 0; i < groundVisuals.Count; i++)
        {
            GroundVisual visual = groundVisuals[i];
            if (visual.Ground == null) continue;
            bool important = visual.Ground.Kind == GatheringGroundKind.Main ||
                             visual.Ground.Kind == GatheringGroundKind.Crossing ||
                             visual.Ground.Quality >= GatheringGroundQuality.Upper;
            bool show = size < 62f || important;
            visual.Mist.enabled = show;
            visual.Pattern.enabled = show && size < 85f;
            visual.Core.enabled = size < 110f;
        }
    }

    private void UpdateHover()
    {
        if (World.world == null ||
            Tooltip.findActive(tooltip => tooltip.asset != Content.Tooltips.SpiritVein) != null ||
            Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
            Input.mouseScrollDelta.y != 0f)
        {
            HideTooltip();
            return;
        }
        WorldTile tile = World.world.getMouseTilePosCachedFrame();
        if (tile?.data == null)
        {
            HideTooltip();
            return;
        }
        int tileId = tile.data.tile_id;
        SpiritVeinEye eye = manager.FindEyeNearTile(tileId, 2);
        object selected = null;
        SpiritVein tooltipOwner = null;
        string targetType = string.Empty;
        int targetId = -1;
        if (eye != null && TileDistance(tileId, eye.TileId, field.Width) <= 2)
        {
            selected = eye;
            tooltipOwner = manager.GetVeinByTopologyId(eye.VeinId);
            targetType = "eye";
            targetId = eye.Id;
        }
        else
        {
            GatheringGround ground = manager.GetGroundAtTile(tileId);
            if (ground != null)
            {
                selected = ground;
                tooltipOwner = manager.GetVeinByTopologyId(ground.PrimaryVeinId);
                targetType = "ground";
                targetId = ground.Id;
            }
            else
            {
                SpiritVeinSection section = manager.GetSectionAtTile(tileId);
                if (section != null)
                {
                    selected = section;
                    tooltipOwner = manager.GetVeinByTopologyId(section.VeinId);
                    targetType = "section";
                    targetId = section.Id;
                }
            }
        }
        if (selected == null)
        {
            HideTooltip();
            return;
        }

        bool tooltipActive = Tooltip.findActive(tooltip => tooltip.asset == Content.Tooltips.SpiritVein) != null;
        if (ReferenceEquals(selected, hoveredObject) && hoveredType == targetType &&
            hoveredRevision == manager.DisplayRevision && tooltipActive)
        {
            return;
        }
        hoveredObject = selected;
        hoveredType = targetType;
        hoveredRevision = manager.DisplayRevision;
        Tooltip.show(
            tooltipOwner ?? selected,
            Content.Tooltips.SpiritVein.id,
            new TooltipData
            {
                tip_name = targetType + ":" + targetId,
                tooltip_scale = 0.78f,
                is_sim_tooltip = false,
                sound_allowed = false
            });
    }

    private void HideTooltip()
    {
        hoveredObject = null;
        hoveredType = string.Empty;
        hoveredRevision = -1;
        Tooltip active = Tooltip.findActive(tooltip => tooltip.asset == Content.Tooltips.SpiritVein);
        active?.hide();
    }

    private void ReleaseWakanTexture()
    {
        wakanRenderer.sprite = null;
        if (wakanSprite != null) UnityEngine.Object.Destroy(wakanSprite);
        if (wakanTexture != null) UnityEngine.Object.Destroy(wakanTexture);
        wakanSprite = null;
        wakanTexture = null;
        wakanPixels = Array.Empty<Color32>();
        wakanWidth = 0;
        wakanHeight = 0;
        wakanDisplayRevision = -1;
    }

    private void ReleaseFieldTexture()
    {
        fieldRenderer.sprite = null;
        if (fieldSprite != null) UnityEngine.Object.Destroy(fieldSprite);
        if (fieldTexture != null) UnityEngine.Object.Destroy(fieldTexture);
        fieldSprite = null;
        fieldTexture = null;
    }

    private static SpriteRenderer CreateSpriteRenderer(
        Transform parent,
        string name,
        Sprite sprite,
        int sortingOrder)
    {
        GameObject layer = new(name, typeof(SpriteRenderer));
        layer.transform.SetParent(parent, false);
        SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = RenderSortingLayerNames.MapOverlay_6;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    internal static Color ResolveInkColor(ElementComposition composition, float purity)
    {
        Color element = new(0.72f, 0.85f, 0.76f);
        string hex = composition.HexColor();
        if (ColorUtility.TryParseHtmlString(
                hex.StartsWith("#", StringComparison.Ordinal) ? hex : "#" + hex,
                out Color parsed))
        {
            element = parsed;
        }
        Color jadeInk = new(0.82f, 0.89f, 0.8f);
        Color color = Color.Lerp(jadeInk, element, 0.34f);
        Color polluted = new(0.34f, 0.32f, 0.29f);
        return Color.Lerp(polluted, color, Mathf.Lerp(0.22f, 1f, Mathf.Clamp01(purity)));
    }

    internal static Sprite ResolveManifestationSprite(SpiritEyeManifestation manifestation)
    {
        return manifestation switch
        {
            SpiritEyeManifestation.SpiritSpring => SpiritVeinVisualResources.Ripple,
            SpiritEyeManifestation.StoneMarrow => SpiritVeinVisualResources.Fissure,
            SpiritEyeManifestation.WoodBloom => SpiritVeinVisualResources.Bloom,
            SpiritEyeManifestation.WindEye => SpiritVeinVisualResources.Spiral,
            SpiritEyeManifestation.FireCave => SpiritVeinVisualResources.Flame,
            SpiritEyeManifestation.YinPool => SpiritVeinVisualResources.LowMist,
            SpiritEyeManifestation.YangPool => SpiritVeinVisualResources.Ripple,
            SpiritEyeManifestation.ChaosBreath => SpiritVeinVisualResources.Chaos,
            _ => SpiritVeinVisualResources.Breath
        };
    }

    private static Vector3 TileCenter(int tileId, int mapWidth)
    {
        return mapWidth <= 0
            ? Vector3.zero
            : new Vector3(tileId % mapWidth + 0.5f, tileId / mapWidth + 0.5f, 0f);
    }

    private static int TileDistance(int left, int right, int mapWidth)
    {
        return Mathf.Abs(left % mapWidth - right % mapWidth) + Mathf.Abs(left / mapWidth - right / mapWidth);
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

    private static void SetPoolActive(List<SourceVisual> values, bool active)
    {
        for (int i = 0; i < values.Count; i++) values[i].Root.SetActive(active);
    }

    private static void SetPoolActive(List<WispVisual> values, bool active)
    {
        for (int i = 0; i < values.Count; i++) values[i].Renderer.gameObject.SetActive(active);
    }

    private static void SetPoolActive(List<GroundVisual> values, bool active)
    {
        for (int i = 0; i < values.Count; i++) values[i].Root.SetActive(active);
    }

    private sealed class SourceVisual
    {
        internal SourceVisual(GameObject root, SpriteRenderer mist, SpriteRenderer breath)
        {
            Root = root;
            Mist = mist;
            Breath = breath;
        }
        internal GameObject Root { get; }
        internal SpriteRenderer Mist { get; }
        internal SpriteRenderer Breath { get; }
        internal SpiritVein Vein { get; set; }
        internal int SectionId { get; set; }
        internal float BaseDiameter { get; set; }
        internal float Phase { get; set; }
    }

    private sealed class WispVisual
    {
        internal WispVisual(SpriteRenderer renderer)
        {
            Renderer = renderer;
        }
        internal SpriteRenderer Renderer { get; }
        internal int SeedTileId { get; set; }
        internal int VeinId { get; set; }
        internal Vector3 Position { get; set; }
        internal float Phase { get; set; }
        internal float Speed { get; set; }
        internal float Life { get; set; }
    }

    private sealed class GroundVisual
    {
        internal GroundVisual(GameObject root, SpriteRenderer mist, SpriteRenderer pattern, SpriteRenderer core)
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
        internal float BaseDiameter { get; set; }
        internal float Phase { get; set; }
    }
}

/// <summary>望气图与普通地图征兆共用的运行时水墨纹理。</summary>
internal static class SpiritVeinVisualResources
{
    internal static readonly Sprite Solid = Build("Solid", 4, 4, SpiritInkShape.Solid, 4f);
    internal static readonly Sprite Mist = Build("Mist", 64, 64, SpiritInkShape.Mist, 64f);
    internal static readonly Sprite LowMist = Build("LowMist", 64, 64, SpiritInkShape.LowMist, 64f);
    internal static readonly Sprite Ripple = Build("Ripple", 64, 64, SpiritInkShape.Ripple, 64f);
    internal static readonly Sprite Spiral = Build("Spiral", 64, 64, SpiritInkShape.Spiral, 64f);
    internal static readonly Sprite Breath = Build("Breath", 64, 64, SpiritInkShape.Breath, 64f);
    internal static readonly Sprite Fissure = Build("Fissure", 64, 64, SpiritInkShape.Fissure, 64f);
    internal static readonly Sprite Bloom = Build("Bloom", 64, 64, SpiritInkShape.Bloom, 64f);
    internal static readonly Sprite Flame = Build("Flame", 64, 64, SpiritInkShape.Flame, 64f);
    internal static readonly Sprite Chaos = Build("Chaos", 64, 64, SpiritInkShape.Chaos, 64f);
    internal static readonly Sprite Core = Build("Core", 32, 32, SpiritInkShape.Core, 32f);
    internal static readonly Sprite Wisp = Build("Wisp", 64, 16, SpiritInkShape.Wisp, 16f);

    private static Sprite Build(string name, int width, int height, SpiritInkShape shape, float pixelsPerUnit)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Cultiway_SpiritInk_" + name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width * 2f - 1f;
                float ny = (y + 0.5f) / height * 2f - 1f;
                float alpha = ResolveAlpha(nx, ny, shape);
                if (shape != SpiritInkShape.Solid)
                {
                    float edgeDistance = 1f - Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                    alpha *= Mathf.SmoothStep(0f, 0.24f, edgeDistance);
                }
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "Cultiway_SpiritInk_" + name + "Sprite";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static float ResolveAlpha(float x, float y, SpiritInkShape shape)
    {
        float radius = Mathf.Sqrt(x * x + y * y);
        switch (shape)
        {
            case SpiritInkShape.Solid:
                return 1f;
            case SpiritInkShape.Mist:
                float mistNoise = 0.82f + Mathf.Sin(x * 11f + y * 7f) * 0.1f + Mathf.Sin(y * 17f) * 0.08f;
                return Mathf.Pow(Mathf.Clamp01(1f - radius), 1.7f) * mistNoise;
            case SpiritInkShape.LowMist:
                float lowRadius = Mathf.Sqrt(x * x * 0.65f + (y + 0.25f) * (y + 0.25f) * 2.2f);
                return Mathf.Pow(Mathf.Clamp01(1f - lowRadius), 1.55f) * 0.78f;
            case SpiritInkShape.Ripple:
                float ripple = Mathf.Max(
                    Ring(radius, 0.3f, 0.045f),
                    Mathf.Max(Ring(radius, 0.56f, 0.045f), Ring(radius, 0.82f, 0.04f)));
                return ripple * Mathf.Clamp01(1f - radius * 0.12f);
            case SpiritInkShape.Spiral:
                float angle = Mathf.Atan2(y, x);
                float spiralDistance = Mathf.Abs(Mathf.Repeat(radius * 2.6f - angle / (Mathf.PI * 2f) + 0.5f, 1f) - 0.5f);
                return (1f - Mathf.SmoothStep(0.035f, 0.13f, spiralDistance)) * Mathf.Clamp01(1f - radius * 0.72f);
            case SpiritInkShape.Breath:
                float plume = Mathf.Abs(x + Mathf.Sin(y * 5f) * 0.16f);
                return (1f - Mathf.SmoothStep(0.08f, 0.32f, plume)) * Mathf.Clamp01((y + 1f) * 0.7f) * Mathf.Clamp01(1f - radius * 0.55f);
            case SpiritInkShape.Fissure:
                float crack = Mathf.Abs(x + Mathf.Sin(y * 8f) * 0.12f + Mathf.Sin(y * 19f) * 0.045f);
                float branch = Mathf.Abs(x - y * 0.42f - 0.12f);
                return Mathf.Max(
                    1f - Mathf.SmoothStep(0.025f, 0.11f, crack),
                    (1f - Mathf.SmoothStep(0.02f, 0.075f, branch)) * 0.65f) * Mathf.Clamp01(1f - radius * 0.6f);
            case SpiritInkShape.Bloom:
                float bloomAngle = Mathf.Atan2(y, x);
                float petal = 0.55f + Mathf.Cos(bloomAngle * 6f) * 0.17f;
                return (1f - Mathf.SmoothStep(0.05f, 0.2f, Mathf.Abs(radius - petal))) * 0.82f +
                       Mathf.Pow(Mathf.Clamp01(0.35f - radius), 1.3f);
            case SpiritInkShape.Flame:
                float flameX = x * (1.25f + (y + 1f) * 0.28f) + Mathf.Sin(y * 8f) * 0.12f;
                float flame = 1f - Mathf.SmoothStep(0.08f, 0.46f, Mathf.Abs(flameX));
                return flame * Mathf.Clamp01((y + 1f) * 0.85f) * Mathf.Clamp01(1.1f - radius);
            case SpiritInkShape.Chaos:
                float chaos = Mathf.Sin(x * 13f + y * 7f) * Mathf.Cos(y * 11f - x * 5f);
                return Mathf.Clamp01(chaos * 0.55f + 0.35f) * Mathf.Pow(Mathf.Clamp01(1f - radius), 0.75f);
            case SpiritInkShape.Core:
                return Mathf.Pow(Mathf.Clamp01(1f - radius), 2.5f);
            case SpiritInkShape.Wisp:
                float longitudinal = Mathf.Clamp01(1f - Mathf.Abs(x - 0.28f) * 0.76f);
                float transverse = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 2.3f);
                return longitudinal * transverse * Mathf.SmoothStep(0f, 0.5f, x + 1f);
            default:
                return 0f;
        }
    }

    private static float Ring(float radius, float target, float width)
    {
        return 1f - Mathf.SmoothStep(width * 0.35f, width, Mathf.Abs(radius - target));
    }

    private enum SpiritInkShape
    {
        Solid,
        Mist,
        LowMist,
        Ripple,
        Spiral,
        Breath,
        Fissure,
        Bloom,
        Flame,
        Chaos,
        Core,
        Wisp
    }
}
