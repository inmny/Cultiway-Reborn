using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.RealmVisual;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;
using RealmVisualComponent = Cultiway.Content.Components.RealmVisual;

namespace Cultiway.Content.Systems.Render;

public class RealmAuraRenderSystem : QuerySystem<ActorBinder, RealmVisualComponent>
{
    private readonly MonoObjPool<AuraRenderer> _pool;

    public RealmAuraRenderSystem()
    {
        var root = new GameObject("realm_auras");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(RealmAuraRenderSystem) + "_Aura").AddComponent<AuraRenderer>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sortingOrder = -5;

        _pool = new MonoObjPool<AuraRenderer>(
            prefab,
            root.transform,
            active_action: aura => aura.transform.localScale = Vector3.one * 0.01f);
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        var manager = RealmVisualManager.Instance;
        if (manager == null || !manager.AuraEnabled)
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
                if (!visual.HasDefinition) return;
                var actor = binder.Actor;
                if (actor == null || !actor.isAlive() || !actor.is_visible) return;

                var definition = manager.GetDefinition(visual.definition_index);
                if (definition == null || definition.AuraSprite == null) return;

                var aura = _pool.GetNext();
                var renderer = aura.sprite_renderer;
                renderer.sprite = definition.AuraSprite;

                var baseColor = definition.AuraColor;
                var breathPhase = Time.time * definition.BreathSpeed;
                var t = (Mathf.Sin(breathPhase) + 1f) * 0.5f;
                var alpha = Mathf.Lerp(definition.AlphaMin, definition.AlphaMax, t);
                baseColor.a = alpha;
                renderer.color = baseColor;

                var scale = Mathf.Max(actor.stats[S.scale], 0.1f) * Mathf.Max(definition.ScaleMultiplier, 0.1f);
                var breathScale = 1f + Mathf.Sin(breathPhase) * definition.BreathAmplitude;

                aura.transform.localScale = Vector3.one * scale * breathScale;
                aura.transform.localRotation = Quaternion.identity;
                aura.transform.localPosition = actor.cur_transform_position;
            });
        }

        _pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class AuraRenderer : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}

