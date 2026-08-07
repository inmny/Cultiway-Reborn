using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Content.MapModeVisuals;

internal sealed class KingdomMapRenderer : ICustomMapModeRenderer
{
    private const float FillOpacity = 0.72f;
    private const float BorderOpacity = 0.98f;
    private const float VanillaMaximumMapOpacity = 0.78f;
    private readonly GameObject root;
    private readonly Mesh fillMesh;
    private readonly Mesh borderMesh;
    private readonly MeshRenderer fillRenderer;
    private readonly MeshRenderer borderRenderer;
    private readonly MaterialPropertyBlock fillProperties = new();
    private readonly MaterialPropertyBlock borderProperties = new();
    private Task<KingdomMapGeometry> buildTask;
    private KingdomMapGeometry geometry;
    private bool visible;
    private bool topologyDirty = true;
    private bool colorsDirty = true;
    private int generation;
    private long hoveredKingdomId = long.MinValue;
    private long selectedKingdomId = long.MinValue;

    internal KingdomMapRenderer(CustomMapModeManager _)
    {
        root = new GameObject("[layer] Strategic Kingdom Map");
        root.transform.SetParent(World.world.transform, false);
        fillRenderer = CreateLayer(root.transform, "fill", 1, out fillMesh);
        borderRenderer = CreateLayer(root.transform, "border", 2, out borderMesh);
        fillRenderer.sharedMaterial = StrategicMapVisualResources.FillMaterial;
        borderRenderer.sharedMaterial = StrategicMapVisualResources.BorderMaterial;
        root.SetActive(false);
    }

    public void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        root.SetActive(value);
        if (value) topologyDirty = true;
    }

    public void Update(float elapsed)
    {
        if (!visible) return;
        CompleteBuild();
        if (topologyDirty && buildTask == null) StartBuild();
        if (geometry == null) return;

        if (colorsDirty)
        {
            RefreshColors();
            colorsDirty = false;
        }
        UpdateInteraction();
        UpdateMaterialProperties();
    }

    public void SetAllDirty()
    {
        topologyDirty = true;
    }

    public void ClearWorld()
    {
        generation++;
        buildTask = null;
        geometry = null;
        topologyDirty = true;
        colorsDirty = true;
        hoveredKingdomId = long.MinValue;
        selectedKingdomId = long.MinValue;
        fillMesh.Clear();
        borderMesh.Clear();
    }

    internal void MarkZoneDirty(TileZone zone)
    {
        if (zone == null || World.world?.zone_calculator == null) return;
        ZoneCalculator calculator = World.world.zone_calculator;
        if ((uint)zone.x >= (uint)calculator.zones_total_x ||
            (uint)zone.y >= (uint)calculator.zones_total_y) return;
        topologyDirty = true;
    }

    internal void MarkCityDirty(City city)
    {
        foreach (TileZone zone in city.zones) MarkZoneDirty(zone);
    }

    internal void MarkKingdomDirty(Kingdom kingdom)
    {
        foreach (City city in kingdom.cities) MarkCityDirty(city);
    }

    internal void MarkColorsDirty()
    {
        colorsDirty = true;
    }

    private static MeshRenderer CreateLayer(Transform parent, string name, int sortingOrder, out Mesh mesh)
    {
        GameObject layer = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        layer.transform.SetParent(parent, false);
        mesh = new Mesh
        {
            name = $"StrategicKingdom_{name}",
            hideFlags = HideFlags.DontSave,
            indexFormat = IndexFormat.UInt32
        };
        mesh.MarkDynamic();
        layer.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
        renderer.sortingLayerName = RenderSortingLayerNames.MapOverlay_6;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void StartBuild()
    {
        KingdomMapSnapshot snapshot = CaptureSnapshot(++generation);
        if (snapshot == null) return;
        topologyDirty = false;
        buildTask = Task.Run(() => KingdomMapGeometryBuilder.Build(snapshot));
    }

    private void CompleteBuild()
    {
        if (buildTask == null || !buildTask.IsCompleted) return;
        KingdomMapGeometry result = buildTask.GetAwaiter().GetResult();
        buildTask = null;
        if (topologyDirty || result.Generation != generation || result.WorldId != MapBox.current_world_seed_id) return;
        geometry = result;
        ApplyGeometry();
        colorsDirty = true;
        hoveredKingdomId = long.MinValue;
        selectedKingdomId = long.MinValue;
    }

    private KingdomMapSnapshot CaptureSnapshot(int generation)
    {
        ZoneCalculator calculator = World.world?.zone_calculator;
        if (calculator?.map == null || calculator.zones_total_x <= 0 || calculator.zones_total_y <= 0) return null;

        int width = calculator.zones_total_x;
        int height = calculator.zones_total_y;
        var owners = new long[width * height];
        var colors = new Dictionary<long, Color32>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Kingdom kingdom = ResolveKingdom(calculator.map[x, y]);
                if (kingdom == null) continue;
                long kingdomId = kingdom.getID();
                owners[y * width + x] = kingdomId;
                if (!colors.ContainsKey(kingdomId)) colors.Add(kingdomId, ResolvePastelColor(kingdom));
            }
        }

        return new KingdomMapSnapshot(
            generation,
            MapBox.current_world_seed_id,
            width,
            height,
            MapBox.width,
            MapBox.height,
            owners,
            colors);
    }

    private static Kingdom ResolveKingdom(TileZone zone)
    {
        Kingdom kingdom = zone?.city?.kingdom;
        if (kingdom == null || kingdom.isRekt() || kingdom.isNeutral()) return null;
        return kingdom;
    }

    private static Color32 ResolvePastelColor(Kingdom kingdom)
    {
        Color32 source = kingdom.getColor().getColorMain32();
        return new Color32(
            BlendByte(source.r, 224, 0.2f),
            BlendByte(source.g, 220, 0.2f),
            BlendByte(source.b, 205, 0.2f),
            255);
    }

    private static byte BlendByte(byte source, byte target, float amount)
    {
        return (byte)Mathf.RoundToInt(source + (target - source) * amount);
    }

    private void ApplyGeometry()
    {
        fillMesh.Clear();
        fillMesh.vertices = geometry.FillVertices;
        fillMesh.colors32 = geometry.FillColors;
        fillMesh.uv2 = geometry.FillHighlights;
        fillMesh.triangles = geometry.FillTriangles;
        fillMesh.RecalculateBounds();

        borderMesh.Clear();
        borderMesh.vertices = geometry.BorderVertices;
        borderMesh.colors32 = geometry.BorderColors;
        borderMesh.uv = geometry.BorderLineData;
        borderMesh.uv2 = geometry.BorderMiterExpand;
        borderMesh.uv3 = geometry.BorderHighlights;
        borderMesh.triangles = geometry.BorderTriangles;
        borderMesh.RecalculateBounds();
    }

    private void RefreshColors()
    {
        Dictionary<long, Color32> colors = CaptureCurrentColors();
        for (int i = 0; i < geometry.FillColors.Length; i++)
        {
            if (colors.TryGetValue(geometry.FillOwners[i], out Color32 color)) geometry.FillColors[i] = color;
        }
        for (int i = 0; i < geometry.BorderColors.Length; i++)
        {
            if (colors.TryGetValue(geometry.BorderOwners[i], out Color32 color)) geometry.BorderColors[i] = color;
        }
        fillMesh.colors32 = geometry.FillColors;
        borderMesh.colors32 = geometry.BorderColors;
    }

    private static Dictionary<long, Color32> CaptureCurrentColors()
    {
        var colors = new Dictionary<long, Color32>();
        foreach (Kingdom kingdom in World.world.kingdoms)
        {
            if (kingdom == null || kingdom.isRekt() || kingdom.isNeutral()) continue;
            colors[kingdom.getID()] = ResolvePastelColor(kingdom);
        }
        return colors;
    }

    private void UpdateInteraction()
    {
        long hovered = ResolveKingdom(World.world.getMouseTilePosCachedFrame()?.zone)?.getID() ?? 0;
        Kingdom selected = SelectedMetas.selected_kingdom;
        long selectedId = selected == null || selected.isRekt() || selected.isNeutral() ? 0 : selected.getID();
        if (hovered == hoveredKingdomId && selectedId == selectedKingdomId) return;
        hoveredKingdomId = hovered;
        selectedKingdomId = selectedId;

        for (int i = 0; i < geometry.FillHighlights.Length; i++)
        {
            geometry.FillHighlights[i] = new Vector2(ResolveHighlight(geometry.FillOwners[i]), 0f);
        }
        for (int i = 0; i < geometry.BorderHighlights.Length; i++)
        {
            geometry.BorderHighlights[i] = new Vector2(ResolveHighlight(geometry.BorderOwners[i]), 0f);
        }
        fillMesh.uv2 = geometry.FillHighlights;
        borderMesh.uv3 = geometry.BorderHighlights;
    }

    private float ResolveHighlight(long owner)
    {
        if (owner != 0 && owner == selectedKingdomId) return 1f;
        if (owner != 0 && owner == hoveredKingdomId) return 0.72f;
        return 0f;
    }

    private void UpdateMaterialProperties()
    {
        float night = World.world.zone_calculator._night_multiplier;
        float cameraScale = Mathf.Clamp(World.world.camera.orthographicSize / 20f, 1f, 30f);
        float mapOpacity = Mathf.Clamp(cameraScale * 0.3f, 0f, VanillaMaximumMapOpacity);
        float zoomFade = mapOpacity / VanillaMaximumMapOpacity;
        fillProperties.Clear();
        fillProperties.SetFloat("_Opacity", FillOpacity * zoomFade * night);
        fillProperties.SetFloat("_PulseStrength", 0.28f);
        fillRenderer.SetPropertyBlock(fillProperties);

        float lineWidth = Mathf.Clamp(World.world.camera.orthographicSize * 0.018f, 0.22f, 0.9f);
        borderProperties.Clear();
        borderProperties.SetFloat("_LineWidth", lineWidth);
        borderProperties.SetFloat("_Opacity", BorderOpacity * zoomFade * night);
        borderProperties.SetFloat("_PulseStrength", 0.38f);
        borderProperties.SetFloat("_FlowSpeed", 1.1f);
        borderRenderer.SetPropertyBlock(borderProperties);
    }
}
