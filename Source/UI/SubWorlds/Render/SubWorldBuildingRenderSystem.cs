using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Objects;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>查询 Building 类别与共享视觉状态，并复用原版 BuildingAsset Sprite 定义。</summary>
internal sealed class SubWorldBuildingRenderSystem : QuerySystem<Position, SubWorldBuilding, SubWorldVisual>,
    IWorldStateClearable
{
    private readonly SubWorldRenderState state;
    private readonly Transform buildingsRoot;
    private readonly MonoObjPool<SubWorldBuildingView> pool;
    private readonly Dictionary<string, BuildingAsset> assets = new(StringComparer.Ordinal);

    internal SubWorldBuildingRenderSystem(SubWorldRenderState state, Transform parent)
    {
        this.state = state;
        GameObject buildings = new("Buildings");
        buildings.transform.SetParent(parent, false);
        buildingsRoot = buildings.transform;

        GameObject prefabObject = new("SubWorldBuildingViewPrefab");
        prefabObject.transform.SetParent(buildingsRoot, false);
        SpriteRenderer renderer = prefabObject.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = S_SortingLayer.Objects;
        var prefab = prefabObject.AddComponent<SubWorldBuildingView>();
        prefab.Bind(renderer);
        prefabObject.SetActive(false);
        pool = new MonoObjPool<SubWorldBuildingView>(prefab, buildingsRoot);
        buildings.SetActive(false);
    }

    void IWorldStateClearable.ClearWorldState()
    {
        pool.Clear();
        assets.Clear();
    }

    protected override void OnUpdate()
    {
        if (buildingsRoot.gameObject.activeSelf != state.GameplayVisible)
            buildingsRoot.gameObject.SetActive(state.GameplayVisible);
        pool.ResetToStart();
        if (state.GameplayVisible)
        {
            Query.ForEachEntity((ref Position position, ref SubWorldBuilding building, ref SubWorldVisual visual,
                Entity _) =>
            {
                BuildingAsset asset = GetAsset(building.BuildingAssetId);
                BuildingAnimationData animation = GetAnimation(asset, visual.VariantIndex);
                Sprite[] frames = GetFrames(asset, animation, visual.State);
                int frameIndex = frames.Length == 1
                    ? 0
                    : Mathf.FloorToInt(Tick.time * asset.animation_speed) % frames.Length;

                SubWorldBuildingView view = pool.GetNext();
                view.Renderer.sprite = frames[frameIndex];
                view.Renderer.sortingOrder = -Mathf.RoundToInt(position.value.y * 10f);
                view.transform.localPosition = position.value;
                view.transform.localScale = new Vector3(asset.scale_base.x, asset.scale_base.y, 1f);
            });
        }
        pool.ClearUnsed();
    }

    private BuildingAsset GetAsset(string buildingAssetId)
    {
        if (assets.TryGetValue(buildingAssetId, out BuildingAsset asset)) return asset;
        asset = AssetManager.buildings.get(buildingAssetId);
        asset.checkSpritesAreLoaded();
        assets.Add(buildingAssetId, asset);
        return asset;
    }

    private static BuildingAnimationData GetAnimation(BuildingAsset asset, int variantIndex)
    {
        if ((uint)variantIndex >= (uint)asset.building_sprites.animation_data.Count ||
            asset.building_sprites.animation_data[variantIndex] == null)
            throw new InvalidOperationException(
                $"SubWorld Building 视觉变体不存在: asset={asset.id}, variant={variantIndex}");
        return asset.building_sprites.animation_data[variantIndex];
    }

    private static Sprite[] GetFrames(
        BuildingAsset asset,
        BuildingAnimationData animation,
        SubWorldVisualState visualState)
    {
        Sprite[] frames = visualState switch
        {
            SubWorldVisualState.Default => animation.main,
            SubWorldVisualState.Ruin => animation.ruins,
            SubWorldVisualState.Disabled => animation.main_disabled,
            SubWorldVisualState.Special => animation.special,
            _ => null
        };
        if (frames == null || frames.Length == 0)
            throw new InvalidOperationException(
                $"SubWorld Building 缺少视觉阶段 Sprite: asset={asset.id}, state={visualState}");
        return frames;
    }
}

internal sealed class SubWorldBuildingView : MonoBehaviour
{
    private SpriteRenderer renderer;

    internal SpriteRenderer Renderer => renderer ??= GetComponent<SpriteRenderer>();

    internal void Bind(SpriteRenderer value)
    {
        renderer = value;
    }
}
