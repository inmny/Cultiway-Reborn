using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

public class RealmIndicatorRenderSystem : QuerySystem<ActorBinder, RealmVisual>
{
    private readonly MonoObjPool<IndicatorRenderer> _pool;

    public RealmIndicatorRenderSystem()
    {
        var root = new GameObject("realm_indicators");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(RealmIndicatorRenderSystem) + "_Indicator")
            .AddComponent<IndicatorRenderer>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sortingOrder = 5;

        _pool = new MonoObjPool<IndicatorRenderer>(
            prefab,
            root.transform,
            active_action: indicator => indicator.transform.localScale = Vector3.one * 0.1f);
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        var manager = RealmVisualManager.Instance;
        if (manager == null || !manager.IndicatorEnabled)
        {
            _pool.ResetToStart();
            _pool.ClearUnsed();
            return;
        }

        _pool.ResetToStart();
        if (!MapBox.isRenderMiniMap())
        {
            Query.ForEachEntity((ref ActorBinder binder, ref RealmVisual visual, Entity entity) =>
            {
                if (!visual.HasDefinition || visual.indicator_flags == 0) return;
                var sprite = manager.GetIndicatorSprite(visual.indicator_flags);
                if (sprite == null) return;

                var actor = binder.Actor;
                if (actor == null || !actor.isAlive() || !actor.is_visible) return;

                var def = manager.GetDefinition(visual.definition_index);
                if (def == null) return;

                // 根据强度计算不透明度
                float strength = 0f;
                if (entity.HasComponent<Yuanying>())
                {
                    var yuanying = entity.GetComponent<Yuanying>();
                    strength = yuanying.strength;
                }
                else if (entity.HasComponent<Jindan>())
                {
                    var jindan = entity.GetComponent<Jindan>();
                    strength = jindan.strength;
                }

                // 计算不透明度：强度越高，不透明度越高
                // 使用对数函数使强度变化更平滑，范围映射到 0.3-1.0
                var alpha = strength > 0f 
                    ? Mathf.Clamp(0.3f + Mathf.Log10(strength + 1f) * 0.2f, 0.3f, 1.0f)
                    : 0.3f;

                var indicator = _pool.GetNext();
                indicator.sprite_renderer.sprite = sprite;
                
                // 设置半透明颜色
                var color = Color.white;
                color.a = alpha;
                indicator.sprite_renderer.color = color;

                var actorScale = Mathf.Max(actor.stats[S.scale], 0.1f) * Mathf.Max(def.ScaleMultiplier, 0.1f);
                
                // 根据sprite的实际尺寸调整缩放，兼容不同尺寸的图标（如28x28）
                // 基准尺寸为32x32，如果使用28x28则自动调整
                var spriteSize = sprite.rect.size;
                var baseSize = 32f; // 基准尺寸
                var sizeRatio = Mathf.Min(spriteSize.x, spriteSize.y) / baseSize;
                
                // 基础缩放倍率，根据角色大小和境界调整
                var baseScale = actorScale * 0.1f;
                // 根据sprite实际尺寸调整，确保不同尺寸的图标显示大小一致
                var finalScale = baseScale * sizeRatio;
                
                indicator.transform.localScale = Vector3.one * finalScale;

                var position = actor.cur_transform_position;
                position.y += 0.2f * actorScale;
                indicator.transform.localPosition = position;
                indicator.transform.localRotation = Quaternion.identity;
            });
        }

        _pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class IndicatorRenderer : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}

