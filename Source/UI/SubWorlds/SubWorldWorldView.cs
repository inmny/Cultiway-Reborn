using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Runtime;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds;

/// <summary>把单个小世界 Runtime 投影到其稳定槽位中的私有 Unity Renderer。</summary>
internal sealed class SubWorldWorldView
{
    private const int EmptyRenderLayer = int.MinValue;
    private const int PawnSortingOrder = 200;
    private const string TilemapLayerPrefabPath = "prefabs/TilemapLayer";

    private readonly SubWorldRuntime runtime;
    private readonly GameObject root;
    private readonly Dictionary<int, TerrainLayer> terrainLayers = new();
    private readonly TileTypeBase[] renderedTerrainTypes;
    private readonly int[] renderedTerrainLayers;
    private readonly int[] renderedBorderLayers;
    private readonly bool[] renderedWaterRunups;
    private readonly TileType borderWaterType;
    private readonly TileType borderWaterRunupType;
    private readonly TileType borderPitType;
    private readonly SpriteRenderer pawnRenderer;
    private readonly Material pawnMaterial;
    private readonly Sprite[] pawnFrames;
    private readonly float pawnAnimationSpeed;
    private readonly List<int> dirtyTileIndices = new();
    private bool visible = true;

    internal SubWorldWorldView(SubWorldRuntime runtime, SubWorldSpatialSlot slot)
    {
        this.runtime = runtime;
        Slot = slot;
        root = new GameObject($"SubWorld.{runtime.InstanceId}", typeof(Grid));
        root.transform.SetParent(MapBox.instance.transform, false);
        root.transform.position = new Vector3(slot.WorldOrigin.x, slot.WorldOrigin.y, 0f);

        TilemapExtended layerPrefab = Resources.Load<TilemapExtended>(TilemapLayerPrefabPath) ??
                                             throw new InvalidOperationException(
                                                 $"原版 TilemapLayer prefab 未找到: {TilemapLayerPrefabPath}");
        CreateTerrainLayers(layerPrefab);

        borderWaterType = AssetManager.tiles.get("border_water");
        borderWaterRunupType = AssetManager.tiles.get("border_water_runup");
        borderPitType = AssetManager.tiles.get("border_pit");
        UseOriginalWaterRunupMaterial();

        renderedTerrainTypes = new TileTypeBase[runtime.Grid.TileCount];
        renderedTerrainLayers = CreateEmptyLayerArray(runtime.Grid.TileCount);
        renderedBorderLayers = CreateEmptyLayerArray(runtime.Grid.TileCount);
        renderedWaterRunups = new bool[runtime.Grid.TileCount];

        ActorAsset pawnAsset = AssetManager.actor_library.get(runtime.VisualProfile.pawn_actor_asset_id) ??
                               throw new InvalidOperationException(
                                   $"SubWorld Pawn Actor Asset 未注册: {runtime.VisualProfile.pawn_actor_asset_id}");
        AnimationContainerUnit animation = DynamicActorSpriteCreatorUI.getContainerForUI(
            pawnAsset, true, pawnAsset.texture_asset);
        pawnFrames = animation.idle.frames;
        pawnAnimationSpeed = pawnAsset.animation_idle_speed;

        GameObject pawn = new("Pawn", typeof(SpriteRenderer));
        pawn.transform.SetParent(root.transform, false);
        pawnRenderer = pawn.GetComponent<SpriteRenderer>();
        pawnRenderer.sortingOrder = PawnSortingOrder;
        pawnMaterial = new Material(LibraryMaterials.instance.mat_world_object);
        pawnRenderer.sharedMaterial = pawnMaterial;
        SetVisible(false);
    }

    internal long InstanceId => runtime.InstanceId;
    internal SubWorldSpatialSlot Slot { get; }
    internal Rect WorldBounds => Slot.WorldBounds;

    internal void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        foreach (TerrainLayer layer in terrainLayers.Values) layer.Renderer.enabled = value;
        pawnRenderer.enabled = value;
    }

    internal void SyncVisibleState()
    {
        runtime.Grid.ConsumeDirtyTiles(dirtyTileIndices);
        if (dirtyTileIndices.Count > 0)
        {
            PrepareTerrainDraw();
            for (int i = 0; i < dirtyTileIndices.Count; i++) RenderTile(dirtyTileIndices[i]);
            FlushTerrainDraw();
        }

        Position position = runtime.PawnEntity.GetComponent<Position>();
        pawnRenderer.transform.localPosition = position.value;
        int frame = Mathf.FloorToInt((float)runtime.Clock.LocalTime * pawnAnimationSpeed) % pawnFrames.Length;
        pawnRenderer.sprite = pawnFrames[frame];
    }

    internal void Destroy()
    {
        Object.Destroy(pawnMaterial);
        Object.Destroy(root);
    }

    private void CreateTerrainLayers(TilemapExtended layerPrefab)
    {
        for (int i = 0; i < AssetManager.tiles.list.Count; i++)
        {
            CreateTerrainLayer(AssetManager.tiles.list[i], layerPrefab);
        }

        for (int i = 0; i < AssetManager.top_tiles.list.Count; i++)
        {
            CreateTerrainLayer(AssetManager.top_tiles.list[i], layerPrefab);
        }
    }

    private void CreateTerrainLayer(TileTypeBase terrainType, TilemapExtended layerPrefab)
    {
        if (terrainLayers.ContainsKey(terrainType.render_z)) return;

        TilemapExtended layerObject = Object.Instantiate(layerPrefab, root.transform);
        Tilemap tilemap = layerObject.GetComponent<Tilemap>();
        tilemap.ClearAllTiles();
        layerObject.create(terrainType);
        terrainLayers.Add(terrainType.render_z,
            new TerrainLayer(tilemap, layerObject.GetComponent<TilemapRenderer>()));
    }

    private void UseOriginalWaterRunupMaterial()
    {
        TerrainLayer waterRunupLayer = terrainLayers[borderWaterRunupType.render_z];
        TilemapRenderer[] originalRenderers = MapBox.instance.GetComponentsInChildren<TilemapRenderer>(true);
        for (int i = 0; i < originalRenderers.Length; i++)
        {
            TilemapRenderer renderer = originalRenderers[i];
            if (renderer.transform.IsChildOf(root.transform) ||
                renderer.sortingOrder != borderWaterRunupType.render_z) continue;
            waterRunupLayer.Renderer.sharedMaterial = renderer.sharedMaterial;
            return;
        }
    }

    private void PrepareTerrainDraw()
    {
        foreach (TerrainLayer layer in terrainLayers.Values) layer.PrepareDraw();
    }

    private void FlushTerrainDraw()
    {
        foreach (TerrainLayer layer in terrainLayers.Values) layer.FlushDraw();
    }

    private void RenderTile(int index)
    {
        int x = runtime.Grid.GetX(index);
        int y = runtime.Grid.GetY(index);
        Vector3Int position = new(x, y, 0);
        TileTypeBase terrainType = runtime.Grid.GetTerrainType(index);

        if (renderedTerrainTypes[index] != terrainType)
        {
            int previousLayer = renderedTerrainLayers[index];
            if (previousLayer != EmptyRenderLayer) terrainLayers[previousLayer].Queue(position, null);

            terrainLayers[terrainType.render_z].Queue(position, GetTerrainVariation(index, terrainType));
            renderedTerrainTypes[index] = terrainType;
            renderedTerrainLayers[index] = terrainType.render_z;
        }

        RenderBorder(index, position);
    }

    private TileBase GetTerrainVariation(int index, TileTypeBase terrainType)
    {
        int y = runtime.Grid.GetY(index);
        if (terrainType.force_edge_variation && y + 1 < runtime.Grid.Height &&
            runtime.Grid.GetTerrainType(index + runtime.Grid.Width) != terrainType)
        {
            return terrainType.sprites.getVariation(terrainType.force_edge_variation_frame);
        }
        return terrainType.sprites.getRandom();
    }

    private void RenderBorder(int index, Vector3Int position)
    {
        int previousBorderLayer = renderedBorderLayers[index];
        if (previousBorderLayer != EmptyRenderLayer)
        {
            terrainLayers[previousBorderLayer].Queue(position, null);
            renderedBorderLayers[index] = EmptyRenderLayer;
        }
        if (renderedWaterRunups[index])
        {
            terrainLayers[borderWaterRunupType.render_z].Queue(position, null);
            renderedWaterRunups[index] = false;
        }

        TileType mainType = runtime.Grid.GetMainType(index);
        if ((!mainType.ground && !mainType.block) || mainType.can_be_filled_with_ocean) return;

        TileType borderType = null;
        bool drawWaterRunup = false;
        int y = runtime.Grid.GetY(index);
        if (y > 0 && runtime.Grid.GetMainType(index - runtime.Grid.Width).can_be_filled_with_ocean)
        {
            borderType = borderPitType;
        }
        else if (IsWaterAround(index))
        {
            borderType = borderWaterType;
            drawWaterRunup = true;
        }

        if (borderType == null) return;
        terrainLayers[borderType.render_z].Queue(position, borderType.sprites.main);
        renderedBorderLayers[index] = borderType.render_z;
        if (!drawWaterRunup) return;
        terrainLayers[borderWaterRunupType.render_z].Queue(position, borderWaterRunupType.sprites.main);
        renderedWaterRunups[index] = true;
    }

    private bool IsWaterAround(int index)
    {
        int x = runtime.Grid.GetX(index);
        int y = runtime.Grid.GetY(index);
        if (x == 0 || y == 0 || x == runtime.Grid.Width - 1 || y == runtime.Grid.Height - 1) return true;
        return runtime.Grid.GetTerrainType(index - runtime.Grid.Width).liquid ||
               runtime.Grid.GetTerrainType(index + runtime.Grid.Width).liquid ||
               runtime.Grid.GetTerrainType(index - 1).liquid ||
               runtime.Grid.GetTerrainType(index + 1).liquid;
    }

    private static int[] CreateEmptyLayerArray(int count)
    {
        int[] layers = new int[count];
        for (int i = 0; i < layers.Length; i++) layers[i] = EmptyRenderLayer;
        return layers;
    }

    private sealed class TerrainLayer
    {
        private readonly Tilemap tilemap;
        private readonly List<Vector3Int> positions = new();
        private readonly List<TileBase> tiles = new();

        internal TerrainLayer(Tilemap tilemap, TilemapRenderer renderer)
        {
            this.tilemap = tilemap;
            Renderer = renderer;
        }

        internal TilemapRenderer Renderer { get; }

        internal void PrepareDraw()
        {
            positions.Clear();
            tiles.Clear();
        }

        internal void Queue(Vector3Int position, TileBase tile)
        {
            positions.Add(position);
            tiles.Add(tile);
        }

        internal void FlushDraw()
        {
            if (positions.Count > 0) tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
        }
    }
}
