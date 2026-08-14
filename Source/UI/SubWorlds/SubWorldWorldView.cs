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
    private const string TilemapLayerPrefabPath = "prefabs/TilemapLayer";
    private const string UnitRendererPrefabPath = "prefabs/PrefabUnitRenderer";
    private const string MapSpritePrefabPath = "civ/p_mapSprite";

    private readonly SubWorldRuntime runtime;
    private readonly GameObject root;
    private readonly Transform tilemapsRoot;
    private readonly Transform wallsRoot;
    private readonly Transform unitsRoot;
    private readonly GameObject mapSpritePrefab;
    private readonly Dictionary<int, TerrainLayer> terrainLayers = new();
    private readonly Dictionary<int, WallVisual> wallVisuals = new();
    private readonly Dictionary<TopTileType, Sprite[]> wallFrames = new();
    private readonly Stack<WallVisual> wallPool = new();
    private readonly TileTypeBase[] renderedTerrainTypes;
    private readonly int[] renderedTerrainLayers;
    private readonly int[] renderedBorderLayers;
    private readonly bool[] renderedWaterRunups;
    private readonly HashSet<int> pendingTerrainTiles = new();
    private readonly TileType borderWaterType;
    private readonly TileType borderWaterRunupType;
    private readonly TileType borderPitType;
    private readonly SpriteRenderer pawnRenderer;
    private readonly SpriteRenderer overviewRenderer;
    private readonly Texture2D overviewTexture;
    private readonly Sprite overviewSprite;
    private readonly byte[] overviewPixels;
    private readonly Sprite[] pawnFrames;
    private readonly float pawnAnimationSpeed;
    private readonly List<int> dirtyTileIndices = new();
    private bool overviewTextureDirty;
    private bool overviewMode;
    private bool visible = true;

    internal SubWorldWorldView(SubWorldRuntime runtime, SubWorldSpatialSlot slot)
    {
        this.runtime = runtime;
        Slot = slot;
        root = new GameObject($"SubWorld.{runtime.InstanceId}");
        root.transform.SetParent(MapBox.instance.transform, false);
        root.transform.position = new Vector3(slot.WorldOrigin.x, slot.WorldOrigin.y, 0f);

        tilemapsRoot = CreateContainer("Tilemaps", root.transform, typeof(Grid));
        tilemapsRoot.localScale = new Vector3(0.25f, 0.25f, 1f);
        Grid tilemapGrid = tilemapsRoot.GetComponent<Grid>();
        tilemapGrid.cellSize = new Vector3(4f, 4f, 1f);
        tilemapGrid.cellGap = new Vector3(-0.0001f, -0.0001f, 0f);
        tilemapGrid.cellLayout = GridLayout.CellLayout.Rectangle;
        tilemapGrid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        Transform objectsRoot = CreateContainer("Objects", root.transform);
        wallsRoot = CreateContainer("Walls", objectsRoot);
        Transform creaturesRoot = CreateContainer("Creatures", root.transform);
        unitsRoot = CreateContainer("Units", creaturesRoot);

        mapSpritePrefab = Resources.Load<GameObject>(MapSpritePrefabPath) ??
                          throw new InvalidOperationException(
                              $"原版 MapSprite prefab 未找到: {MapSpritePrefabPath}");

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
        overviewPixels = new byte[runtime.Grid.TileCount];
        overviewRenderer = CreateOverviewRenderer(out overviewTexture, out overviewSprite);
        overviewMode = MapBox.isRenderMiniMap();

        ActorAsset pawnAsset = AssetManager.actor_library.get(runtime.VisualProfile.pawn_actor_asset_id) ??
                               throw new InvalidOperationException(
                                   $"SubWorld Pawn Actor Asset 未注册: {runtime.VisualProfile.pawn_actor_asset_id}");
        AnimationContainerUnit animation = DynamicActorSpriteCreatorUI.getContainerForUI(
            pawnAsset, true, pawnAsset.texture_asset);
        pawnFrames = animation.idle.frames;
        pawnAnimationSpeed = pawnAsset.animation_idle_speed;

        GameObject pawnPrefab = Resources.Load<GameObject>(UnitRendererPrefabPath) ??
                                throw new InvalidOperationException(
                                    $"原版 UnitRenderer prefab 未找到: {UnitRendererPrefabPath}");
        GameObject pawn = Object.Instantiate(pawnPrefab, unitsRoot);
        pawn.name = "Pawn";
        float pawnScale = pawnAsset.base_stats["scale"];
        pawn.transform.localScale = new Vector3(pawnScale, pawnScale, 1f);
        pawnRenderer = pawn.GetComponent<SpriteRenderer>();
        SetVisible(false);
    }

    internal long InstanceId => runtime.InstanceId;
    internal SubWorldSpatialSlot Slot { get; }
    internal Rect WorldBounds => Slot.WorldBounds;

    internal void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        UpdateRendererVisibility();
    }

    internal void SyncVisibleState()
    {
        SetOverviewMode(MapBox.isRenderMiniMap());
        runtime.Grid.ConsumeDirtyTiles(dirtyTileIndices);
        for (int i = 0; i < dirtyTileIndices.Count; i++)
        {
            int index = dirtyTileIndices[i];
            overviewPixels[index] = SubWorldVisualResources.GetTerrainIndex(runtime.Grid.GetTerrainType(index));
            overviewTextureDirty = true;
            pendingTerrainTiles.Add(index);
        }

        if (overviewMode)
        {
            UploadOverviewTexture();
            return;
        }

        if (pendingTerrainTiles.Count > 0)
        {
            PrepareTerrainDraw();
            foreach (int index in pendingTerrainTiles) RenderTile(index);
            FlushTerrainDraw();
            pendingTerrainTiles.Clear();
        }

        UpdateWalls();
        Position position = runtime.PawnEntity.GetComponent<Position>();
        pawnRenderer.transform.localPosition = position.value;
        int frame = Mathf.FloorToInt((float)runtime.Clock.LocalTime * pawnAnimationSpeed) % pawnFrames.Length;
        pawnRenderer.sprite = pawnFrames[frame];
    }

    internal void Destroy()
    {
        Object.Destroy(overviewSprite);
        Object.Destroy(overviewTexture);
        Object.Destroy(root);
    }

    private SpriteRenderer CreateOverviewRenderer(out Texture2D texture, out Sprite sprite)
    {
        texture = new Texture2D(runtime.Grid.Width, runtime.Grid.Height, TextureFormat.R8, false, true)
        {
            name = $"SubWorld.{runtime.InstanceId}.TerrainIndices",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        sprite = Sprite.Create(texture,
            new Rect(0f, 0f, runtime.Grid.Width, runtime.Grid.Height),
            Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
        sprite.name = $"SubWorld.{runtime.InstanceId}.Overview";
        sprite.hideFlags = HideFlags.DontSave;

        GameObject overview = new("world_layer", typeof(SpriteRenderer));
        overview.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = overview.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = SubWorldVisualResources.OverviewMaterial;
        SpriteRenderer worldLayerRenderer = MapBox.instance.GetComponent<SpriteRenderer>();
        renderer.sortingLayerID = worldLayerRenderer.sortingLayerID;
        renderer.sortingOrder = worldLayerRenderer.sortingOrder;
        renderer.spriteSortPoint = worldLayerRenderer.spriteSortPoint;
        renderer.renderingLayerMask = worldLayerRenderer.renderingLayerMask;
        return renderer;
    }

    private void SetOverviewMode(bool value)
    {
        if (overviewMode == value) return;
        overviewMode = value;
        UpdateRendererVisibility();
    }

    private void UpdateRendererVisibility()
    {
        bool showTerrainTiles = visible && !overviewMode;
        tilemapsRoot.gameObject.SetActive(showTerrainTiles);
        wallsRoot.gameObject.SetActive(showTerrainTiles);
        unitsRoot.gameObject.SetActive(showTerrainTiles);
        overviewRenderer.enabled = visible && overviewMode;
    }

    private void UploadOverviewTexture()
    {
        if (!overviewTextureDirty) return;
        overviewTexture.LoadRawTextureData(overviewPixels);
        overviewTexture.Apply(false, false);
        overviewTextureDirty = false;
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

        TilemapExtended layerObject = Object.Instantiate(layerPrefab, tilemapsRoot);
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
        RenderWall(index);
    }

    private void RenderWall(int index)
    {
        TopTileType wallType = runtime.Grid.GetTopType(index);
        if (wallType == null || !wallType.wall)
        {
            if (!wallVisuals.TryGetValue(index, out WallVisual removedWall)) return;
            wallVisuals.Remove(index);
            removedWall.GameObject.SetActive(false);
            wallPool.Push(removedWall);
            return;
        }

        if (!wallVisuals.TryGetValue(index, out WallVisual wall))
        {
            wall = GetWallVisual();
            wallVisuals.Add(index, wall);
        }

        Sprite[] frames = GetWallFrames(wallType);
        int frameOffset = GetWallFrameOffset(index, frames.Length);
        wall.Type = wallType;
        wall.Frames = frames;
        wall.FrameOffset = frameOffset;
        wall.GameObject.name = $"Wall.{index}";
        wall.GameObject.SetActive(true);
        wall.Renderer.sprite = frames[frameOffset];
        wall.Renderer.sharedMaterial = wallType == TopTileLibrary.wall_light
            ? LibraryMaterials.instance.mat_world_object_lit
            : LibraryMaterials.instance.mat_world_object;

        float x = runtime.Grid.GetX(index) + 0.5f;
        float y = runtime.Grid.GetY(index) + 0.5f;
        wall.Transform.localPosition = new Vector3(x, y, Mathf.Repeat(x * 0.0001f, 0.1f));
    }

    private WallVisual GetWallVisual()
    {
        if (wallPool.Count > 0) return wallPool.Pop();

        GameObject wallObject = Object.Instantiate(mapSpritePrefab, wallsRoot);
        SpriteRenderer renderer = wallObject.GetComponent<SpriteRenderer>();
        renderer.sortingLayerID = SortingLayer.NameToID("Objects");
        return new WallVisual(wallObject, renderer);
    }

    private Sprite[] GetWallFrames(TopTileType wallType)
    {
        if (wallFrames.TryGetValue(wallType, out Sprite[] frames)) return frames;
        frames = SpriteTextureLoader.getSpriteList($"walls/{wallType.id}/wall_sheet");
        wallFrames.Add(wallType, frames);
        return frames;
    }

    private int GetWallFrameOffset(int index, int frameCount)
    {
        uint seed = unchecked((uint)runtime.Seed * 397u ^ (uint)index * 2654435761u);
        return (int)(seed % (uint)frameCount);
    }

    private void UpdateWalls()
    {
        float scale = World.world.quality_changer.getTweenBuildingsValue() * 0.25f;
        foreach (WallVisual wall in wallVisuals.Values)
        {
            wall.Transform.localScale = new Vector3(scale, scale, 1f);
            if (!wall.Type.animated_wall) continue;
            int frame = ((int)AnimationHelper.getAnimationGlobalTime(4f) + wall.FrameOffset) % wall.Frames.Length;
            wall.Renderer.sprite = wall.Frames[frame];
        }
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

    private static Transform CreateContainer(string name, Transform parent, params Type[] components)
    {
        GameObject container = new(name, components);
        container.transform.SetParent(parent, false);
        return container.transform;
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

    private sealed class WallVisual
    {
        internal WallVisual(GameObject gameObject, SpriteRenderer renderer)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = renderer;
        }

        internal GameObject GameObject { get; }
        internal Transform Transform { get; }
        internal SpriteRenderer Renderer { get; }
        internal TopTileType Type { get; set; }
        internal Sprite[] Frames { get; set; }
        internal int FrameOffset { get; set; }
    }
}
