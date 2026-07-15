using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Utils;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>
/// 为已显化的法器实体创建世界视图。法器的位置、朝向和碰撞信息均来自 ECS 本体。
/// </summary>
public class ArtifactWorldRenderSystem
    : QuerySystem<Artifact, ArtifactManifestation, ItemShape, Position, Rotation>
{
    private readonly MonoObjPool<ArtifactWorldView> _pool;

    public ArtifactWorldRenderSystem()
    {
        var root = new GameObject("artifact_world_views");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(ArtifactWorldRenderSystem) + "_Artifact")
            .AddComponent<ArtifactWorldView>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.Objects_4;

        _pool = new MonoObjPool<ArtifactWorldView>(
            prefab,
            root.transform,
            active_action: view => view.transform.localScale = Vector3.one);
    }

    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        if (!GeneralSettings.EnableArtifactSystems || !MapBox.isRenderGameplay() || MapBox.isRenderMiniMap())
        {
            _pool.ClearUnsed();
            return;
        }

        float time = Time.time;
        Query.ForEachEntity((
            ref Artifact _,
            ref ArtifactManifestation manifestation,
            ref ItemShape itemShape,
            ref Position position,
            ref Rotation rotation,
            Entity artifact) =>
        {
            if (!manifestation.visible) return;
            ArtifactShapeAsset shape = (ArtifactShapeAsset)itemShape.Type;
            Sprite sprite = shape.GetWorldSprite(artifact);
            if (sprite == null) return;

            float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (spriteSize <= 0f || manifestation.world_size <= 0f) return;

            ArtifactWorldView view = _pool.GetNext();
            view.artifact = artifact;
            view.sprite_renderer.sprite = sprite;
            view.sprite_renderer.color = manifestation.control_state.GetStateColor(time);
            view.sprite_renderer.flipX = manifestation.flip_x;
            view.sprite_renderer.sortingOrder = manifestation.sorting_order;
            view.transform.localPosition = position.value;
            view.transform.localRotation = Quaternion.Euler(0f, 0f, rotation.z);
            view.transform.localScale = Vector3.one * (manifestation.world_size / spriteSize);
        });

        _pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class ArtifactWorldView : MonoBehaviour
    {
        public Entity artifact;
        public SpriteRenderer sprite_renderer;
    }
}
