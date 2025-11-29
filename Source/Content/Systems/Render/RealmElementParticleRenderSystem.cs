using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

public class RealmElementParticleRenderSystem : QuerySystem<ActorBinder, RealmVisual, ElementRoot>
{
    private const float ParticleSize = 0.15f;
    private const float ParticleLifetime = 2.0f;
    private const float ParticleSpeed = 0.3f;
    private static ParticleSystem _sharedEmitter;
    private static readonly Gradient FadeOutGradient = new()
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(0f, 1f)
        }
    };

    public RealmElementParticleRenderSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        var manager = RealmVisualManager.Instance;
        if (manager == null || !manager.ParticleEnabled)
        {
            return;
        }

        if (!MapBox.isRenderGameplay())
        {
            return;
        }

        var emitter = GetEmitter();
        if (emitter == null)
        {
            return;
        }

        Query.ForEachEntity((ref ActorBinder binder, ref RealmVisual visual, ref ElementRoot elementRoot, Entity entity) =>
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

            var scale = Mathf.Max(actor.stats[S.scale], 0.2f);
            var radiusBase = 0.35f * scale * def.ScaleMultiplier;
            var time = Time.time;
            var position = actor.cur_transform_position;
            var emitted = 0;

            float EmitCountFor(float value)
            {
                var count = Mathf.RoundToInt(baseCount * (value / total));
                return Mathf.Clamp(count, 0, baseCount - emitted);
            }

            emitted += EmitElement(ElementIndex.Iron, elementRoot.Iron, EmitCountFor, position, radiusBase, time, scale, manager);
            emitted += EmitElement(ElementIndex.Wood, elementRoot.Wood, EmitCountFor, position, radiusBase, time, scale, manager);
            emitted += EmitElement(ElementIndex.Water, elementRoot.Water, EmitCountFor, position, radiusBase, time, scale, manager);
            emitted += EmitElement(ElementIndex.Fire, elementRoot.Fire, EmitCountFor, position, radiusBase, time, scale, manager);
            emitted += EmitElement(ElementIndex.Earth, elementRoot.Earth, EmitCountFor, position, radiusBase, time, scale, manager);

            if (emitted < baseCount)
            {
                // 若由于四舍五入导致不足，补足到目标数量。
                emitted += EmitElement(ElementIndex.Iron, 1f, _ => baseCount - emitted, position, radiusBase, time, scale, manager);
            }
        });
    }

    private static int EmitElement(int elementIndex, float value, Func<float, float> resolver, Vector3 actorPosition, float radiusBase, float time, float scale, RealmVisualManager manager)
    {
        if (value <= 0f) return 0;
        var targetCount = Mathf.RoundToInt(resolver(value));
        if (targetCount <= 0) return 0;

        var color = manager.GetElementColor(elementIndex);
        var emitter = GetEmitter();
        if (emitter == null) return 0;

        for (var i = 0; i < targetCount; i++)
        {
            var phase = time * (0.6f + 0.15f * elementIndex) + i;
            var radius = radiusBase * (0.7f + 0.2f * (i % 3));
            var angle = phase;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            var particlePosition = actorPosition + offset;

            // 计算粒子速度方向（垂直于半径方向，形成旋转效果）
            var velocity = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0) * ParticleSpeed;

            var emitParams = new ParticleSystem.EmitParams
            {
                position = particlePosition,
                startColor = color,
                startSize = ParticleSize * (0.8f + 0.2f * (i % 4)),
                velocity = velocity
            };
            emitter.Emit(emitParams, 1);
        }

        return targetCount;
    }

    private static ParticleSystem GetEmitter()
    {
        if (_sharedEmitter != null)
        {
            return _sharedEmitter;
        }

        var parent = World.world?.transform;
        if (parent == null)
        {
            return null;
        }

        var emitterObject = new GameObject("Cultiway_RealmElementParticles_Global");
        emitterObject.transform.SetParent(parent, false);
        emitterObject.transform.localPosition = Vector3.zero;

        var particleSystem = emitterObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = ParticleLifetime;
        main.startSpeed = ParticleSpeed;
        main.startSize = ParticleSize;
        main.maxParticles = 1024;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = false;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient);

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        // 粒子会继续旋转，但速度逐渐衰减
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0f);

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = -3;
        if (LibraryMaterials.instance != null)
        {
            renderer.material = LibraryMaterials.instance.mat_world_object;
        }
        particleSystem.Play();
        // 统一使用一个粒子系统，避免为每个粒子或单位生成多余的GameObject
        _sharedEmitter = particleSystem;
        return _sharedEmitter;
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

