using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Objects;
using Cultiway.Core.SubWorlds.Runtime;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>把小世界建筑的原版缩略图合并为一张远景透明纹理。</summary>
internal sealed class SubWorldBuildingOverviewRenderSystem :
    QuerySystem<Position, SubWorldBuilding, SubWorldVisual>, IWorldStateClearable
{
    private static readonly Color32 NatureFallbackColor = new(57, 115, 54, 255);
    private static readonly Color32 BuildingFallbackColor = new(214, 197, 154, 255);

    private readonly SubWorldGrid grid;
    private readonly SubWorldRenderState state;
    private readonly SpriteRenderer renderer;
    private readonly Texture2D texture;
    private readonly Sprite sprite;
    private readonly Color32[] pixels;
    private readonly List<OverviewBuildingSnapshot> currentSnapshots = new();
    private readonly List<OverviewBuildingSnapshot> renderedSnapshots = new();
    private readonly Dictionary<string, BuildingOverviewDefinition> definitions =
        new(StringComparer.Ordinal);

    internal SubWorldBuildingOverviewRenderSystem(
        long instanceId,
        SubWorldGrid grid,
        SubWorldRenderState state,
        Transform parent)
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        pixels = new Color32[grid.TileCount];
        texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false, true)
        {
            name = $"SubWorld.{instanceId}.BuildingOverview",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        sprite = Sprite.Create(texture, new Rect(0f, 0f, grid.Width, grid.Height), Vector2.zero, 1f, 0,
            SpriteMeshType.FullRect);
        sprite.name = $"SubWorld.{instanceId}.BuildingOverview";
        sprite.hideFlags = HideFlags.DontSave;

        GameObject overview = new("building_layer", typeof(SpriteRenderer));
        overview.transform.SetParent(parent, false);
        renderer = overview.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = WorldVisualResources.TransparentSpriteMaterial;
        renderer.sortingLayerName = S_SortingLayer.Objects;
        renderer.sortingOrder = 0;
        renderer.enabled = false;
    }

    void IWorldStateClearable.ClearWorldState()
    {
        renderer.enabled = false;
        currentSnapshots.Clear();
        renderedSnapshots.Clear();
        definitions.Clear();
        Object.Destroy(sprite);
        Object.Destroy(texture);
    }

    protected override void OnUpdate()
    {
        renderer.enabled = state.OverviewVisible;
        if (!state.OverviewVisible) return;

        CollectSnapshots();
        if (SnapshotsMatch()) return;

        Array.Clear(pixels, 0, pixels.Length);
        for (int i = 0; i < currentSnapshots.Count; i++) DrawBuilding(currentSnapshots[i]);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        renderedSnapshots.Clear();
        renderedSnapshots.AddRange(currentSnapshots);
    }

    private void CollectSnapshots()
    {
        currentSnapshots.Clear();
        Query.ForEachEntity((ref Position position, ref SubWorldBuilding building, ref SubWorldVisual visual,
            Entity _) =>
        {
            currentSnapshots.Add(new OverviewBuildingSnapshot(
                building.LocalObjectId,
                building.BuildingAssetId,
                Mathf.FloorToInt(position.value.x),
                Mathf.FloorToInt(position.value.y),
                visual.VariantIndex,
                visual.State));
        });
    }

    private bool SnapshotsMatch()
    {
        if (currentSnapshots.Count != renderedSnapshots.Count) return false;
        for (int i = 0; i < currentSnapshots.Count; i++)
        {
            if (!currentSnapshots[i].Equals(renderedSnapshots[i])) return false;
        }
        return true;
    }

    private void DrawBuilding(OverviewBuildingSnapshot snapshot)
    {
        BuildingOverviewDefinition definition = GetDefinition(snapshot.BuildingAssetId);
        SubWorldBuildingBounds bounds = SubWorldBuildingGeometry.GetBounds(
            snapshot.AnchorX, snapshot.AnchorY, definition.Asset.fundament);
        BuildingOverviewIcon icon = definition.GetIcon(snapshot.VariantIndex);
        if (icon == null)
        {
            DrawFallback(snapshot, definition.Asset);
            return;
        }

        for (int targetY = 0; targetY < bounds.Height; targetY++)
        for (int targetX = 0; targetX < bounds.Width; targetX++)
        {
            int mapX = bounds.MinX + targetX;
            int mapY = bounds.MinY + targetY;
            if (!grid.Contains(mapX, mapY)) continue;

            int sourceX = targetX * icon.Width / bounds.Width;
            int sourceY = targetY * icon.Height / bounds.Height;
            Color32 color = icon.Pixels[sourceY * icon.Width + sourceX];
            if (color.a == 0) continue;
            pixels[grid.GetIndex(mapX, mapY)] = ApplyVisualState(color, snapshot.VisualState);
        }
    }

    private void DrawFallback(OverviewBuildingSnapshot snapshot, BuildingAsset asset)
    {
        if (!grid.Contains(snapshot.AnchorX, snapshot.AnchorY)) return;
        Color32 color = asset.is_vegetation ? NatureFallbackColor : BuildingFallbackColor;
        pixels[grid.GetIndex(snapshot.AnchorX, snapshot.AnchorY)] =
            ApplyVisualState(color, snapshot.VisualState);
    }

    private BuildingOverviewDefinition GetDefinition(string buildingAssetId)
    {
        if (definitions.TryGetValue(buildingAssetId, out BuildingOverviewDefinition definition))
            return definition;

        BuildingAsset asset = AssetManager.buildings.get(buildingAssetId) ??
                              throw new InvalidOperationException(
                                  $"SubWorld Building Asset 未注册: {buildingAssetId}");
        if (asset.fundament == null)
            throw new InvalidOperationException($"SubWorld Building 缺少占地定义: {buildingAssetId}");

        definition = new BuildingOverviewDefinition(asset);
        Sprite[] sprites = asset.loadBuildingSpriteList();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (!TryGetMiniVariant(candidate.name, out int variantIndex)) continue;
            definition.AddIcon(variantIndex, CreateIcon(candidate));
        }

        definitions.Add(buildingAssetId, definition);
        return definition;
    }

    private static bool TryGetMiniVariant(string spriteName, out int variantIndex)
    {
        variantIndex = 0;
        if (string.IsNullOrEmpty(spriteName) ||
            !spriteName.StartsWith("mini_", StringComparison.Ordinal))
        {
            return false;
        }

        int valueStart = "mini_".Length;
        int valueEnd = spriteName.IndexOf('_', valueStart);
        string value = valueEnd < 0
            ? spriteName.Substring(valueStart)
            : spriteName.Substring(valueStart, valueEnd - valueStart);
        return int.TryParse(value, out variantIndex) && variantIndex >= 0;
    }

    private static BuildingOverviewIcon CreateIcon(Sprite source)
    {
        Rect rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        int startX = Mathf.RoundToInt(rect.x);
        int startY = Mathf.RoundToInt(rect.y);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"Building mini Sprite 尺寸无效: {source.name}");

        Color32[] texturePixels = source.texture.GetPixels32();
        int textureWidth = source.texture.width;
        var iconPixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            iconPixels[y * width + x] = texturePixels[(startY + y) * textureWidth + startX + x];
        return new BuildingOverviewIcon(width, height, iconPixels);
    }

    private static Color32 ApplyVisualState(Color32 color, SubWorldVisualState visualState)
    {
        float multiplier = visualState switch
        {
            SubWorldVisualState.Ruin => 0.6f,
            SubWorldVisualState.Disabled => 0.78f,
            _ => 1f
        };
        if (Mathf.Approximately(multiplier, 1f)) return color;
        return new Color32(
            (byte)Mathf.RoundToInt(color.r * multiplier),
            (byte)Mathf.RoundToInt(color.g * multiplier),
            (byte)Mathf.RoundToInt(color.b * multiplier),
            color.a);
    }

    private readonly struct OverviewBuildingSnapshot : IEquatable<OverviewBuildingSnapshot>
    {
        internal OverviewBuildingSnapshot(
            LocalObjectId localObjectId,
            string buildingAssetId,
            int anchorX,
            int anchorY,
            int variantIndex,
            SubWorldVisualState visualState)
        {
            LocalObjectId = localObjectId;
            BuildingAssetId = buildingAssetId;
            AnchorX = anchorX;
            AnchorY = anchorY;
            VariantIndex = variantIndex;
            VisualState = visualState;
        }

        internal LocalObjectId LocalObjectId { get; }
        internal string BuildingAssetId { get; }
        internal int AnchorX { get; }
        internal int AnchorY { get; }
        internal int VariantIndex { get; }
        internal SubWorldVisualState VisualState { get; }

        public bool Equals(OverviewBuildingSnapshot other)
        {
            return LocalObjectId == other.LocalObjectId &&
                   string.Equals(BuildingAssetId, other.BuildingAssetId, StringComparison.Ordinal) &&
                   AnchorX == other.AnchorX &&
                   AnchorY == other.AnchorY &&
                   VariantIndex == other.VariantIndex &&
                   VisualState == other.VisualState;
        }
    }

    private sealed class BuildingOverviewDefinition
    {
        private readonly Dictionary<int, BuildingOverviewIcon> icons = new();
        private BuildingOverviewIcon firstIcon;

        internal BuildingOverviewDefinition(BuildingAsset asset)
        {
            Asset = asset;
        }

        internal BuildingAsset Asset { get; }

        internal void AddIcon(int variantIndex, BuildingOverviewIcon icon)
        {
            if (!icons.TryAdd(variantIndex, icon)) return;
            firstIcon ??= icon;
        }

        internal BuildingOverviewIcon GetIcon(int variantIndex)
        {
            if (icons.TryGetValue(variantIndex, out BuildingOverviewIcon icon)) return icon;
            if (icons.TryGetValue(0, out icon)) return icon;
            return firstIcon;
        }
    }

    private sealed class BuildingOverviewIcon
    {
        internal BuildingOverviewIcon(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal Color32[] Pixels { get; }
    }
}
