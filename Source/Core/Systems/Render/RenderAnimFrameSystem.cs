using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

public class RenderAnimFrameSystem : BaseSystem
{
    private readonly MonoObjPool<AnimRenderer>                                  _pool;
    private readonly ArchetypeQuery<Position, Scale, AnimData, AnimBindRenderer> init_query;
    private readonly ArchetypeQuery<Position, AnimBindRenderer>                  pos_query;
    private readonly ArchetypeQuery<Rotation, AnimBindRenderer>                  rot_query;
    private readonly ArchetypeQuery<Scale, AnimBindRenderer>                     scale_query;
    private readonly ArchetypeQuery<AnimLinearLayout, Scale, AnimBindRenderer>           linear_layout_query;
    private readonly ArchetypeQuery<AnimTint, AnimBindRenderer>                  tint_query;
    private readonly ArchetypeQuery<AnimData, AnimAfterimage, Rotation, AnimBindRenderer> afterimage_query;
    private readonly ArchetypeQuery<AnimBindRenderer>                            single_query;
    private readonly Dictionary<Sprite, Sprite>                                  full_rect_sprites = new();

    public RenderAnimFrameSystem(EntityStore world)
    {
        var obj = new GameObject("general_anims");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        _pool = AnimRenderer.NewPool(obj.transform);


        var filter = new QueryFilter();
        filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());

        single_query = world.Query<AnimBindRenderer>(filter);
        init_query = world.Query<Position, Scale, AnimData, AnimBindRenderer>(filter);
        pos_query = world.Query<Position, AnimBindRenderer>(filter);
        rot_query = world.Query<Rotation, AnimBindRenderer>(filter);
        scale_query = world.Query<Scale, AnimBindRenderer>(filter);
        linear_layout_query = world.Query<AnimLinearLayout, Scale, AnimBindRenderer>(filter);
        tint_query = world.Query<AnimTint, AnimBindRenderer>(filter);
        afterimage_query = world.Query<AnimData, AnimAfterimage, Rotation, AnimBindRenderer>(filter);
    }
    [Hotfixable]
    protected override void OnUpdateGroup()
    {
        if (!MapBox.isRenderMiniMap() || ModClass.I.GetConfig()["AnimSettings"]["ALL_RENDER"].BoolVal)
        {
            init_query.ForEachEntity(
                (ref Position pos, ref Scale scale, ref AnimData anim_data, ref AnimBindRenderer bind_renderer,
                    Entity entity) =>
                {
                    Sprite sprite = ResolveRenderSprite(entity, anim_data.CurrentFrame);
                    if (!NeedRender(entity, sprite, ref pos, ref scale))
                    {
                        if (bind_renderer.value != null && bind_renderer.value.gameObject.activeSelf)
                        {
                            bind_renderer.value.gameObject.SetActive(false);
                        }
                        return;
                    }

                    if (bind_renderer.value != null)
                    {
                        bind_renderer.value.bind.sprite = sprite;
                        if (!bind_renderer.value.hasTint)
                        {
                            bind_renderer.value.bind.color = Color.white;
                        }
                        if (!bind_renderer.value.gameObject.activeSelf)
                        {
                            bind_renderer.value.gameObject.SetActive(true);
                        }
                    }
                    else
                    {
                        AnimRenderer renderer = _pool.GetNext();
                        renderer.bind.sprite = sprite;
                        bind_renderer.value = renderer;
                    }

                    if (!entity.HasComponent<AnimAfterimage>())
                    {
                        bind_renderer.value.HideAfterimage();
                    }
                });
            pos_query.ForEachComponents((ref Position pos, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.transform.localPosition = new Vector3(pos.x, pos.y + pos.z);
            });
            scale_query.ForEachComponents((ref Scale scale, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.transform.localScale = scale.value;
            });
            linear_layout_query.ForEachComponents(
                (ref AnimLinearLayout layout, ref Scale scale, ref AnimBindRenderer bindRenderer) =>
                {
                    if (bindRenderer.value == null) return;

                    SpriteRenderer spriteRenderer = bindRenderer.value.bind;
                    if (layout.Mode == AnimLinearLayoutMode.Tile)
                    {
                        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                        spriteRenderer.tileMode = SpriteTileMode.Continuous;
                        spriteRenderer.size = new Vector2(
                            layout.WorldLength / scale.x,
                            spriteRenderer.sprite.bounds.size.y);
                    }
                    else
                    {
                        spriteRenderer.drawMode = SpriteDrawMode.Simple;
                    }
                });
            tint_query.ForEachComponents((ref AnimTint tint, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.hasTint = true;
                bind_renderer.value.bind.color = tint.Value;
            });
            rot_query.ForEach((rotations, bindRenderers, entities) =>
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    ref var bindRenderer = ref bindRenderers[i];
                    if (bindRenderer.value == null) continue;

                    ref var rot = ref rotations[i];
                    var entity = entities.EntityAt(i);
                    var angle = AnimVisualUtils.GetVisualAngle(entity, ref rot);
                    bindRenderer.value.transform.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }).RunParallel();
            afterimage_query.ForEach((animData, afterimages, rotations, bindRenderers, entities) =>
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    ref var bindRenderer = ref bindRenderers[i];
                    var renderer = bindRenderer.value;
                    if (renderer == null || !renderer.gameObject.activeSelf) continue;

                    ref var rotation = ref rotations[i];
                    ref var afterimage = ref afterimages[i];
                    var entity = entities.EntityAt(i);
                    if (entity.Tags.Has<TagSuppressAnimAfterimage>())
                    {
                        renderer.HideAfterimage();
                        continue;
                    }
                    var localBackDirection = AnimVisualUtils.GetLocalBackDirection(entity, ref rotation);
                    var movementAngle = AnimVisualUtils.GetRotationAngle(ref rotation);
                    renderer.SetAfterimage(animData[i].CurrentFrame, renderer.bind.color, ref afterimage,
                        localBackDirection, movementAngle);
                }
            }).Run();
        }
        else
        {
            single_query.ForEachComponents((ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value!= null && bind_renderer.value.gameObject.activeSelf)
                {
                    bind_renderer.value.gameObject.SetActive(false);
                }
            });
        }
        /*
        rot_query.ForEachComponents((ref Rotation rot, ref AnimBindRenderer bind_renderer) =>
        {
            if (bind_renderer.value == null) return;
            bind_renderer.value.transform.localScale = Vector3.Scale(bind_renderer.value.transform.localScale,
                new Vector3(0.5f + 1 / Mathf.Max(rot.z / Mathf.Max(0.01f, rot.in_plane.magnitude) + 1, 1), 1, 1));
        });
        */
    }
    [Hotfixable]
    private bool NeedRender(Entity entity, Sprite sprite, ref Position pos, ref Scale scale)
    {
        if (sprite == null) return false;
        var size = sprite.rect.size;
        if (entity.HasComponent<AnimLinearLayout>())
        {
            ref AnimLinearLayout layout = ref entity.GetComponent<AnimLinearLayout>();
            if (layout.Mode == AnimLinearLayoutMode.Tile)
            {
                size.x = layout.WorldLength / scale.x;
            }
        }
        // 先转换到屏幕坐标
        var screen_pos = World.world.camera.WorldToScreenPoint(new Vector3(pos.x, pos.y + pos.z));
        var screen_rect = new Rect(screen_pos.x - size.x * scale.value.x / 2, screen_pos.y - size.y * scale.value.y / 2, size.x * scale.value.x, size.y * scale.value.y);
        // 如果和屏幕有交集
        var screen = new Rect(0, 0, Screen.width, Screen.height);
        if (!ShapeUtils.OverlapRect(screen, screen_rect)) return false;
        return true;
    }

    private Sprite GetFullRectSprite(Sprite source)
    {
        if (full_rect_sprites.TryGetValue(source, out Sprite fullRect)) return fullRect;

        Rect rect = source.rect;
        Vector2 pivot = new(source.pivot.x / rect.width, source.pivot.y / rect.height);
        fullRect = Sprite.Create(
            source.texture,
            rect,
            pivot,
            source.pixelsPerUnit,
            1,
            SpriteMeshType.FullRect,
            source.border);
        fullRect.name = $"{source.name}_full_rect";
        full_rect_sprites.Add(source, fullRect);
        return fullRect;
    }

    private Sprite ResolveRenderSprite(Entity entity, Sprite source)
    {
        if (!entity.HasComponent<AnimLinearLayout>()) return source;

        ref AnimLinearLayout layout = ref entity.GetComponent<AnimLinearLayout>();
        return layout.Mode == AnimLinearLayoutMode.Tile
            ? GetFullRectSprite(source)
            : source;
    }
}
