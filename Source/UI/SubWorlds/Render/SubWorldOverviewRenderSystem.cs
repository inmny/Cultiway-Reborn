using Cultiway.Abstract;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.UI.SubWorlds.Render;

/// <summary>维护小世界远景使用的 R8 地形索引纹理和单一 SpriteRenderer。</summary>
internal sealed class SubWorldOverviewRenderSystem : BaseSystem, IWorldStateClearable
{
    private readonly SubWorldGrid grid;
    private readonly SubWorldRenderState state;
    private readonly SpriteRenderer renderer;
    private readonly Texture2D texture;
    private readonly Sprite sprite;
    private readonly byte[] pixels;
    private bool textureDirty;

    internal SubWorldOverviewRenderSystem(long instanceId, SubWorldGrid grid, SubWorldRenderState state,
        Transform parent)
    {
        this.grid = grid;
        this.state = state;
        pixels = new byte[grid.TileCount];
        texture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false, true)
        {
            name = $"SubWorld.{instanceId}.TerrainIndices",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        sprite = Sprite.Create(texture, new Rect(0f, 0f, grid.Width, grid.Height), Vector2.zero, 1f, 0,
            SpriteMeshType.FullRect);
        sprite.name = $"SubWorld.{instanceId}.Overview";
        sprite.hideFlags = HideFlags.DontSave;

        GameObject overview = new("world_layer", typeof(SpriteRenderer));
        overview.transform.SetParent(parent, false);
        renderer = overview.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = SubWorldVisualResources.OverviewMaterial;
        SpriteRenderer worldLayerRenderer = MapBox.instance.GetComponent<SpriteRenderer>();
        renderer.sortingLayerID = worldLayerRenderer.sortingLayerID;
        renderer.sortingOrder = worldLayerRenderer.sortingOrder;
        renderer.spriteSortPoint = worldLayerRenderer.spriteSortPoint;
        renderer.renderingLayerMask = worldLayerRenderer.renderingLayerMask;
        renderer.enabled = false;
    }

    void IWorldStateClearable.ClearWorldState()
    {
        Object.Destroy(sprite);
        Object.Destroy(texture);
    }

    protected override void OnUpdateGroup()
    {
        for (int i = 0; i < state.DirtyTiles.Count; i++)
        {
            int index = state.DirtyTiles[i];
            pixels[index] = SubWorldVisualResources.GetTerrainIndex(grid.GetTerrainType(index));
            textureDirty = true;
        }

        renderer.enabled = state.OverviewVisible;
        if (!state.OverviewVisible || !textureDirty) return;
        texture.LoadRawTextureData(pixels);
        texture.Apply(false, false);
        textureDirty = false;
    }
}
