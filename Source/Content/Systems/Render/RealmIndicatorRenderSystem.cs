using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.RealmVisual;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;
using RealmVisualComponent = Cultiway.Content.Components.RealmVisual;

namespace Cultiway.Content.Systems.Render;

public class RealmIndicatorRenderSystem : QuerySystem<ActorBinder, RealmVisualComponent>
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
            Query.ForEachEntity((ref ActorBinder binder, ref RealmVisualComponent visual, Entity entity) =>
            {
                if (!visual.HasDefinition || visual.indicator_flags == 0) return;
                var sprite = manager.GetIndicatorSprite(visual.indicator_flags);
                if (sprite == null) return;

                var actor = binder.Actor;
                if (actor == null || !actor.isAlive() || !actor.is_visible) return;

                var def = manager.GetDefinition(visual.definition_index);
                if (def == null) return;

                var indicator = _pool.GetNext();
                indicator.sprite_renderer.sprite = sprite;
                indicator.sprite_renderer.color = Color.white;

                var scale = Mathf.Max(actor.stats[S.scale], 0.1f) * Mathf.Max(def.ScaleMultiplier, 0.1f);
                indicator.transform.localScale = Vector3.one * scale * 0.4f;

                var position = actor.cur_transform_position;
                position.y += actor.getHeight() + 0.2f * scale;
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

