using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

public class CloudRenderSystem : QuerySystem<ActorBinder, Xian>
{
    private const float BaseBottomOpaqueSpan = 6f;

    private readonly Dictionary<Sprite, float> _fly_visual_scale_cache = new();
    private MonoObjPool<Cloud> _pool;
    private Sprite _cloud_sprite;
    public CloudRenderSystem()
    {
        var obj = new GameObject("xian_clouds");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview("XianCloud").AddComponent<Cloud>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sprite = SpriteTextureLoader.getSprite("cultiway/special_effects/clouds/simple_cloud");
        _cloud_sprite = prefab.sprite_renderer.sprite;
        _pool = new(prefab, obj.transform,
            active_action: [Hotfixable](cloud) => { cloud.transform.localScale = Vector3.one * 0.01f; });
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        if (!MapBox.isRenderMiniMap())
            Query.ForEachEntity([Hotfixable](ref ActorBinder actor_binder, ref Xian xian, Entity e) =>
            {
                Actor a = actor_binder.Actor;
                if (a == null || !a.isAlive()) return;
                if (!a.is_visible || !a.data.hasFlag(ContentActorDataKeys.IsFlying_flag)) return;
                Cloud cloud = _pool.GetNext();
                var sprite_renderer = cloud.sprite_renderer;
                var transform = cloud.transform;
                float visual_scale = GetFlyVisualScale(a);
                transform.localScale = Vector3.one * visual_scale;
                
                if (a.GetExtend().GetCultisys<Xian>().CurrLevel >= XianSetting.CloudFlyLevel)
                {
                    sprite_renderer.sprite = _cloud_sprite;
                    sprite_renderer.flipX = a.flip;
                    transform.localPosition = actor_binder.Actor.cur_transform_position;
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
                else if (a.hasWeapon())
                {
                    var weapon_sprite = ItemRendering.getItemMainSpriteFrame(a.getWeaponAsset());
                    sprite_renderer.sprite = weapon_sprite;
                    if (weapon_sprite.rect.width >= weapon_sprite.rect.height)
                    {
                        sprite_renderer.flipX = a.flip;
                        var flip_mul = a.flip ? -1 : 1;
                        var x_offset = 0.5f * weapon_sprite.rect.width - weapon_sprite.pivot.x;
                        transform.localRotation = Quaternion.Euler(0, 0, 0);
                        transform.localPosition = actor_binder.Actor.cur_transform_position + new Vector3(x_offset * visual_scale * flip_mul, 0, 0);
                    }
                    else
                    {
                        sprite_renderer.flipX = !a.flip;
                        var flip_mul = a.flip ? -1 : 1;

                        var x_offset = 0.5f * weapon_sprite.rect.height - weapon_sprite.pivot.y;
                        transform.localRotation = Quaternion.Euler(0, 0, 90 * flip_mul);
                        transform.localPosition = actor_binder.Actor.cur_transform_position + new Vector3(x_offset * visual_scale * flip_mul, 0, 0);
                    }
                }
                else
                {
                    sprite_renderer.sprite = _cloud_sprite;
                    sprite_renderer.flipX = a.flip;
                    transform.localPosition = actor_binder.Actor.cur_transform_position;
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
                //cloud.sprite_renderer.flipX = a.flip;
            });

        _pool.ClearUnsed();
    }

    /// <summary>
    /// 根据单位第一帧底部两行非透明像素跨度调整飞行载具大小；跨度为 6 像素时保持原大小。
    /// </summary>
    private float GetFlyVisualScale(Actor actor)
    {
        Sprite sprite = GetFirstActorSprite(actor);
        float texture_scale = 1f;
        if (sprite != null)
        {
            if (!_fly_visual_scale_cache.TryGetValue(sprite, out texture_scale))
            {
                int bottom_span = SpritePixelUtils.MeasureBottomOpaqueSpan(sprite);
                texture_scale = bottom_span > BaseBottomOpaqueSpan ? bottom_span / BaseBottomOpaqueSpan : 1f;
                _fly_visual_scale_cache[sprite] = texture_scale;
            }
        }

        return actor.stats[S.scale] * texture_scale;
    }

    /// <summary>
    /// 取当前单位贴图的第一帧，优先使用飞行时固定显示的 idle 第一帧。
    /// </summary>
    private static Sprite GetFirstActorSprite(Actor actor)
    {
        if (actor == null) return null;

        if (actor.asset.has_override_sprite)
        {
            return actor.calculateMainSprite();
        }

        actor.checkAnimationContainer();
        var frames = actor.animation_container?.idle?.frames;
        if (frames is { Length: > 0 }) return frames[0];

        return actor.calculateMainSprite();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    class Cloud : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}
