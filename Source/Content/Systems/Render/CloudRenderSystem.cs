using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Core.Performance;
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
                if (!ActorPresentationRenderer.TryGetPresentationStateForRender(
                        actor_binder.ID,
                        actor_binder.Actor,
                        out ActorPresentationSample sample,
                        out Vector3 position,
                        out bool visible,
                        out _) ||
                    !visible ||
                    !sample.HasFlag(ActorPresentationFlags.Alive) ||
                    !sample.HasFlag(ActorPresentationFlags.Flying))
                {
                    return;
                }

                Cloud cloud = _pool.GetNext();
                var sprite_renderer = cloud.sprite_renderer;
                var transform = cloud.transform;
                float visual_scale = GetFlyVisualScale(in sample);
                transform.localScale = Vector3.one * visual_scale;
                
                if (xian.CurrLevel >= XianSetting.CloudFlyLevel)
                {
                    sprite_renderer.sprite = _cloud_sprite;
                    sprite_renderer.flipX = sample.Flip;
                    transform.localPosition = position;
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
                else if (sample.FlyingVehicleSprite != null)
                {
                    var weapon_sprite = sample.FlyingVehicleSprite;
                    sprite_renderer.sprite = weapon_sprite;
                    if (!sample.FlyingVehicleVertical)
                    {
                        sprite_renderer.flipX = sample.Flip;
                        var flip_mul = sample.Flip ? -1 : 1;
                        var x_offset = 0.5f * weapon_sprite.rect.width - weapon_sprite.pivot.x;
                        transform.localRotation = Quaternion.Euler(0, 0, 0);
                        transform.localPosition = position + new Vector3(x_offset * visual_scale * flip_mul, 0, 0);
                    }
                    else
                    {
                        sprite_renderer.flipX = !sample.Flip;
                        var flip_mul = sample.Flip ? -1 : 1;

                        var x_offset = 0.5f * weapon_sprite.rect.height - weapon_sprite.pivot.y;
                        transform.localRotation = Quaternion.Euler(0, 0, 90 * flip_mul);
                        transform.localPosition = position + new Vector3(x_offset * visual_scale * flip_mul, 0, 0);
                    }
                }
                else
                {
                    sprite_renderer.sprite = _cloud_sprite;
                    sprite_renderer.flipX = sample.Flip;
                    transform.localPosition = position;
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
                //cloud.sprite_renderer.flipX = a.flip;
            });

        _pool.ClearUnsed();
    }

    private float GetFlyVisualScale(
        in ActorPresentationSample sample)
    {
        float textureScale = 1f;
        Sprite sprite = sample.FlyingScaleReferenceSprite;
        if (sprite != null &&
            !_fly_visual_scale_cache.TryGetValue(
                sprite,
                out textureScale))
        {
            int bottomSpan =
                SpritePixelUtils.MeasureBottomOpaqueSpan(sprite);
            textureScale = bottomSpan > BaseBottomOpaqueSpan
                ? bottomSpan / BaseBottomOpaqueSpan
                : 1f;
            _fly_visual_scale_cache[sprite] = textureScale;
        }

        return Mathf.Max(sample.VisualScale, 0.1f) * textureScale;
    }

    [RequireComponent(typeof(SpriteRenderer))]
    class Cloud : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}
