using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>按原版 WorldTilemap 规则维护小世界近景 terrain 与边缘 Tilemap。</summary>
internal sealed class SubWorldTerrainRenderSystem : BaseSystem, IWorldStateClearable
{
    private const int EmptyRenderLayer = int.MinValue;
    private const string TilemapLayerPrefabPath = "prefabs/TilemapLayer";

    private readonly SubWorldGrid grid;
    private readonly SubWorldRenderState state;
    private readonly Transform tilemapsRoot;
    private readonly Dictionary<int, TerrainLayer> terrainLayers = new();
    private readonly TileTypeBase[] renderedTerrainTypes;
    private readonly int[] renderedTerrainLayers;
    private readonly int[] renderedBorderLayers;
    private readonly bool[] renderedWaterRunups;
    private readonly HashSet<int> pendingTerrainTiles = new();
    private readonly TileType borderWaterType;
    private readonly TileType borderWaterRunupType;
    private readonly TileType borderPitType;

    internal SubWorldTerrainRenderSystem(SubWorldGrid grid, SubWorldRenderState state, Transform parent)
    {
        this.grid = grid;
        this.state = state;

        GameObject tilemaps = new("Tilemaps", typeof(Grid));
        tilemaps.transform.SetParent(parent, false);
        tilemapsRoot = tilemaps.transform;
        tilemapsRoot.localScale = new Vector3(0.25f, 0.25f, 1f);
        Grid tilemapGrid = tilemaps.GetComponent<Grid>();
        tilemapGrid.cellSize = new Vector3(4f, 4f, 1f);
        tilemapGrid.cellGap = new Vector3(-0.0001f, -0.0001f, 0f);
        tilemapGrid.cellLayout = GridLayout.CellLayout.Rectangle;
        tilemapGrid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        TilemapExtended layerPrefab = Resources.Load<TilemapExtended>(TilemapLayerPrefabPath) ??
                                      throw new InvalidOperationException(
                                          $"原版 TilemapLayer prefab 未找到: {TilemapLayerPrefabPath}");
        CreateTerrainLayers(layerPrefab);
        borderWaterType = AssetManager.tiles.get("border_water");
        borderWaterRunupType = AssetManager.tiles.get("border_water_runup");
        borderPitType = AssetManager.tiles.get("border_pit");
        UseOriginalWaterRunupMaterial(parent);

        renderedTerrainTypes = new TileTypeBase[grid.TileCount];
        renderedTerrainLayers = CreateEmptyLayerArray(grid.TileCount);
        renderedBorderLayers = CreateEmptyLayerArray(grid.TileCount);
        renderedWaterRunups = new bool[grid.TileCount];
        tilemaps.gameObject.SetActive(false);
    }

    void IWorldStateClearable.ClearWorldState()
    {
        pendingTerrainTiles.Clear();
        foreach (TerrainLayer layer in terrainLayers.Values) layer.Clear();
    }

    protected override void OnUpdateGroup()
    {
        pendingTerrainTiles.UnionWith(state.DirtyTiles);
        if (tilemapsRoot.gameObject.activeSelf != state.GameplayVisible)
            tilemapsRoot.gameObject.SetActive(state.GameplayVisible);
        if (!state.GameplayVisible || pendingTerrainTiles.Count == 0) return;

        foreach (TerrainLayer layer in terrainLayers.Values) layer.PrepareDraw();
        foreach (int index in pendingTerrainTiles) RenderTile(index);
        foreach (TerrainLayer layer in terrainLayers.Values) layer.FlushDraw();
        pendingTerrainTiles.Clear();
    }

    private void CreateTerrainLayers(TilemapExtended layerPrefab)
    {
        for (int i = 0; i < AssetManager.tiles.list.Count; i++)
            CreateTerrainLayer(AssetManager.tiles.list[i], layerPrefab);
        for (int i = 0; i < AssetManager.top_tiles.list.Count; i++)
            CreateTerrainLayer(AssetManager.top_tiles.list[i], layerPrefab);
    }

    private void CreateTerrainLayer(TileTypeBase terrainType, TilemapExtended layerPrefab)
    {
        if (terrainLayers.ContainsKey(terrainType.render_z)) return;
        TilemapExtended layerObject = Object.Instantiate(layerPrefab, tilemapsRoot);
        Tilemap tilemap = layerObject.GetComponent<Tilemap>();
        tilemap.ClearAllTiles();
        layerObject.create(terrainType);
        terrainLayers.Add(terrainType.render_z,
            new TerrainLayer(tilemap, layerObject.GetComponent<TilemapRenderer>()));
    }

    private void UseOriginalWaterRunupMaterial(Transform viewRoot)
    {
        TerrainLayer waterRunupLayer = terrainLayers[borderWaterRunupType.render_z];
        TilemapRenderer[] originalRenderers = MapBox.instance.GetComponentsInChildren<TilemapRenderer>(true);
        for (int i = 0; i < originalRenderers.Length; i++)
        {
            TilemapRenderer renderer = originalRenderers[i];
            if (renderer.transform.IsChildOf(viewRoot) || renderer.sortingOrder != borderWaterRunupType.render_z)
                continue;
            waterRunupLayer.Renderer.sharedMaterial = renderer.sharedMaterial;
            return;
        }
    }

    private void RenderTile(int index)
    {
        int x = grid.GetX(index);
        int y = grid.GetY(index);
        Vector3Int position = new(x, y, 0);
        TileTypeBase terrainType = grid.GetTerrainType(index);
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
        int y = grid.GetY(index);
        if (terrainType.force_edge_variation && y + 1 < grid.Height &&
            grid.GetTerrainType(index + grid.Width) != terrainType)
            return terrainType.sprites.getVariation(terrainType.force_edge_variation_frame);
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

        TileType mainType = grid.GetMainType(index);
        if ((!mainType.ground && !mainType.block) || mainType.can_be_filled_with_ocean) return;
        TileType borderType = null;
        bool drawWaterRunup = false;
        int y = grid.GetY(index);
        if (y > 0 && grid.GetMainType(index - grid.Width).can_be_filled_with_ocean)
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
        int x = grid.GetX(index);
        int y = grid.GetY(index);
        if (x == 0 || y == 0 || x == grid.Width - 1 || y == grid.Height - 1) return true;
        return grid.GetTerrainType(index - grid.Width).liquid ||
               grid.GetTerrainType(index + grid.Width).liquid ||
               grid.GetTerrainType(index - 1).liquid ||
               grid.GetTerrainType(index + 1).liquid;
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
        internal void PrepareDraw() { positions.Clear(); tiles.Clear(); }
        internal void Queue(Vector3Int position, TileBase tile) { positions.Add(position); tiles.Add(tile); }
        internal void FlushDraw()
        {
            if (positions.Count > 0) tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
        }
        internal void Clear() { tilemap.ClearAllTiles(); }
    }
}
