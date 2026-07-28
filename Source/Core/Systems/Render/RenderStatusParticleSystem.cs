using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Performance;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

public class RenderStatusParticleSystem : QuerySystem<StatusComponent, StatusParticleState>
{
    private const float ParticleSize = 0.2f;
    private const float ParticleLifetime = 1.25f;
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

    public RenderStatusParticleSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var deltaTime = Tick.deltaTime;
        

        Query.ForEachEntity(((ref StatusComponent status, ref StatusParticleState state, Entity entity) =>
        {
            var settings = status.Type.ParticleSettings;
            if (!settings.enabled || settings.count <= 0)
            {
                return;
            }

            state.timer += deltaTime;
            var interval = Mathf.Max(settings.interval, 0.05f);
            if (state.timer >= interval)
            {
                // 表现层不追赶历史发射次数，避免长帧后集中喷发。
                state.timer %= interval;
                SpawnForOwners(entity, settings);
            }
        }));
        // 获取游戏倍率并应用到粒子系统
        var timeScale = Mathf.Max(0.01f, Config.time_scale_asset.multiplier);
        if (_sharedEmitter != null)
        {
            var main = _sharedEmitter.main;
            main.simulationSpeed = timeScale;
        }
    }

    private static void SpawnForOwners(Entity statusEntity, StatusParticleSettings settings)
    {
        foreach (var owner in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.HasComponent<ActorBinder>()) continue;
            var binder = owner.GetComponent<ActorBinder>();
            if (!ActorPresentationRenderer.TryGetPresentationStateForRender(
                    binder.ID,
                    binder.Actor,
                    out ActorPresentationSample sample,
                    out Vector3 position,
                    out bool visible,
                    out _) ||
                !visible ||
                !sample.HasFlag(ActorPresentationFlags.Alive))
            {
                continue;
            }

            EmitParticles(in sample, position, settings);
        }
    }

    private static void EmitParticles(
        in ActorPresentationSample sample,
        Vector3 position,
        StatusParticleSettings settings)
    {
        if (!MapBox.isRenderGameplay())
        {
            return;
        }

        Vector2 spriteSize = sample.MainSprite == null
            ? Vector2.zero
            : sample.MainSprite.bounds.size *
              new Vector2(sample.Scale.x, sample.Scale.y);

        var emitter = GetEmitter();
        if (emitter == null)
        {
            return;
        }

        for (var i = 0; i < settings.count; i++)
        {
            if (Randy.randomBool())
            {
                continue;
            }

            var jittered = position;
            jittered.x += Randy.randomFloat(
                -spriteSize.x / 2f,
                spriteSize.x / 2f);
            jittered.y += Randy.randomFloat(0, spriteSize.y);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = jittered,
                startColor = settings.color,
                startSize = ParticleSize
            };
            emitter.Emit(emitParams, 1);
        }
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

        var emitterObject = new GameObject("Cultiway_StatusParticles_Global");
        emitterObject.transform.SetParent(parent, false);
        emitterObject.transform.localPosition = Vector3.zero;

        var particleSystem = emitterObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = ParticleLifetime;
        main.startSpeed = 0f;
        main.startSize = ParticleSize;
        main.maxParticles = 512;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = false;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient);

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        renderer.sortingOrder = 0;
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
