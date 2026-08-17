using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>按原版 Quantum wall 绘制方式提交小世界墙体 Sprite。</summary>
internal sealed class SubWorldWallRenderSystem : BaseSystem, IWorldStateClearable
{
    private const string MapSpritePrefabPath = "civ/p_mapSprite";

    private readonly SubWorldGrid grid;
    private readonly SubWorldRenderState state;
    private readonly Transform wallsRoot;
    private readonly MonoObjPool<QuantumSprite> wallPool;
    private readonly HashSet<int> wallIndices = new();
    private readonly Dictionary<TopTileType, Sprite[]> wallFrames = new();
    private readonly int seed;

    internal SubWorldWallRenderSystem(SubWorldGrid grid, int seed, SubWorldRenderState state, Transform parent)
    {
        this.grid = grid;
        this.seed = seed;
        this.state = state;
        GameObject objects = new("Objects");
        objects.transform.SetParent(parent, false);
        GameObject walls = new("Walls");
        walls.transform.SetParent(objects.transform, false);
        wallsRoot = walls.transform;

        QuantumSprite prefab = Resources.Load<QuantumSprite>(MapSpritePrefabPath) ??
                               throw new InvalidOperationException(
                                   $"原版 MapSprite prefab 未找到: {MapSpritePrefabPath}");
        wallPool = new MonoObjPool<QuantumSprite>(prefab, wallsRoot,
            create_action: wall =>
            {
                wall.sprite_renderer.sortingLayerID = SortingLayer.NameToID("Objects");
                wall.setSharedMat(LibraryMaterials.instance.mat_world_object);
            });
        walls.SetActive(false);
    }

    void IWorldStateClearable.ClearWorldState()
    {
        wallPool.Clear();
        wallIndices.Clear();
        wallFrames.Clear();
    }

    protected override void OnUpdateGroup()
    {
        for (int i = 0; i < state.DirtyTiles.Count; i++)
        {
            int index = state.DirtyTiles[i];
            TopTileType topType = grid.GetTopType(index);
            if (topType != null && topType.wall) wallIndices.Add(index);
            else wallIndices.Remove(index);
        }

        if (wallsRoot.gameObject.activeSelf != state.GameplayVisible)
            wallsRoot.gameObject.SetActive(state.GameplayVisible);
        wallPool.ResetToStart();
        if (!state.GameplayVisible)
        {
            wallPool.ClearUnsed();
            return;
        }

        float scale = World.world.quality_changer.getTweenBuildingsValue() * 0.25f;
        foreach (int index in wallIndices)
        {
            TopTileType wallType = grid.GetTopType(index);
            Sprite[] frames = GetWallFrames(wallType);
            int frameOffset = GetWallFrameOffset(index, frames.Length);
            int frame = wallType.animated_wall
                ? ((int)AnimationHelper.getAnimationGlobalTime(4f) + frameOffset) % frames.Length
                : frameOffset;
            QuantumSprite wall = wallPool.GetNext();
            wall.setSprite(frames[frame]);
            float x = grid.GetX(index) + 0.5f;
            Vector3 position = new(x, grid.GetY(index) + 0.5f, Mathf.Repeat(x * 0.0001f, 0.1f));
            wall.setPosOnly(ref position);
            wall.setScale(scale, scale);
            wall.setSharedMat(wallType == TopTileLibrary.wall_light
                ? LibraryMaterials.instance.mat_world_object_lit
                : LibraryMaterials.instance.mat_world_object);
        }
        wallPool.ClearUnsed();
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
        uint frameSeed = unchecked((uint)seed * 397u ^ (uint)index * 2654435761u);
        return (int)(frameSeed % (uint)frameCount);
    }
}
