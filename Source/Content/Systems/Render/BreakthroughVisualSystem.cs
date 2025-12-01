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

/// <summary>
///     突破异象的粒子渲染系统。
/// </summary>
public class BreakthroughVisualSystem : QuerySystem<ActorBinder, RealmVisual, XianBreakthroughState>
{
    private const float ParticleSize = 0.25f;
    private const float ParticleLifetime = 2.4f;
    private const float BaseSpeed = 0.35f;
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

    public BreakthroughVisualSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        var delta = Tick.deltaTime > 0 ? Tick.deltaTime : Time.deltaTime;
        
        // 第一部分：处理计时器减少和组件回收（总是执行）
        Query.ForEachEntity((ref ActorBinder binder, ref RealmVisual visual, ref XianBreakthroughState state, Entity entity) =>
        {
            var actor = binder.Actor;
            if (actor == null || !actor.isAlive())
            {
                return;
            }
            state.visual_timer -= delta;

            if (state.visual_timer <= 0f)
            {
                state.visual_timer = 0f;
                if (visual.visual_state == RealmVisual.VisualStateBreakthrough)
                {
                    visual.visual_state = RealmVisual.VisualStateDefault;
                }
                CommandBuffer.RemoveComponent<XianBreakthroughState>(entity.Id);
                return;
            }
        });
        
        CommandBuffer.Playback();

        // 第二部分：处理粒子渲染（只在渲染时执行）
        var manager = BreakthroughVisualManager.Instance;
        if (manager == null || !manager.Enabled)
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

        // 获取游戏倍率并应用到粒子系统
        var timeScale = Mathf.Max(0.01f, Config.time_scale_asset.multiplier);
        var main = emitter.main;
        main.simulationSpeed = timeScale;

        Query.ForEachEntity((ref ActorBinder binder, ref RealmVisual visual, ref XianBreakthroughState state, Entity entity) =>
        {
            var actor = binder.Actor;
            if (actor == null || !actor.isAlive() || !actor.is_visible)
            {
                return;
            }
            var def = manager.GetDefinition(state.visual_level);
            if (def == null)
            {
                return;
            }
            visual.visual_state = RealmVisual.VisualStateBreakthrough;

            var scale = Mathf.Max(actor.stats[S.scale], 0.35f);
            var position = actor.cur_transform_position;
            var time = Time.time;
            switch (def.ToLevel)
            {
                case 1:
                    EmitQiToFoundation(emitter, def, position, scale, time);
                    break;
                case 2:
                    EmitFoundationToJindan(emitter, def, position, scale, time, ref state);
                    break;
                case 3:
                    EmitJindanToYuanying(emitter, def, position, scale, time, ref state);
                    break;
                default:
                    EmitGenericBurst(emitter, def, position, scale, time);
                    break;
            }
        });
    }

    private static void EmitQiToFoundation(ParticleSystem emitter, BreakthroughVisualDefinition def, Vector3 position, float scale, float time)
    {
        var count = Mathf.Clamp(def.BaseParticleCount, 8, 64);
        var radius = def.Radius * scale;
        var speed = BaseSpeed * (1.2f + def.ExtraIntensity);
        for (var i = 0; i < count; i++)
        {
            var angle = time * 2.4f + i * 0.35f;
            var rotRadius = radius * (0.7f + 0.3f * Mathf.Sin(time * 1.1f + i * 0.2f));
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * rotRadius;
            var tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0) * speed;
            var color = Color.Lerp(def.PrimaryColor, def.SecondaryColor, (Mathf.Sin(angle + time) + 1f) * 0.5f);

            Emit(emitter, position + offset, color, ParticleSize * scale, ParticleLifetime * 0.8f, tangent);
        }

        // 灵液滴落
        for (var i = 0; i < 3; i++)
        {
            var dropOffset = new Vector3(Randy.randomFloat(-0.1f, 0.1f), 0.2f * scale + i * 0.02f, 0);
            var dropVelocity = new Vector3(0, -0.45f * scale, 0);
            Emit(emitter, position + dropOffset, def.PrimaryColor, ParticleSize * 1.6f * scale, ParticleLifetime * 0.5f, dropVelocity);
        }

        // 排浊灰粒子
        if (Randy.randomChance(0.35f))
        {
            for (var i = 0; i < 4; i++)
            {
                var dirAngle = time * 3f + i * 1.3f;
                var dir = new Vector3(Mathf.Cos(dirAngle), Mathf.Sin(dirAngle), 0);
                Emit(emitter, position + dir * 0.12f * scale, new Color(0.45f, 0.45f, 0.45f, 0.8f),
                    ParticleSize * 0.9f * scale, ParticleLifetime * 0.4f, dir * 0.25f * scale);
            }
        }
    }

    private static void EmitFoundationToJindan(ParticleSystem emitter, BreakthroughVisualDefinition def, Vector3 position, float scale, float time, ref XianBreakthroughState state)
    {
        var count = Mathf.Clamp(def.BaseParticleCount, 10, 72);
        var funnelRadius = def.Radius * scale * 1.2f;
        var height = def.HeightOffset + 0.4f * scale;
        var primary = def.PrimaryColor;
        var secondary = def.SecondaryColor;

        // 漏斗云/灵气
        if (def.UseCloud)
        {
            for (var i = 0; i < count / 2; i++)
            {
                var angle = time * 1.4f + i * 0.5f;
                var r = funnelRadius * (0.6f + 0.3f * Mathf.Sin(angle + time * 0.3f));
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * r;
                var pos = position + offset + new Vector3(0, height, 0);
                var vel = (position - pos).normalized * BaseSpeed * 1.2f;
                Emit(emitter, pos, Color.Lerp(primary, secondary, 0.4f), ParticleSize * 1.1f * scale, ParticleLifetime, vel);
            }
        }

        // 灵流下灌
        for (var i = 0; i < count / 3; i++)
        {
            var offsetX = Randy.randomFloat(-funnelRadius * 0.4f, funnelRadius * 0.4f);
            var start = position + new Vector3(offsetX, height + 0.2f * scale, 0);
            var dir = (position - start).normalized;
            Emit(emitter, start, Color.Lerp(primary, secondary, 0.7f), ParticleSize * 0.9f * scale, ParticleLifetime * 0.9f, dir * BaseSpeed * 1.5f);
        }

        // 虚丹旋转
        var pillRadius = 0.2f * scale;
        for (var i = 0; i < 6; i++)
        {
            var angle = time * 3.2f + i * Mathf.PI / 3f;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * pillRadius;
            var pos = position + offset;
            var vel = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0) * BaseSpeed * 1.1f;
            Emit(emitter, pos, secondary, ParticleSize * 1.4f * scale, ParticleLifetime * 0.7f, vel);
        }

        // 成丹爆闪（首帧触发）
        if (!state.HasFlag(XianBreakthroughState.FlagShockwaveTriggered))
        {
            state.SetFlag(XianBreakthroughState.FlagShockwaveTriggered);
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Emit(emitter, position, secondary, ParticleSize * 1.8f * scale, ParticleLifetime * 0.6f, dir * BaseSpeed * 2.2f);
            }
        }
    }

    private static void EmitJindanToYuanying(ParticleSystem emitter, BreakthroughVisualDefinition def, Vector3 position, float scale, float time, ref XianBreakthroughState state)
    {
        var count = Mathf.Clamp(def.BaseParticleCount, 12, 80);
        var radius = def.Radius * scale;
        var primary = def.PrimaryColor;
        var secondary = def.SecondaryColor;

        // 裂丹闪烁
        for (var i = 0; i < count / 2; i++)
        {
            var angle = time * 4f + i * 0.7f;
            var jitter = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (radius * 0.6f);
            var pos = position + jitter;
            Emit(emitter, pos, Color.Lerp(primary, secondary, 0.5f), ParticleSize * 0.9f * scale, ParticleLifetime * 0.5f, Vector3.zero);
        }

        // 冲击波（只触发一次）
        if (def.Shockwave && !state.HasFlag(XianBreakthroughState.FlagShockwaveTriggered))
        {
            state.SetFlag(XianBreakthroughState.FlagShockwaveTriggered);
            for (var i = 0; i < 18; i++)
            {
                var angle = i * Mathf.PI * 2f / 18f;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Emit(emitter, position, secondary, ParticleSize * 2.1f * scale, ParticleLifetime * 0.9f, dir * BaseSpeed * 2.5f);
            }
        }

        // 元婴虚影/道韵
        var auraRadius = radius * 0.6f;
        for (var i = 0; i < count / 3; i++)
        {
            var angle = time * 2.2f + i * 0.9f;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * auraRadius;
            var pos = position + offset;
            var vel = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0) * BaseSpeed * 0.8f;
            Emit(emitter, pos, secondary, ParticleSize * 1.1f * scale, ParticleLifetime, vel);
        }

        // 神识暴涨：向外丝线
        for (var i = 0; i < 8; i++)
        {
            var angle = time * 1.5f + i * Mathf.PI / 4f;
            var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            Emit(emitter, position + dir * 0.1f * scale, Color.Lerp(secondary, Color.white, 0.4f),
                ParticleSize * 0.7f * scale, ParticleLifetime * 1.2f, dir * BaseSpeed * 1.4f);
        }

        // 天象：祥云或劫雷
        if (!state.HasFlag(XianBreakthroughState.FlagWeatherRolled))
        {
            state.SetFlag(XianBreakthroughState.FlagWeatherRolled);
            var roll = Randy.random();
            if (roll < def.LightningChance)
            {
                SpawnLightning(position);
                state.SetFlag(XianBreakthroughState.FlagSpecialTriggered);
            }
            else if (roll < def.LightningChance + 0.3f && def.UseCloud)
            {
                EmitCloud(emitter, position, scale, def);
            }
        }
    }

    private static void EmitGenericBurst(ParticleSystem emitter, BreakthroughVisualDefinition def, Vector3 position, float scale, float time)
    {
        var count = Mathf.Clamp(def.BaseParticleCount, 6, 48);
        for (var i = 0; i < count; i++)
        {
            var angle = time * 1.2f + i * 0.8f;
            var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            var color = Color.Lerp(def.PrimaryColor, def.SecondaryColor, (float)i / count);
            Emit(emitter, position + dir * def.Radius * scale, color, ParticleSize * scale, ParticleLifetime, dir * BaseSpeed);
        }
    }

    private static void EmitCloud(ParticleSystem emitter, Vector3 position, float scale, BreakthroughVisualDefinition def)
    {
        var cloudRadius = def.Radius * scale * 0.8f;
        var height = def.HeightOffset + 0.6f * scale;
        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.PI * 2f / 12f;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * cloudRadius;
            var pos = position + offset + new Vector3(0, height, 0);
            Emit(emitter, pos, Color.white, ParticleSize * 1.2f * scale, ParticleLifetime * 1.4f, new Vector3(0, 0.1f * scale, 0));
        }
    }

    private static void Emit(ParticleSystem emitter, Vector3 position, Color color, float size, float lifetime, Vector3 velocity)
    {
        var emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            startColor = color,
            startSize = size,
            startLifetime = lifetime,
            velocity = velocity
        };
        emitter.Emit(emitParams, 1);
    }

    private static void SpawnLightning(Vector3 position)
    {
        try
        {
            EffectsLibrary.spawnAt("fx_lightning_small", position, 0.35f);
        }
        catch (Exception)
        {
            // 忽略特效缺失
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

        var emitterObject = new GameObject("Cultiway_BreakthroughParticles_Global");
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
        main.maxParticles = 2048;

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
        renderer.sortingOrder = -2;
        if (LibraryMaterials.instance != null)
        {
            renderer.material = LibraryMaterials.instance.mat_world_object;
        }

        particleSystem.Play();
        _sharedEmitter = particleSystem;
        return _sharedEmitter;
    }
}
