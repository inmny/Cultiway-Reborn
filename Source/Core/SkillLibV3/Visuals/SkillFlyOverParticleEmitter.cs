using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 使用单个全局 ParticleSystem 绘制法术掠地粒子，避免为轨迹采样点创建临时对象。
/// </summary>
public static class SkillFlyOverParticleEmitter
{
    private const int MaxParticles = 2048;
    private const int MaxEmissionSources = 512;
    private static ParticleSystem _sharedEmitter;
    private static Transform _worldRoot;
    private static readonly Dictionary<EmissionSourceKey, EmissionSource> EmissionSources = new();
    private static readonly List<EmissionSourceKey> ActiveKeys = new(MaxEmissionSources);
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

    public static void Activate(
        WorldTile tile,
        SkillVfxElementAsset element,
        SemanticColorPalette colorPalette)
    {
        if (!MapBox.isRenderGameplay()) return;

        EnsureWorld();
        var style = element.FlyOverParticles;
        if (style.ParticlesPerEmission <= 0 || !IsVisible(tile.posV3)) return;

        var color = colorPalette.HasColor
            ? SemanticColorResolver.ToVfxColor(colorPalette.Primary)
            : element.AccentColor;
        color.a = style.Alpha;
        var key = new EmissionSourceKey(tile.pos.x, tile.pos.y, element, color);
        if (EmissionSources.TryGetValue(key, out var existing))
        {
            existing.Style = style;
            existing.Color = color;
            existing.RemainingTime = style.EmissionDuration;
            EmissionSources[key] = existing;
            return;
        }

        if (EmissionSources.Count >= MaxEmissionSources) return;

        var source = new EmissionSource
        {
            Position = tile.posV3,
            Style = style,
            Color = color,
            RemainingTime = style.EmissionDuration,
            TimeUntilEmission = style.EmissionInterval
        };
        EmissionSources.Add(key, source);
        EmitParticles(source);
    }

    /// <summary>
    /// 从任意世界坐标直接发射一批共享粒子，供技能、法器等上层表现复用而不创建独立 ParticleSystem。
    /// </summary>
    public static void EmitBurst(
        Vector3 position,
        Color color,
        SkillFlyOverParticleStyle style,
        float spread = 0.35f,
        Vector3 direction = default,
        float directionalSpeed = 0f,
        int particleCount = -1)
    {
        if (!MapBox.isRenderGameplay() || !IsVisible(position)) return;
        EnsureWorld();
        color.a *= style.Alpha;
        EmissionSource source = new()
        {
            Position = position,
            Style = style,
            Color = color,
        };
        EmitParticles(
            source,
            particleCount >= 0 ? particleCount : style.ParticlesPerEmission,
            Mathf.Max(0f, spread),
            direction,
            directionalSpeed);
    }

    public static void Update(float deltaTime)
    {
        EnsureWorld();
        if (_sharedEmitter != null)
        {
            var main = _sharedEmitter.main;
            main.simulationSpeed = Mathf.Max(0.01f, Config.time_scale_asset.multiplier);
        }
        if (deltaTime <= 0f || EmissionSources.Count == 0) return;

        ActiveKeys.Clear();
        foreach (var key in EmissionSources.Keys)
        {
            ActiveKeys.Add(key);
        }

        foreach (var key in ActiveKeys)
        {
            var source = EmissionSources[key];
            source.RemainingTime -= deltaTime;
            source.TimeUntilEmission -= deltaTime;

            while (source.RemainingTime > 0f && source.TimeUntilEmission <= 0f)
            {
                if (MapBox.isRenderGameplay() && IsVisible(source.Position))
                {
                    EmitParticles(source);
                }
                source.TimeUntilEmission += source.Style.EmissionInterval;
            }

            if (source.RemainingTime <= 0f)
            {
                EmissionSources.Remove(key);
            }
            else
            {
                EmissionSources[key] = source;
            }
        }
    }

    private static void EmitParticles(EmissionSource source)
    {
        EmitParticles(source, source.Style.ParticlesPerEmission, 0.35f, Vector3.zero, 0f);
    }

    private static void EmitParticles(
        EmissionSource source,
        int particleCount,
        float spread,
        Vector3 direction,
        float directionalSpeed)
    {
        var emitter = GetEmitter();
        var style = source.Style;
        Vector3 directionalVelocity = direction.sqrMagnitude > 0.0001f
            ? direction.normalized * directionalSpeed
            : Vector3.zero;

        for (var i = 0; i < particleCount; i++)
        {
            var position = source.Position;
            position.x += Randy.randomFloat(-spread, spread);
            position.y += Randy.randomFloat(-spread * 0.28f, spread * 0.45f);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = directionalVelocity + new Vector3(
                    Randy.randomFloat(-style.HorizontalDrift, style.HorizontalDrift),
                    Randy.randomFloat(style.MinRiseSpeed, style.MaxRiseSpeed),
                    0f),
                startColor = source.Color,
                startSize = Randy.randomFloat(style.MinSize, style.MaxSize),
                startLifetime = Randy.randomFloat(style.MinLifetime, style.MaxLifetime)
            };
            emitter.Emit(emitParams, 1);
        }
    }

    private static void EnsureWorld()
    {
        var worldRoot = World.world.transform;
        if (_worldRoot == worldRoot) return;

        _worldRoot = worldRoot;
        _sharedEmitter = null;
        EmissionSources.Clear();
        ActiveKeys.Clear();
    }

    private static bool IsVisible(Vector3 position)
    {
        var viewport = World.world.camera.WorldToViewportPoint(position);
        return viewport.x >= -0.05f && viewport.x <= 1.05f
                                    && viewport.y >= -0.05f && viewport.y <= 1.05f;
    }

    private static ParticleSystem GetEmitter()
    {
        if (_sharedEmitter != null) return _sharedEmitter;

        var emitterObject = new GameObject("Cultiway_SkillFlyOverParticles_Global");
        emitterObject.transform.SetParent(World.world.transform, false);
        emitterObject.transform.localPosition = Vector3.zero;

        var particleSystem = emitterObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.45f;
        main.startSpeed = 0f;
        main.startSize = 0.16f;
        main.maxParticles = MaxParticles;

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
        renderer.sortingOrder = -4;
        renderer.sharedMaterial = LibraryMaterials.instance.mat_world_object;

        particleSystem.Play();
        _sharedEmitter = particleSystem;
        return _sharedEmitter;
    }

    private readonly struct EmissionSourceKey : IEquatable<EmissionSourceKey>
    {
        private readonly int _x;
        private readonly int _y;
        private readonly SkillVfxElementAsset _element;
        private readonly Color32 _color;

        public EmissionSourceKey(int x, int y, SkillVfxElementAsset element, Color color)
        {
            _x = x;
            _y = y;
            _element = element;
            _color = color;
        }

        public bool Equals(EmissionSourceKey other)
        {
            return _x == other._x && _y == other._y && ReferenceEquals(_element, other._element) &&
                   _color.Equals(other._color);
        }

        public override bool Equals(object obj)
        {
            return obj is EmissionSourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _x;
                hashCode = hashCode * 397 ^ _y;
                hashCode = hashCode * 397 ^ _element.GetHashCode();
                hashCode = hashCode * 397 ^ _color.GetHashCode();
                return hashCode;
            }
        }
    }

    private struct EmissionSource
    {
        public Vector3 Position;
        public SkillFlyOverParticleStyle Style;
        public Color Color;
        public float RemainingTime;
        public float TimeUntilEmission;
    }
}
