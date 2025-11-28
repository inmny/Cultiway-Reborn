using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.RealmVisual;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;
using RealmVisualComponent = Cultiway.Content.Components.RealmVisual;

namespace Cultiway.Content.Systems.Render;

public class RealmElementParticleRenderSystem : QuerySystem<ActorBinder, RealmVisualComponent, ElementRoot>
{
    private readonly MonoObjPool<ElementParticle> _pool;

    public RealmElementParticleRenderSystem()
    {
        var root = new GameObject("realm_element_particles");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(RealmElementParticleRenderSystem) + "_Particle")
            .AddComponent<ElementParticle>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sortingOrder = -3;

        _pool = new MonoObjPool<ElementParticle>(
            prefab,
            root.transform,
            active_action: particle => particle.transform.localScale = Vector3.one * 0.1f);
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        var manager = RealmVisualManager.Instance;
        if (manager == null || !manager.ParticleEnabled || manager.GetElementParticleSprite() == null)
        {
            _pool.ResetToStart();
            _pool.ClearUnsed();
            return;
        }

        _pool.ResetToStart();
        if (MapBox.isRenderMiniMap())
        {
            _pool.ClearUnsed();
            return;
        }

        Query.ForEachEntity((ref ActorBinder binder, ref RealmVisualComponent visual, ref ElementRoot elementRoot, Entity entity) =>
        {
            if (!visual.HasDefinition || !visual.has_element_root) return;
            var def = manager.GetDefinition(visual.definition_index);
            if (def == null) return;

            var actor = binder.Actor;
            if (actor == null || !actor.isAlive() || !actor.is_visible) return;

            var baseCount = Mathf.Clamp(def.BaseParticleCount, 0, manager.MaxParticlesPerActor);
            if (baseCount <= 0) return;

            var total = elementRoot.ElementSum();
            if (total <= 0f) total = 1f;

            var sprite = manager.GetElementParticleSprite();
            if (sprite == null) return;

            var scale = Mathf.Max(actor.stats[S.scale], 0.2f);
            var radiusBase = 0.35f * scale * def.ScaleMultiplier;
            var time = Time.time;
            var emitted = 0;

            float EmitCountFor(float value)
            {
                var count = Mathf.RoundToInt(baseCount * (value / total));
                return Mathf.Clamp(count, 0, baseCount - emitted);
            }

            emitted += EmitElement(ElementIndex.Iron, elementRoot.Iron, EmitCountFor);
            emitted += EmitElement(ElementIndex.Wood, elementRoot.Wood, EmitCountFor);
            emitted += EmitElement(ElementIndex.Water, elementRoot.Water, EmitCountFor);
            emitted += EmitElement(ElementIndex.Fire, elementRoot.Fire, EmitCountFor);
            emitted += EmitElement(ElementIndex.Earth, elementRoot.Earth, EmitCountFor);

            if (emitted < baseCount)
            {
                // 若由于四舍五入导致不足，补足到目标数量。
                emitted += EmitElement(ElementIndex.Iron, 1f, _ => baseCount - emitted);
            }

            int EmitElement(int elementIndex, float value, Func<float, float> resolver)
            {
                if (value <= 0f) return 0;
                var targetCount = Mathf.RoundToInt(resolver(value));
                if (targetCount <= 0) return 0;

                var color = manager.GetElementColor(elementIndex);

                for (var i = 0; i < targetCount; i++)
                {
                    var particle = _pool.GetNext();
                    var renderer = particle.sprite_renderer;
                    renderer.sprite = sprite;
                    renderer.color = color;

                    var phase = time * (0.6f + 0.15f * elementIndex) + i;
                    var radius = radiusBase * (0.7f + 0.2f * (i % 3));
                    var offset = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0) * radius;
                    particle.transform.localPosition = actor.cur_transform_position + offset;

                    var scaleMultiplier = 0.25f + 0.05f * (i % 4);
                    particle.transform.localScale = Vector3.one * scale * scaleMultiplier;
                }

                return targetCount;
            }
        });

        _pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class ElementParticle : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}

internal static class ElementRootExtensions
{
    public static float ElementSum(this ElementRoot root)
    {
        return Mathf.Max(root.Iron, 0f)
               + Mathf.Max(root.Wood, 0f)
               + Mathf.Max(root.Water, 0f)
               + Mathf.Max(root.Fire, 0f)
               + Mathf.Max(root.Earth, 0f);
    }
}

