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
    private const int PawnSortingOrder = 200;

    private readonly SubWorldRuntime runtime;
    private readonly GameObject root;
    private readonly Tilemap mainTilemap;
    private readonly Tilemap topTilemap;
    private readonly Tilemap statusTilemap;
    private readonly TilemapRenderer mainRenderer;
    private readonly TilemapRenderer topRenderer;
    private readonly TilemapRenderer statusRenderer;
    private readonly Material mainMaterial;
    private readonly Material topMaterial;
    private readonly Material statusMaterial;
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

        TileTypeBase mainType = runtime.Grid.GetMainType(0);
        TileTypeBase topType = FindTopType(runtime.Grid) ?? mainType;
        mainTilemap = CreateTilemap("Main", mainType, mainType.render_z,
            out mainRenderer, out mainMaterial);
        topTilemap = CreateTilemap("Top", topType, topType.render_z,
            out topRenderer, out topMaterial);
        statusTilemap = CreateTilemap("Status", mainType, PawnSortingOrder - 1,
            out statusRenderer, out statusMaterial);

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
    internal Tilemap MainTilemap => mainTilemap;
    internal Tilemap TopTilemap => topTilemap;
    internal TilemapRenderer MainRenderer => mainRenderer;
    internal TilemapRenderer TopRenderer => topRenderer;
    internal Material MainMaterial => mainMaterial;
    internal Material TopMaterial => topMaterial;

    internal void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        mainRenderer.enabled = value;
        topRenderer.enabled = value;
        statusRenderer.enabled = value;
        pawnRenderer.enabled = value;
    }

    internal void SyncVisibleState()
    {
        runtime.Grid.ConsumeDirtyTiles(dirtyTileIndices);
        for (int i = 0; i < dirtyTileIndices.Count; i++) RenderTile(dirtyTileIndices[i]);

        Position position = runtime.PawnEntity.GetComponent<Position>();
        pawnRenderer.transform.localPosition = position.value;
        int frame = Mathf.FloorToInt((float)runtime.Clock.LocalTime * pawnAnimationSpeed) % pawnFrames.Length;
        pawnRenderer.sprite = pawnFrames[frame];
    }

    internal void Destroy()
    {
        Object.Destroy(mainMaterial);
        Object.Destroy(topMaterial);
        Object.Destroy(statusMaterial);
        Object.Destroy(pawnMaterial);
        Object.Destroy(root);
    }

    private Tilemap CreateTilemap(string name, TileTypeBase terrain, int sortingOrder,
        out TilemapRenderer renderer, out Material material)
    {
        GameObject layer = new(name, typeof(Tilemap), typeof(TilemapRenderer));
        layer.transform.SetParent(root.transform, false);
        Tilemap tilemap = layer.GetComponent<Tilemap>();
        renderer = layer.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        material = new Material(LibraryMaterials.instance.dict[terrain.material]);
        renderer.sharedMaterial = material;
        return tilemap;
    }

    private void RenderTile(int index)
    {
        var position = new Vector3Int(runtime.Grid.GetX(index), runtime.Grid.GetY(index), 0);
        mainTilemap.SetTile(position, runtime.Grid.GetMainType(index).sprites.main);
        TopTileType topType = runtime.Grid.GetTopType(index);
        topTilemap.SetTile(position, topType?.sprites.main);
    }

    private static TopTileType FindTopType(SubWorldGrid grid)
    {
        for (int index = 0; index < grid.TileCount; index++)
        {
            TopTileType topType = grid.GetTopType(index);
            if (topType != null) return topType;
        }
        return null;
    }
}
